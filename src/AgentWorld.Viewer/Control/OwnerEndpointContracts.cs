using System.Net;
using AgentWorld.Simulation.Harness;
using Microsoft.AspNetCore.Http;

namespace AgentWorld.Viewer.Control;

public sealed record LocalPairingApprovalHttpRequest(string PairingCode);

public sealed record LocalDeviceRevokeHttpRequest(string? Reason);

public sealed record OwnerControlReceipt(
    string Operation,
    bool Changed,
    bool IsPaused,
    long RunEpoch,
    long Revision,
    long LatestEventId)
{
    public static OwnerControlReceipt From(string operation, bool changed, OwnerWorldSnapshot snapshot) => new(
        operation,
        changed,
        snapshot.IsPaused,
        snapshot.RunEpoch,
        snapshot.Revision,
        snapshot.LatestGlobalEventId);
}

public sealed record OwnerControlFailure(string Code, string Detail);

/// <summary>
/// The normal HTTPS/Tailnet listener never has this port. Only a separately
/// bound loopback listener can approve the first pending device.
/// </summary>
public sealed record OwnerPairingHostOptions(int LocalApprovalPort)
{
    public bool IsLocalApprovalRequest(HttpContext context) =>
        LocalApprovalPort > 0 &&
        context.Connection.LocalPort == LocalApprovalPort &&
        context.Connection.RemoteIpAddress is { } address &&
        IPAddress.IsLoopback(address);
}

public static class OwnerFailures
{
    public static IResult UnpairedObservation() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Owner pairing required",
        detail: "Pair a device and use a signed owner reconnect request to inspect this world.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "owner_pairing_required",
        });

    public static IResult ToHttpResult(OwnerAuthorityFailure failure)
    {
        var (statusCode, code, title) = failure switch
        {
            OwnerAuthorityFailure.InvalidRequest => (StatusCodes.Status400BadRequest, "invalid_request", "Invalid owner request"),
            OwnerAuthorityFailure.InvalidPublicKey => (StatusCodes.Status400BadRequest, "invalid_public_key", "Invalid owner key"),
            OwnerAuthorityFailure.PairingNotFound or OwnerAuthorityFailure.DeviceNotFound or OwnerAuthorityFailure.ChallengeNotFound =>
                (StatusCodes.Status404NotFound, "owner_record_not_found", "Owner record not found"),
            OwnerAuthorityFailure.PublicKeyAlreadyRegistered or OwnerAuthorityFailure.PairingNotPending or OwnerAuthorityFailure.PairingAlreadyActive =>
                (StatusCodes.Status409Conflict, "owner_state_conflict", "Owner state conflict"),
            OwnerAuthorityFailure.PairingCapacityExceeded or OwnerAuthorityFailure.ChallengeCapacityExceeded =>
                (StatusCodes.Status429TooManyRequests, "owner_capacity_reached", "Owner authority capacity reached"),
            OwnerAuthorityFailure.ChallengeConsumed =>
                (StatusCodes.Status409Conflict, "challenge_already_consumed", "Challenge already consumed"),
            OwnerAuthorityFailure.DeviceRevoked =>
                (StatusCodes.Status403Forbidden, "device_revoked", "Device revoked"),
            OwnerAuthorityFailure.PairingExpired or OwnerAuthorityFailure.PairingNotApproved or OwnerAuthorityFailure.PairingCodeMismatch or
            OwnerAuthorityFailure.InvalidCanonicalProof or OwnerAuthorityFailure.InvalidSignature or OwnerAuthorityFailure.ChallengeExpired or
            OwnerAuthorityFailure.ChallengeDeviceMismatch or OwnerAuthorityFailure.ChallengeNonceMismatch =>
                (StatusCodes.Status401Unauthorized, "owner_proof_rejected", "Owner proof rejected"),
            _ => (StatusCodes.Status400BadRequest, "owner_request_rejected", "Owner request rejected"),
        };
        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: $"Owner authority rejected this request: {failure}.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["authorityFailure"] = failure.ToString(),
            });
    }
}
