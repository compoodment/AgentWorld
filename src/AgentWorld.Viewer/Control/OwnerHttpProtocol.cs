using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgentWorld.Viewer.Control;

/// <summary>
/// Transport DTOs and canonical bytes for the owner-device HTTP boundary.
/// These types intentionally carry no bearer token. Every protected operation
/// is authorized by consuming a short-lived, one-use signed challenge.
/// </summary>
public sealed record StartOwnerPairingHttpRequest(string PublicKeySpkiBase64);

public sealed record ActivateOwnerPairingHttpRequest(
    string PairingId,
    string CanonicalProof,
    string SignatureBase64);

public sealed record IssueOwnerChallengeHttpRequest(
    string DeviceId,
    string RequestId,
    string CanonicalProof,
    string SignatureBase64);

public sealed record OwnerSignedHttpRequest<TAction>(
    string DeviceId,
    string ChallengeId,
    string Nonce,
    string Binding,
    string CanonicalProof,
    string SignatureBase64,
    string RequestId,
    TAction Action)
    where TAction : class;

public sealed record OwnerReconnectAction(long AfterEventId);

public sealed record OwnerControlAction(string Operation);

public sealed record OwnerPairingApprovalAction(string PairingId, string PairingCode);

public sealed record OwnerDeviceManagementAction(string DeviceId);

/// <summary>
/// Deliberately empty action body for a signed paired-device registry query.
/// The endpoint and its fixed canonical payload make this read just as
/// challenge-bound and one-use as a world-changing owner request.
/// </summary>
public sealed record OwnerDeviceListAction;

public sealed record OwnerInstructionAction(
    string IdempotencyKey,
    string TargetInhabitantId,
    string Kind,
    string Text);

/// <summary>
/// A stable scalar representation keeps authoring requests independent of a
/// client JSON serializer's ordering or polymorphism behavior. The server
/// maps it to a typed simulation operation only after signed authorization.
/// </summary>
public sealed record OwnerAuthoringOperationAction(
    string Kind,
    string? Id,
    string? Value,
    string? SecondaryValue,
    int? X,
    int? Y,
    bool? IsRenewable);

public sealed record OwnerAuthoringBatchAction(
    string BatchId,
    IReadOnlyList<OwnerAuthoringOperationAction> Operations);

public static class OwnerHttpBinding
{
    public const string Domain = "agentworld.owner-http-binding.v1";

    public static string Create(
        string method,
        string path,
        string requestId,
        string canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        if (!path.StartsWith('/') || path.Contains('?'))
        {
            throw new ArgumentException("A binding path must be an absolute path without a query string.", nameof(path));
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));
        return string.Join(
            '\n',
            Domain,
            $"method={ToBase64Url(Encoding.UTF8.GetBytes(method.ToUpperInvariant()))}",
            $"path={ToBase64Url(Encoding.UTF8.GetBytes(path))}",
            $"request-id={ToBase64Url(Encoding.UTF8.GetBytes(requestId))}",
            $"payload-sha256={ToBase64Url(digest)}");
    }

    public static string ReconnectPayload(OwnerReconnectAction action) => string.Join(
        '\n',
        "agentworld.owner-reconnect.v1",
        $"after-event-id={action.AfterEventId.ToString(CultureInfo.InvariantCulture)}");

    public static string EmptyPayload(string operation) => string.Join(
        '\n',
        "agentworld.owner-control.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}");

    public static string PairingApprovalPayload(OwnerPairingApprovalAction action) => string.Join(
        '\n',
        "agentworld.owner-pairing-approval.v1",
        $"pairing-id={EncodeRequired(action.PairingId, nameof(action.PairingId))}",
        $"pairing-code={EncodeRequired(action.PairingCode, nameof(action.PairingCode))}");

    public static string DeviceManagementPayload(OwnerDeviceManagementAction action) => string.Join(
        '\n',
        "agentworld.owner-device-management.v1",
        $"device-id={EncodeRequired(action.DeviceId, nameof(action.DeviceId))}");

    public static string DeviceListPayload() => EmptyPayload("list_devices");

    public static string InstructionPayload(OwnerInstructionAction action) => string.Join(
        '\n',
        "agentworld.owner-instruction.v1",
        $"idempotency-key={EncodeRequired(action.IdempotencyKey, nameof(action.IdempotencyKey))}",
        $"target-inhabitant-id={EncodeRequired(action.TargetInhabitantId, nameof(action.TargetInhabitantId))}",
        $"kind={EncodeRequired(action.Kind, nameof(action.Kind))}",
        $"text={EncodeRequired(action.Text, nameof(action.Text))}");

    public static string AuthoringPayload(OwnerAuthoringBatchAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(action.Operations);
        var lines = new List<string>
        {
            "agentworld.owner-authoring.v1",
            $"batch-id={EncodeRequired(action.BatchId, nameof(action.BatchId))}",
            $"operation-count={action.Operations.Count.ToString(CultureInfo.InvariantCulture)}",
        };

        for (var index = 0; index < action.Operations.Count; index++)
        {
            var operation = action.Operations[index] ?? throw new ArgumentException("Authoring operations cannot contain null.", nameof(action));
            var prefix = $"op-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.kind={EncodeRequired(operation.Kind, nameof(operation.Kind))}");
            lines.Add($"{prefix}.id={EncodeOptional(operation.Id)}");
            lines.Add($"{prefix}.value={EncodeOptional(operation.Value)}");
            lines.Add($"{prefix}.secondary-value={EncodeOptional(operation.SecondaryValue)}");
            lines.Add($"{prefix}.x={EncodeOptionalInteger(operation.X)}");
            lines.Add($"{prefix}.y={EncodeOptionalInteger(operation.Y)}");
            lines.Add($"{prefix}.is-renewable={EncodeOptionalBoolean(operation.IsRenewable)}");
        }

        return string.Join('\n', lines);
    }

    private static string EncodeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return ToBase64Url(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeOptional(string? value) => value is null
        ? "-"
        : ToBase64Url(Encoding.UTF8.GetBytes(value));

    private static string EncodeOptionalInteger(int? value) => value is null
        ? "-"
        : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string EncodeOptionalBoolean(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => "-",
    };

    private static string ToBase64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

public sealed record OwnerRequestAuthorization(
    string DeviceId,
    string PublicKeyFingerprint,
    string RequestId);

public sealed record OwnerRequestAuthorizationResult(
    OwnerAuthorityFailure Failure,
    OwnerRequestAuthorization? Value)
{
    public bool IsSuccess => Failure == OwnerAuthorityFailure.None && Value is not null;
}

/// <summary>
/// Single ingress guard for signed owner operations. It compares the server's
/// reconstructed binding before consuming the challenge, then persists the
/// consumed anti-replay state before a caller asks the simulation to commit.
/// </summary>
public sealed class OwnerRequestAuthorizer(
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile)
{
    public OwnerRequestAuthorizationResult Authorize<TAction>(
        OwnerSignedHttpRequest<TAction>? request,
        string method,
        string path,
        string canonicalPayload)
        where TAction : class
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.ChallengeId) ||
            string.IsNullOrWhiteSpace(request.Nonce) ||
            string.IsNullOrWhiteSpace(request.CanonicalProof) ||
            string.IsNullOrWhiteSpace(request.SignatureBase64) ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            request.Action is null)
        {
            return new OwnerRequestAuthorizationResult(OwnerAuthorityFailure.InvalidRequest, null);
        }

        var expectedBinding = OwnerHttpBinding.Create(method, path, request.RequestId, canonicalPayload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedBinding),
                Encoding.UTF8.GetBytes(request.Binding ?? string.Empty)))
        {
            return new OwnerRequestAuthorizationResult(OwnerAuthorityFailure.InvalidCanonicalProof, null);
        }

        var consumed = authority.ConsumeChallenge(new OwnerChallengeConsumeRequest(
            request.DeviceId,
            request.ChallengeId,
            request.Nonce,
            expectedBinding,
            request.CanonicalProof,
            request.SignatureBase64));
        // ConsumeChallenge also applies expiry/retention cleanup on rejected
        // requests. Persist before branching so a restart cannot resurrect a
        // just-expired challenge or discarded terminal history.
        stateFile.Save(authority);
        if (!consumed.IsSuccess)
        {
            return new OwnerRequestAuthorizationResult(consumed.Failure, null);
        }

        return new OwnerRequestAuthorizationResult(
            OwnerAuthorityFailure.None,
            new OwnerRequestAuthorization(
                consumed.Value!.DeviceId,
                consumed.Value.PublicKeyFingerprint,
                request.RequestId));
    }
}
