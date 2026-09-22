using System.Globalization;
using System.Security.Cryptography;
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

public sealed record OwnerWorldPublicIntention(
    string CandidateId,
    string Summary,
    string Provider,
    long WorldTick);

public sealed record OwnerWorldInhabitantRelationship(
    string RelationshipId,
    string OtherPartyId,
    string Type,
    string State,
    string PrivacyClass,
    long EffectiveTick);

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
    bool IsDraft)
{
    public OwnerWorldPublicIntention? PublicIntention { get; init; }

    public OwnerWorldProject? Project { get; init; }
    public OwnerWorldSurvival? Survival { get; init; }

    public IReadOnlyList<string> SocialNotes { get; init; } = [];

    public IReadOnlyList<OwnerWorldInhabitantRelationship> Relationships { get; init; } = [];
}

public sealed record OwnerWorldProject(string Label, string Stage, int WorkDone, int WorkRequired, string? Blocker, long StartedTick);
public sealed record OwnerWorldSurvival(int WarmthBasisPoints, int IllnessBasisPoints, bool HasClothing, bool HasTool,
    int NutritionBasisPoints, string? LastMealKind);

public sealed record OwnerWorldStockpile(string OwnerId, string Name, IReadOnlyList<OwnerWorldInventoryEntry> Items);

public sealed record OwnerWorldInstruction(
    string InstructionId,
    string TargetInhabitantId,
    string Kind,
    string Text,
    string State,
    long SubmittedTick,
    long RunEpoch,
    long SubmissionSequence);

public sealed record OwnerWorldCognitionEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record OwnerWorldInhabitantDecision(
    string InhabitantId, string Provider, string CandidateId, long WorldTick,
    double Confidence, string? Model, int? InputTokens, int? OutputTokens,
    string? Role = null, long? LatencyMilliseconds = null, bool FellBack = false);

public sealed record OwnerWorldCognition(
    string Provider,
    bool IsPaused,
    string? InFlightRequestId,
    string? CurrentCandidateId,
    string? CurrentDecisionProvider,
    IReadOnlyList<OwnerWorldCognitionEvent> Events,
    IReadOnlyList<OwnerWorldInhabitantDecision>? Decisions = null);

public sealed record OwnerWorldContentPackage(
    string PackageId,
    string Version,
    string PackageDigest,
    string Lifecycle,
    string? LockDigest,
    long? ValidationTick,
    long? StagedTick,
    long? ActivationTick);

public sealed record OwnerWorldContentGovernanceEvent(
    long EventId,
    long WorldTick,
    string PackageId,
    string Kind,
    string Detail);

public sealed record OwnerWorldSystemsSummary(
    string Season,
    string Weather,
    int EcologyResourceCount,
    int FactionCount,
    int CurrencyAccountCount,
    int CultureCount,
    int ChunkCount,
    int BuildingDefinitionCount = 0,
    int RecipeDefinitionCount = 0,
    int PlacedBuildingCount = 0,
    int ProductionJobCount = 0,
    int DistinctAssetReservationCount = 0,
    long DurableAssetReservationBytes = 0,
    long DecodedAssetCacheBytes = 0,
    long GpuAssetBytes = 0,
    int AssetRenderUnits = 0);

public sealed record OwnerWorldPlacedBuilding(
    string InstanceId,
    string DefinitionId,
    OwnerWorldPosition Position,
    long PlacedTick);

public sealed record OwnerWorldProductionJob(
    string JobId,
    string RecipeId,
    string BuildingInstanceId,
    string WorkerId,
    long StartedTick,
    long CompletionTick,
    string State);

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
    OwnerWorldActor? Actor,
    long LatestEventId)
{
    public IReadOnlyList<OwnerWorldStockpile> Stockpiles { get; init; } = [];
    public OwnerWorldCouncil? Council { get; init; }
    public IReadOnlyList<OwnerWorldInhabitant> Inhabitants { get; init; } = [];

    public OwnerWorldAuthoringState? Authoring { get; init; }

    public IReadOnlyList<OwnerWorldInstruction> Instructions { get; init; } = [];

    public OwnerWorldCognition? Cognition { get; init; }

    public IReadOnlyList<OwnerWorldContentPackage> ContentPackages { get; init; } = [];

    public IReadOnlyList<OwnerWorldContentGovernanceEvent> ContentEvents { get; init; } = [];

    public OwnerWorldSystemsSummary? WorldSystems { get; init; }

    public IReadOnlyList<OwnerWorldPlacedBuilding> PlacedBuildings { get; init; } = [];

    public IReadOnlyList<OwnerWorldProductionJob> ProductionJobs { get; init; } = [];
}

public sealed record OwnerWorldEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record OwnerWorldEventSlice(
    long SnapshotTick,
    long AfterEventId,
    IReadOnlyList<OwnerWorldEvent> Events,
    long EventHistoryFloor = 0,
    bool ResetRequired = false);

public sealed record OwnerWorldCouncil(string? StewardName, string FoodPolicy, string? ProposedPolicy, int Approvals, int Rejections, int Voters);

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

public sealed record OwnerProviderStatusAction;

public sealed record OwnerProviderConfigurationAction(
    string Role,
    string Provider,
    string? Model,
    string? ApiKey,
    bool ForgetCredential,
    string? InhabitantId = null);

public sealed record InhabitantProviderAssignment(string InhabitantId, string Role, string Provider, string? Model = null);

public sealed record OwnerProviderOptionStatus(
    string Provider,
    string Model,
    bool HasCredential);

public sealed record OwnerProviderConfigurationStatus(
    string RoutineProvider,
    string PlanningProvider,
    long Revision,
    IReadOnlyList<OwnerProviderOptionStatus> Providers,
    IReadOnlyList<InhabitantProviderAssignment>? Assignments = null);

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

public sealed record OwnerContentDependencyAction(
    string PackageId,
    string MinimumVersion,
    string MaximumExclusiveVersion,
    bool Optional);

public sealed record OwnerContentDefinitionAction(
    string Kind,
    string LocalId,
    string Version,
    string DisplayName,
    string PayloadDigest,
    string? PayloadJson = null);

public sealed record OwnerContentAssetReservationAction(
    string AssetId,
    string NormalizedDigest,
    string DecodeProfile,
    long DurableStorageBytes,
    long DecodedCacheBytes,
    long GpuBytes,
    int RenderUnits);

public sealed record OwnerContentPackageAction(
    string PackageId,
    string Version,
    string PackageDigest,
    IReadOnlyList<OwnerContentDependencyAction> Dependencies,
    IReadOnlyList<OwnerContentDefinitionAction> Definitions,
    IReadOnlyList<string> DeclaredCapabilities,
    IReadOnlyList<OwnerContentAssetReservationAction>? Assets = null);

public sealed record OwnerContentPackageIdAction(string PackageId);

public sealed record OwnerContentRollbackAction(string PackageId, string Reason);

public sealed record OwnerBuildingPlacementAction(
    string InstanceId,
    string DefinitionId,
    int X,
    int Y);

public sealed record OwnerProductionStartAction(
    string RecipeId,
    string BuildingInstanceId,
    string WorkerId);

public sealed record OwnerBuildingPlacementResult(
    bool Applied,
    string InstanceId,
    string DefinitionId,
    OwnerWorldPosition Position,
    string? Failure);

public sealed record OwnerProductionStartResult(
    bool Applied,
    string? JobId,
    string RecipeId,
    string? Failure);

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

public sealed record OwnerContentPackageReceipt(
    string Operation,
    bool Applied,
    string PackageId,
    string Version,
    string PackageDigest,
    string Lifecycle,
    string? LockDigest,
    long? ValidationTick,
    long? StagedTick,
    long? ActivationTick,
    string? Failure,
    string? ManifestDigest = null);

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
            baseline.Snapshot.LatestEventId < requestedAfterEventId ||
            baseline.Events.EventHistoryFloor < 0 ||
            baseline.Events.EventHistoryFloor > baseline.Snapshot.LatestEventId ||
            baseline.Events.ResetRequired != (requestedAfterEventId < baseline.Events.EventHistoryFloor))
        {
            failure = "The owner reconnect baseline is internally inconsistent.";
            return false;
        }

        var expectedEventId = checked(Math.Max(requestedAfterEventId, baseline.Events.EventHistoryFloor) + 1);
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

    public static string ProviderStatus() => Control("provider_status");

    public static string ProviderConfiguration(OwnerProviderConfigurationAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var apiKeyDigest = action.ApiKey is null
            ? "-"
            : ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(action.ApiKey)));
        var payload = string.Join(
            '\n',
            "agentworld.owner-provider-configuration.v1",
            $"role={EncodeRequired(action.Role, nameof(action.Role))}",
            $"provider={EncodeRequired(action.Provider, nameof(action.Provider))}",
            $"model={EncodeOptional(action.Model)}",
            $"api-key-sha256={apiKeyDigest}",
            $"forget-credential={action.ForgetCredential.ToString().ToLowerInvariant()}");
        return action.InhabitantId is null ? payload : payload + "\ninhabitant=" + EncodeRequired(action.InhabitantId, nameof(action.InhabitantId));
    }

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

    public static string ContentPropose(OwnerContentPackageAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(action.Dependencies);
        ArgumentNullException.ThrowIfNull(action.Definitions);
        ArgumentNullException.ThrowIfNull(action.DeclaredCapabilities);
        var assets = action.Assets ?? [];
        var lines = new List<string>
        {
            "agentworld.owner-content-propose.v1",
            $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}",
            $"version={EncodeRequired(action.Version, nameof(action.Version))}",
            $"package-digest={EncodeRequired(action.PackageDigest, nameof(action.PackageDigest))}",
            $"dependency-count={action.Dependencies.Count.ToString(CultureInfo.InvariantCulture)}",
            $"definition-count={action.Definitions.Count.ToString(CultureInfo.InvariantCulture)}",
            $"asset-count={assets.Count.ToString(CultureInfo.InvariantCulture)}",
            $"capability-count={action.DeclaredCapabilities.Count.ToString(CultureInfo.InvariantCulture)}",
        };

        for (var index = 0; index < action.Dependencies.Count; index++)
        {
            var dependency = action.Dependencies[index] ?? throw new ArgumentException(
                "Content dependencies cannot contain null.",
                nameof(action));
            var prefix = $"dependency-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.package-id={EncodeRequired(dependency.PackageId, nameof(dependency.PackageId))}");
            lines.Add($"{prefix}.minimum={EncodeRequired(dependency.MinimumVersion, nameof(dependency.MinimumVersion))}");
            lines.Add($"{prefix}.maximum={EncodeRequired(dependency.MaximumExclusiveVersion, nameof(dependency.MaximumExclusiveVersion))}");
            lines.Add($"{prefix}.optional={dependency.Optional.ToString().ToLowerInvariant()}");
        }

        for (var index = 0; index < action.Definitions.Count; index++)
        {
            var definition = action.Definitions[index] ?? throw new ArgumentException(
                "Content definitions cannot contain null.",
                nameof(action));
            var prefix = $"definition-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.kind={EncodeRequired(definition.Kind, nameof(definition.Kind))}");
            lines.Add($"{prefix}.local-id={EncodeRequired(definition.LocalId, nameof(definition.LocalId))}");
            lines.Add($"{prefix}.version={EncodeRequired(definition.Version, nameof(definition.Version))}");
            lines.Add($"{prefix}.display-name={EncodeRequired(definition.DisplayName, nameof(definition.DisplayName))}");
            lines.Add($"{prefix}.payload-digest={EncodeRequired(definition.PayloadDigest, nameof(definition.PayloadDigest))}");
            lines.Add($"{prefix}.payload-json={EncodeOptional(definition.PayloadJson)}");
        }

        for (var index = 0; index < assets.Count; index++)
        {
            var asset = assets[index] ?? throw new ArgumentException(
                "Content asset reservations cannot contain null.",
                nameof(action));
            var prefix = $"asset-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.asset-id={EncodeRequired(asset.AssetId, nameof(asset.AssetId))}");
            lines.Add($"{prefix}.normalized-digest={EncodeRequired(asset.NormalizedDigest, nameof(asset.NormalizedDigest))}");
            lines.Add($"{prefix}.decode-profile={EncodeRequired(asset.DecodeProfile, nameof(asset.DecodeProfile))}");
            lines.Add($"{prefix}.durable-storage={asset.DurableStorageBytes.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}.decoded-cache={asset.DecodedCacheBytes.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}.gpu-bytes={asset.GpuBytes.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}.render-units={asset.RenderUnits.ToString(CultureInfo.InvariantCulture)}");
        }

        for (var index = 0; index < action.DeclaredCapabilities.Count; index++)
        {
            lines.Add($"capability-{index.ToString(CultureInfo.InvariantCulture)}={EncodeRequired(
                action.DeclaredCapabilities[index],
                nameof(action.DeclaredCapabilities))}");
        }

        return string.Join('\n', lines);
    }

    public static string ContentPackageId(string operation, OwnerContentPackageIdAction action) => string.Join(
        '\n',
        "agentworld.owner-content-lifecycle.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}",
        $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}");

    public static string ContentRollback(OwnerContentRollbackAction action) => string.Join(
        '\n',
        "agentworld.owner-content-rollback.v1",
        $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}",
        $"reason={EncodeRequired(action.Reason, nameof(action.Reason))}");

    public static string BuildingPlacement(OwnerBuildingPlacementAction action) => string.Join(
        '\n',
        "agentworld.owner-building-placement.v1",
        $"instance-id={EncodeRequired(action.InstanceId, nameof(action.InstanceId))}",
        $"definition-id={EncodeRequired(action.DefinitionId, nameof(action.DefinitionId))}",
        $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
        $"y={action.Y.ToString(CultureInfo.InvariantCulture)}");

    public static string ProductionStart(OwnerProductionStartAction action) => string.Join(
        '\n',
        "agentworld.owner-production-start.v1",
        $"recipe-id={EncodeRequired(action.RecipeId, nameof(action.RecipeId))}",
        $"building-instance-id={EncodeRequired(action.BuildingInstanceId, nameof(action.BuildingInstanceId))}",
        $"worker-id={EncodeRequired(action.WorkerId, nameof(action.WorkerId))}");

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

    public Task<OwnerBuildingPlacementResult> PlaceBuildingAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerBuildingPlacementAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerBuildingPlacementAction, OwnerBuildingPlacementResult>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerBuildingPlacement,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.BuildingPlacement(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerProductionStartResult> StartProductionAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerProductionStartAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerProductionStartAction, OwnerProductionStartResult>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerProductionStart,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.ProductionStart(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerContentPackageReceipt> ProposeContentAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerContentPackageAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerContentPackageAction, OwnerContentPackageReceipt>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerContentPropose,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.ContentPropose(action),
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerContentPackageReceipt> ValidateContentAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerContentPackageIdAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        SendContentLifecycleAsync(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerContentValidate,
            "validate",
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerContentPackageReceipt> ApproveContentAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerContentPackageIdAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        SendContentLifecycleAsync(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerContentApprove,
            "approve",
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerContentPackageReceipt> StageContentAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerContentPackageIdAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        SendContentLifecycleAsync(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerContentStage,
            "stage",
            action,
            deviceKey,
            cancellationToken);

    public Task<OwnerContentPackageReceipt> RollbackContentAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerContentRollbackAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerContentRollbackAction, OwnerContentPackageReceipt>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerContentRollback,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.ContentRollback(action),
            action,
            deviceKey,
            cancellationToken);

    private Task<OwnerContentPackageReceipt> SendContentLifecycleAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        string path,
        string operation,
        OwnerContentPackageIdAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerContentPackageIdAction, OwnerContentPackageReceipt>(
            serverUri,
            authority,
            deviceId,
            path,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.ContentPackageId(operation, action),
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

    public Task<OwnerProviderConfigurationStatus> GetProviderStatusAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken)
    {
        var action = new OwnerProviderStatusAction();
        return pairing.SendSignedActionAsync<OwnerProviderStatusAction, OwnerProviderConfigurationStatus>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerProviderStatus,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.ProviderStatus(),
            action,
            deviceKey,
            cancellationToken);
    }

    public Task<OwnerProviderConfigurationStatus> ConfigureProviderAsync(
        Uri serverUri,
        OwnerAuthorityIdentity authority,
        string deviceId,
        OwnerProviderConfigurationAction action,
        IOwnerDeviceSigner deviceKey,
        CancellationToken cancellationToken) =>
        pairing.SendSignedActionAsync<OwnerProviderConfigurationAction, OwnerProviderConfigurationStatus>(
            serverUri,
            authority,
            deviceId,
            OwnerPairingEndpoints.OwnerProviderConfigure,
            OwnerPairingProtocol.CreateRequestId(),
            OwnerWorldActionPayload.ProviderConfiguration(action),
            action,
            deviceKey,
            cancellationToken);
}
