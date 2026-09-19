using System.Globalization;
using System.Text;
using AgentWorld.GodotClient.Pairing;

namespace AgentWorld.GodotClient.UI;

// These client-owned DTOs mirror the public owner projection. Keeping them in
// the Godot project prevents the renderer from taking a project reference on
// the authoritative server or simulation assemblies.
public sealed record OwnerWorldProtocolVersion(int Major, int Minor);

public sealed record OwnerWorldHandshake(
    OwnerWorldProtocolVersion Protocol,
    IReadOnlyList<string> ServerCapabilities,
    IReadOnlyList<string> ClientCapabilities);

public sealed record OwnerWorldPosition(int X, int Y);

public sealed record OwnerWorldTile(int X, int Y, string Terrain);

public sealed record OwnerWorldObject(string Id, string Kind, OwnerWorldPosition Position);

public sealed record OwnerWorldResource(
    string Id,
    string Kind,
    OwnerWorldPosition Position,
    bool IsRenewable,
    string State);

public sealed record OwnerWorldInventoryEntry(string Kind, int Quantity);

public sealed record OwnerWorldDecisionFactor(string Key, string Detail);

public sealed record OwnerWorldRoute(
    string Status,
    string? DestinationId,
    OwnerWorldPosition? Destination,
    IReadOnlyList<OwnerWorldPosition> Steps,
    string TopologyManifestDigest);

public sealed record OwnerWorldSpatialKnowledge(
    OwnerWorldPosition CurrentTile,
    IReadOnlyList<OwnerWorldPosition> PerceivedTiles,
    IReadOnlyList<OwnerWorldPosition> KnownTiles);

public sealed record OwnerWorldInhabitant(
    string Id,
    string DisplayName,
    string Lifecycle,
    OwnerWorldPosition Position,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    IReadOnlyList<OwnerWorldInventoryEntry> Inventory,
    IReadOnlyList<OwnerWorldDecisionFactor> DecisionFactors,
    OwnerWorldRoute Route,
    OwnerWorldSpatialKnowledge SpatialKnowledge,
    bool IsDraft);

public sealed record OwnerWorldInstruction(
    string InstructionId,
    string TargetInhabitantId,
    string Kind,
    string Text,
    string State,
    long SubmittedTick,
    long RunEpoch,
    long SubmissionSequence);

public sealed record OwnerWorldAuthoringState(
    bool IsPaused,
    long RunEpoch,
    long Revision,
    long TopologyRevision,
    string InitialMapManifestDigest,
    string CurrentMapManifestDigest,
    string Weather,
    string Season,
    IReadOnlyList<string> ApprovedAssetReferences);

public sealed record OwnerWorldActor(
    string Id,
    OwnerWorldPosition Position,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    int FoodItems,
    int WoodItems);

public sealed record OwnerWorldSnapshot(
    string WorldId,
    long WorldTick,
    string MapManifestDigest,
    IReadOnlyList<OwnerWorldTile> Tiles,
    IReadOnlyList<OwnerWorldObject> Objects,
    IReadOnlyList<OwnerWorldResource> Resources,
    OwnerWorldActor Actor,
    long LatestEventId)
{
    public IReadOnlyList<OwnerWorldInhabitant> Inhabitants { get; init; } = [];

    public OwnerWorldAuthoringState? Authoring { get; init; }

    public IReadOnlyList<OwnerWorldInstruction> Instructions { get; init; } = [];
}

public sealed record OwnerWorldEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record OwnerWorldEventSlice(
    long SnapshotTick,
    long AfterEventId,
    IReadOnlyList<OwnerWorldEvent> Events);

public sealed record OwnerWorldReconnectBaseline(OwnerWorldSnapshot Snapshot, OwnerWorldEventSlice Events);

public sealed record OwnerWorldReconnect(OwnerWorldHandshake Handshake, OwnerWorldReconnectBaseline Baseline);

public sealed record OwnerReconnectAction(long AfterEventId);

public sealed record OwnerControlAction(string Operation);

public sealed record OwnerPairingApprovalAction(string PairingId, string PairingCode);

public sealed record OwnerDeviceManagementAction(string DeviceId);

/// <summary>
/// Empty typed body for the challenge-bound paired-device registry query.
/// The fixed canonical payload prevents an unsigned or replayable registry
/// read from becoming an accidental bearer endpoint.
/// </summary>
public sealed record OwnerDeviceListAction;

public sealed record OwnerInstructionAction(
    string IdempotencyKey,
    string TargetInhabitantId,
    string Kind,
    string Text);

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

public sealed record OwnerControlReceipt(
    string Operation,
    bool Changed,
    bool IsPaused,
    long RunEpoch,
    long Revision,
    long LatestEventId);

public sealed record OwnerInstructionReceipt(
    string InstructionId,
    string IdempotencyKey,
    long SubmittedTick,
    long RunEpoch,
    long Revision);

public sealed record OwnerAuthoringBatchReceipt(
    string BatchId,
    bool Applied,
    string? Failure,
    long Revision,
    long TopologyRevision,
    string CurrentMapManifestDigest);

/// <summary>
/// Checks a complete signed owner baseline before giving it to the renderer.
/// A failure deliberately retains the last accepted observation, just as a
/// networked client must never invent state during a broken refresh.
/// </summary>
public sealed class OwnerWorldObservationSession
{
    private static readonly string[] RequiredCapabilities =
    [
        "owner-observation.read.v1",
        "inhabitant-inspection.read.v1",
        "spatial-knowledge.read.v1",
        "owner-control.request.v1",
        "paused-authoring.request.v1",
    ];

    public OwnerWorldReconnect? Current { get; private set; }

    public long EventCursor => Current?.Baseline.Snapshot.LatestEventId ?? 0;

    public bool TryAccept(OwnerWorldReconnect response, long requestedAfterEventId, out string failure)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfNegative(requestedAfterEventId);
        if (response.Handshake?.Protocol is null || response.Handshake.Protocol.Major != 1)
        {
            failure = "The owner observation protocol major is unsupported.";
            return false;
        }

        var capabilities = (response.Handshake.ServerCapabilities ?? []).ToHashSet(StringComparer.Ordinal);
        if (RequiredCapabilities.Any(capability => !capabilities.Contains(capability)))
        {
            failure = "The server did not advertise the required paired-owner capabilities.";
            return false;
        }

        var baseline = response.Baseline;
        if (baseline?.Snapshot is null || baseline.Events is null ||
            baseline.Events.AfterEventId != requestedAfterEventId ||
            baseline.Events.SnapshotTick != baseline.Snapshot.WorldTick ||
            baseline.Snapshot.LatestEventId < requestedAfterEventId)
        {
            failure = "The owner reconnect baseline is internally inconsistent.";
            return false;
        }

        var expectedEventId = checked(requestedAfterEventId + 1);
        foreach (var worldEvent in baseline.Events.Events ?? [])
        {
            if (worldEvent.EventId != expectedEventId ||
                worldEvent.EventId > baseline.Snapshot.LatestEventId ||
                worldEvent.WorldTick > baseline.Snapshot.WorldTick)
            {
                failure = "The owner reconnect event suffix is not ordered against its snapshot.";
                return false;
            }

            expectedEventId = checked(expectedEventId + 1);
        }

        if (expectedEventId - 1 != baseline.Snapshot.LatestEventId)
        {
            failure = "The owner reconnect event suffix is incomplete for its snapshot.";
            return false;
        }

        if (Current is { } current &&
            (baseline.Snapshot.WorldTick < current.Baseline.Snapshot.WorldTick ||
             baseline.Snapshot.LatestEventId < current.Baseline.Snapshot.LatestEventId))
        {
            failure = "The owner reconnect response regresses the held world state.";
            return false;
        }

        Current = response;
        failure = string.Empty;
        return true;
    }
}

/// <summary>
/// Canonical scalar payloads for every currently exposed owner operation.
/// They mirror the server contract rather than trusting JSON field order.
/// </summary>
public static class OwnerWorldActionPayload
{
    public static string Reconnect(OwnerReconnectAction action) => string.Join(
        '\n',
        "agentworld.owner-reconnect.v1",
        $"after-event-id={action.AfterEventId.ToString(CultureInfo.InvariantCulture)}");

    public static string Control(string operation) => string.Join(
        '\n',
        "agentworld.owner-control.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}");

    public static string PairingApproval(OwnerPairingApprovalAction action) => string.Join(
        '\n',
        "agentworld.owner-pairing-approval.v1",
        $"pairing-id={EncodeRequired(action.PairingId, nameof(action.PairingId))}",
        $"pairing-code={EncodeRequired(action.PairingCode, nameof(action.PairingCode))}");

    public static string DeviceManagement(OwnerDeviceManagementAction action) => string.Join(
        '\n',
        "agentworld.owner-device-management.v1",
        $"device-id={EncodeRequired(action.DeviceId, nameof(action.DeviceId))}");

    public static string DeviceList() => Control("list_devices");

    public static string Instruction(OwnerInstructionAction action) => string.Join(
        '\n',
        "agentworld.owner-instruction.v1",
        $"idempotency-key={EncodeRequired(action.IdempotencyKey, nameof(action.IdempotencyKey))}",
        $"target-inhabitant-id={EncodeRequired(action.TargetInhabitantId, nameof(action.TargetInhabitantId))}",
        $"kind={EncodeRequired(action.Kind, nameof(action.Kind))}",
        $"text={EncodeRequired(action.Text, nameof(action.Text))}");

    public static string Authoring(OwnerAuthoringBatchAction action)
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
            var operation = action.Operations[index] ?? throw new ArgumentException("Authoring operations cannot be null.", nameof(action));
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

    private static string EncodeOptional(string? value) => value is null ? "-" : ToBase64Url(Encoding.UTF8.GetBytes(value));

    private static string EncodeOptionalInteger(int? value) => value is null
        ? "-"
        : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string EncodeOptionalBoolean(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => "-",
    };

    private static string ToBase64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

/// <summary>
/// Uses an owner device key to issue a one-use challenge and sign every
/// operation. It stores no bearer token and has no local mutation fallback.
/// </summary>
public sealed class OwnerWorldApi
{
    private readonly OwnerPairingClient pairing;

    public OwnerWorldApi(HttpClient httpClient)
    {
        pairing = new OwnerPairingClient(httpClient);
    }

    public Task<OwnerPairingStart> StartPairingAsync(
        Uri serverUri,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.StartPairingAsync(serverUri, deviceKey, cancellationToken);

    public Task<OwnerPairingStatus> GetPairingStatusAsync(
        Uri serverUri,
        string pairingId,
        CancellationToken cancellationToken) =>
        pairing.GetPairingStatusAsync(serverUri, pairingId, cancellationToken);

    public Task<OwnerDevice> ActivatePairingAsync(
        Uri serverUri,
        OwnerPairingStart pairingStart,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.ActivatePairingAsync(serverUri, pairingStart, deviceKey, cancellationToken);

    public Task<OwnerWorldReconnect> ReconnectAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        long afterEventId,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken)
    {
        var action = new OwnerReconnectAction(afterEventId);
        return pairing.SendSignedActionAsync<OwnerReconnectAction, OwnerWorldReconnect>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerReconnect,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.Reconnect(action),
            action,
            deviceKey,
            cancellationToken);
    }

    public Task<OwnerControlReceipt> SetPausedAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        bool paused,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken)
    {
        var operation = paused ? "pause" : "resume";
        var action = new OwnerControlAction(operation);
        var path = paused ? OwnerPairingEndpoints.OwnerPause : OwnerPairingEndpoints.OwnerResume;
        return pairing.SendSignedActionAsync<OwnerControlAction, OwnerControlReceipt>(
            serverUri,
            authority,
            deviceId,
            path,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.Control(operation),
            action,
            deviceKey,
            cancellationToken);
    }

    public Task<OwnerInstructionReceipt> SubmitInstructionAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerInstructionAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerInstructionAction, OwnerInstructionReceipt>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerInstructions,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.Instruction(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerAuthoringBatchReceipt> SubmitAuthoringAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerAuthoringBatchAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerAuthoringBatchAction, OwnerAuthoringBatchReceipt>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerAuthoring,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.Authoring(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerPairingApproval> ApprovePairingAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerPairingApprovalAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerPairingApprovalAction, OwnerPairingApproval>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerPairingApproval,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.PairingApproval(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerDevice> RevokeDeviceAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerDeviceManagementAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerDeviceManagementAction, OwnerDevice>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerDeviceRevoke,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.DeviceManagement(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerDevice[]> ListDevicesAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken)
    {
        var action = new OwnerDeviceListAction();
        return pairing.SendSignedActionAsync<OwnerDeviceListAction, OwnerDevice[]>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerDeviceList,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.DeviceList(),
            action,
            deviceKey,
            cancellationToken);
    }
}
