using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Kernel;
using AgentWorld.Simulation.Society;
using AgentWorld.Simulation.World;

namespace AgentWorld.Simulation.Playtest;

public sealed record PlaytestInhabitantState(
    string InhabitantId,
    GridPoint Position,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    int MoveWaitTicks,
    string Personality,
    string Aspiration);

public sealed record PlaytestResourceState(string ResourceId, ResourceState State);

public sealed record PlaytestWorldEvent(
    long EventId,
    long WorldTick,
    string Kind,
    string Detail);

public sealed record PrivateWorldRuntimeState(
    int SchemaVersion,
    string WorldSeed,
    SeededMap Map,
    SocietyWorldRuntimeState Society,
    IReadOnlyList<PlaytestInhabitantState> Inhabitants,
    IReadOnlyList<PlaytestResourceState> Resources,
    IReadOnlyList<PlaytestWorldEvent> Events,
    IReadOnlyList<OwnerQueuedInstruction>? Instructions = null,
    IReadOnlyList<string>? CompletedInstructionIds = null,
    ContentRegistryState? Content = null,
    WorldSystemsState? WorldSystems = null,
    DeclarativeWorldContentState? WorldContent = null);

public sealed record PrivateWorldStepResult(
    bool Advanced,
    string Outcome,
    long WorldTick,
    IReadOnlyList<SocietyCognitionDispatchResult> Decisions,
    IReadOnlyList<PlaytestWorldEvent> Events);

/// <summary>
/// The live private-world composition for the first complete single-player
/// alpha. Society owns identity, lifecycle, relationships, and inventory;
/// this composition owns the map, physical positions/needs, cognition cadence,
/// and the world-facing event stream. It is intentionally separate from the
/// legacy one-actor owner fixture while the owner protocol is migrated.
/// </summary>
public sealed class PrivateWorldRuntime : IDisposable
{
    public const int StateSchemaVersion = 2;
    private const string HouseholdId = "household:camp-alpha";
    private const string FoodLotId = "food:camp-alpha";
    private const string BerryResourceId = "berry-patch";

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string worldSeed;
    private readonly Func<string, IDecisionProvider>? providerFactory;
    private readonly double minimumCognitionConfidence;
    private SeededMap map;
    private SocietyWorldRuntime society;
    private ContentPackageRegistry contentRegistry;
    private WorldSystemsState worldSystems;
    private DeclarativeWorldContentState worldContent;
    private readonly Dictionary<string, PlaytestInhabitantState> inhabitants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResourceState> resources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerQueuedInstruction> instructionsByIdempotency =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerInstructionReceipt> instructionReceipts =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> completedInstructionIds = new(StringComparer.Ordinal);
    private readonly List<PlaytestWorldEvent> events = [];
    private long nextEventId = 1;
    private long nextInstructionSequence = 1;

    public PrivateWorldRuntime(
        string worldSeed,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxCognitionQueueLength = 64,
        int maxCognitionDispatchPerCycle = 4,
        double minimumCognitionConfidence = 0.5)
    {
        this.worldSeed = NormalizeRequiredText(worldSeed, nameof(worldSeed));
        this.providerFactory = providerFactory;
        if (maxCognitionQueueLength <= 0 || maxCognitionDispatchPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCognitionQueueLength));
        }

        if (double.IsNaN(minimumCognitionConfidence) ||
            double.IsInfinity(minimumCognitionConfidence) ||
            minimumCognitionConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumCognitionConfidence));
        }

        this.minimumCognitionConfidence = minimumCognitionConfidence;
        contentRegistry = new ContentPackageRegistry();
        worldContent = new DeclarativeWorldContentState([], []);
        map = SeededMapGenerator.Generate(this.worldSeed);
        worldSystems = CreateWorldSystems(this.worldSeed, map);
        society = CreateSociety(
            this.worldSeed,
            providerFactory,
            maxCognitionQueueLength,
            maxCognitionDispatchPerCycle,
            minimumCognitionConfidence);
        CreatePhysicalState();
        foreach (var resource in map.Resources)
        {
            resources.Add(resource.Id, ResourceState.Available);
        }

        AppendEvent("world_created", $"{this.worldSeed}:inhabitants:{inhabitants.Count}");
    }

    public long WorldTick => society.Checkpoint.WorldTick;

    public SocietyCheckpoint Society => society.Checkpoint;

    public ContentRegistryState Content => contentRegistry.ExportState();

    public WorldSystemsState WorldSystems => worldSystems;

    public IReadOnlyList<PlaytestInhabitantState> Inhabitants => inhabitants.Values
        .OrderBy(item => item.InhabitantId, StringComparer.Ordinal)
        .ToArray();

    public PrivateWorldRuntimeState ExportState()
    {
        gate.Wait();
        try
        {
            return CaptureState();
        }
        finally
        {
            gate.Release();
        }
    }

    public static PrivateWorldRuntime Restore(
        PrivateWorldRuntimeState state,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxCognitionDispatchPerCycle = 4,
        double minimumCognitionConfidence = 0.5)
    {
        ValidateStateForCodec(state);
        var runtime = new PrivateWorldRuntime(
            state.WorldSeed,
            providerFactory,
            state.Society.Cognition.MaxQueueLength,
            maxCognitionDispatchPerCycle,
            minimumCognitionConfidence);
        if (!string.Equals(runtime.map.ManifestDigest, state.Map.ManifestDigest, StringComparison.Ordinal))
        {
            runtime.Dispose();
            throw new InvalidDataException("The private-world map does not match deterministic regeneration.");
        }

        runtime.map = state.Map;
        runtime.society.Dispose();
        runtime.society = SocietyWorldRuntime.Restore(
            state.Society,
            providerFactory,
            minimumCognitionConfidence);
        runtime.contentRegistry = ContentPackageRegistry.Restore(state.Content);
        runtime.worldContent = state.WorldContent ?? RebuildWorldContent(runtime.contentRegistry.ExportState());
        runtime.worldSystems = state.WorldSystems is null
            ? AdvanceWorldSystemsTo(
                CreateWorldSystems(state.WorldSeed, state.Map),
                state.Society.Society.WorldTick)
            : state.WorldSystems;
        runtime.inhabitants.Clear();
        foreach (var inhabitant in state.Inhabitants)
        {
            runtime.inhabitants.Add(inhabitant.InhabitantId, inhabitant);
        }

        runtime.resources.Clear();
        foreach (var resource in state.Resources)
        {
            runtime.resources.Add(resource.ResourceId, resource.State);
        }

        runtime.instructionsByIdempotency.Clear();
        runtime.instructionReceipts.Clear();
        runtime.completedInstructionIds.Clear();
        foreach (var instruction in state.Instructions ?? [])
        {
            if (!runtime.instructionsByIdempotency.TryAdd(instruction.IdempotencyKey, instruction))
            {
                runtime.Dispose();
                throw new InvalidDataException("The private-world instruction idempotency keys are duplicated.");
            }

            runtime.instructionReceipts.Add(
                instruction.IdempotencyKey,
                new OwnerInstructionReceipt(
                    instruction.InstructionId,
                    instruction.IdempotencyKey,
                    instruction.SubmittedTick,
                    instruction.RunEpoch,
                    instruction.SubmissionSequence));
        }

        foreach (var completedInstructionId in state.CompletedInstructionIds ?? [])
        {
            runtime.completedInstructionIds.Add(completedInstructionId);
        }

        runtime.nextInstructionSequence = runtime.instructionsByIdempotency.Count == 0
            ? 1
            : checked(runtime.instructionsByIdempotency.Values.Max(item => item.SubmissionSequence) + 1);

        runtime.events.Clear();
        runtime.events.AddRange(state.Events);
        runtime.nextEventId = runtime.events.Count == 0 ? 1 : checked(runtime.events[^1].EventId + 1);
        runtime.Validate();
        return runtime;
    }

    public async ValueTask<PrivateWorldStepResult> AdvanceOneTickAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (society.Checkpoint.IsPaused)
            {
                return new PrivateWorldStepResult(false, "paused", WorldTick, [], []);
            }

            var startingEvent = events.Count;
            var targetTick = checked(WorldTick + 1);
            society.AdvanceTo(targetTick);
            var previousClimate = worldSystems.Climate;
            worldSystems = WorldSystemsRules.AdvanceOneTick(worldSystems);
            SyncEcologyResourceStates();
            if (previousClimate.Season != worldSystems.Climate.Season ||
                previousClimate.Weather != worldSystems.Climate.Weather)
            {
                AppendEvent(
                    "weather_changed",
                    $"{worldSystems.Climate.Season.ToString().ToLowerInvariant()}:{worldSystems.Climate.Weather.ToString().ToLowerInvariant()}");
            }

            var readyPackages = contentRegistry.ExportState().Packages
                .Where(package => package.Lifecycle == ContentPackageLifecycle.Staged &&
                    package.StagedTick is { } stagedTick && targetTick > stagedTick)
                .OrderBy(package => package.Manifest.PackageId, StringComparer.Ordinal)
                .ToArray();
            var activatedWorldContent = worldContent;
            foreach (var package in readyPackages)
            {
                activatedWorldContent = ContentDefinitionPayloadCodec.ApplyPackage(
                    activatedWorldContent,
                    package.Manifest);
            }

            foreach (var activated in contentRegistry.ActivateReady(targetTick))
            {
                AppendEvent("content_activated", activated.Manifest.PackageId);
                if (activated.Manifest.Definitions.Any(definition => !string.IsNullOrWhiteSpace(definition.PayloadJson)))
                {
                    AppendEvent(
                        "content_definitions_activated",
                        $"{activated.Manifest.PackageId}:buildings={activatedWorldContent.Buildings.Count}:recipes={activatedWorldContent.Recipes.Count}");
                }
            }
            worldContent = activatedWorldContent;

            DrainNeeds();
            RemoveDeadPhysicalState();
            EnqueueDueCognition();
            var dispatch = await society.DispatchCognitionAsync(cancellationToken).ConfigureAwait(false);
            foreach (var decision in dispatch.Decisions.OrderBy(item => item.InhabitantId, StringComparer.Ordinal))
            {
                ApplyDecision(decision);
            }

            AppendEvent("tick_advanced", targetTick.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var newEvents = events.Skip(startingEvent).ToArray();
            return new PrivateWorldStepResult(true, "advanced", targetTick, dispatch.Decisions, newEvents);
        }
        finally
        {
            gate.Release();
        }
    }

    public OwnerInstructionReceipt SubmitInstruction(OwnerInstructionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateInstructionRequest(request);
        gate.Wait();
        try
        {
            var targetId = request.TargetInhabitantId.Trim();
            var target = society.Checkpoint.Inhabitants.SingleOrDefault(item => item.Id == targetId);
            if (target is null || target.Status != SocietyInhabitantStatus.Active)
            {
                throw new ArgumentException($"No active inhabitant with ID '{targetId}' exists.", nameof(request));
            }

            if (instructionsByIdempotency.TryGetValue(request.IdempotencyKey, out var existing))
            {
                if (!Matches(existing, request))
                {
                    throw new InvalidOperationException(
                        "An idempotency key cannot be reused for a different instruction request.");
                }

                return instructionReceipts[request.IdempotencyKey];
            }

            var sequence = nextInstructionSequence++;
            var instruction = new OwnerQueuedInstruction(
                $"private-instruction-{sequence.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)}",
                request.IdempotencyKey.Trim(),
                request.IssuerId.Trim(),
                targetId,
                request.Kind,
                request.Text.Trim(),
                WorldTick,
                society.Checkpoint.RunEpoch,
                sequence,
                OwnerInstructionState.Queued);
            instructionsByIdempotency.Add(instruction.IdempotencyKey, instruction);
            var receipt = new OwnerInstructionReceipt(
                instruction.InstructionId,
                instruction.IdempotencyKey,
                instruction.SubmittedTick,
                instruction.RunEpoch,
                sequence);
            instructionReceipts.Add(instruction.IdempotencyKey, receipt);
            AppendEvent("instruction_queued", $"{instruction.InstructionId}:{ToWireValue(instruction.Kind)}");
            return receipt;
        }
        finally
        {
            gate.Release();
        }
    }

    public static ContentResolutionResult PreviewContent(
        IEnumerable<ContentPackageManifest> availablePackages,
        IEnumerable<string> rootPackageIds) =>
        ContentPackageResolver.Resolve(availablePackages, rootPackageIds);

    public ContentResolutionResult ResolveContent(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        gate.Wait();
        try
        {
            var available = contentRegistry.ExportState().Packages
                .Select(package => package.Manifest)
                .ToArray();
            return ContentPackageResolver.Resolve(available, [packageId]);
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord ProposeContent(ContentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        gate.Wait();
        try
        {
            var record = contentRegistry.Propose(manifest);
            AppendEvent("content_proposed", manifest.PackageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord ValidateContent(
        string packageId,
        ContentResolutionResult resolution)
    {
        gate.Wait();
        try
        {
            var manifest = GetContentManifest(packageId);
            _ = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
            var record = contentRegistry.Validate(packageId, resolution, WorldTick);
            AppendEvent("content_validated", packageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord ApproveContent(string packageId)
    {
        gate.Wait();
        try
        {
            var record = contentRegistry.Approve(packageId, WorldTick);
            AppendEvent("content_approved", packageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord StageContent(string packageId)
    {
        gate.Wait();
        try
        {
            var manifest = GetContentManifest(packageId);
            _ = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
            var record = contentRegistry.Stage(packageId, WorldTick);
            AppendEvent("content_staged", packageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord RollbackContent(string packageId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        gate.Wait();
        try
        {
            var record = contentRegistry.Rollback(packageId, WorldTick, reason);
            worldContent = ContentDefinitionApplicator.RemovePackage(worldContent, record.Manifest.PackageDigest);
            AppendEvent("content_rolled_back", $"{packageId}:{reason.Trim()}");
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Pause()
    {
        gate.Wait();
        try
        {
            var wasPaused = society.Checkpoint.IsPaused;
            var result = society.Pause();
            if (!wasPaused && result.Checkpoint.IsPaused)
            {
                AppendEvent("paused", "owner_request");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void Resume()
    {
        gate.Wait();
        try
        {
            var wasPaused = society.Checkpoint.IsPaused;
            var result = society.Resume();
            if (wasPaused && !result.Checkpoint.IsPaused)
            {
                AppendEvent("resumed", $"epoch:{result.Checkpoint.RunEpoch}");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void Validate()
    {
        SocietyFixture.Validate(society.Checkpoint);
        society.Validate();
        contentRegistry.Validate();
        worldContent.Validate();
        var expectedWorldContent = RebuildWorldContent(contentRegistry.ExportState());
        if (!string.Equals(worldContent.StateDigest, expectedWorldContent.StateDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The private-world typed content does not match active package records.");
        }
        WorldSystemsRules.Validate(worldSystems);
        if (worldSystems.WorldTick != WorldTick ||
            !string.Equals(worldSystems.WorldSeed, worldSeed, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The private-world richer-systems state does not match the authoritative clock or seed.");
        }
        var mapValidation = MapAcceptance.Validate(map);
        if (!mapValidation.IsValid)
        {
            throw new InvalidDataException($"The private-world map is invalid: {mapValidation.Failure}");
        }
        var activeIds = society.Checkpoint.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var physicalIds = inhabitants.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (!activeIds.SequenceEqual(physicalIds))
        {
            throw new InvalidDataException("The private-world physical and society populations disagree.");
        }

        foreach (var inhabitant in inhabitants.Values)
        {
            if (!map.IsPassable(inhabitant.Position) ||
                inhabitant.HungerBasisPoints is < 0 or > 10_000 ||
                inhabitant.EnergyBasisPoints is < 0 or > 10_000 ||
                inhabitant.MoveWaitTicks < 0)
            {
                throw new InvalidDataException($"Physical state for '{inhabitant.InhabitantId}' is invalid.");
            }
        }

        var expectedEventId = 1L;
        var previousTick = 0L;
        foreach (var worldEvent in events)
        {
            if (worldEvent.EventId != expectedEventId ||
                worldEvent.WorldTick < previousTick ||
                worldEvent.WorldTick > WorldTick)
            {
                throw new InvalidDataException("Private-world events are not a committed ordered sequence.");
            }

            expectedEventId++;
            previousTick = worldEvent.WorldTick;
        }
    }

    public void Dispose()
    {
        society.Dispose();
        gate.Dispose();
    }

    private PrivateWorldRuntimeState CaptureState() => new(
        StateSchemaVersion,
        worldSeed,
        map,
        society.ExportState(),
        inhabitants.Values.OrderBy(item => item.InhabitantId, StringComparer.Ordinal).ToArray(),
        resources.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new PlaytestResourceState(item.Key, item.Value)).ToArray(),
        events.ToArray(),
        instructionsByIdempotency.Values
            .OrderBy(item => item.SubmissionSequence)
            .ToArray(),
        completedInstructionIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
        contentRegistry.ExportState(),
        worldSystems,
        worldContent);

    public DeclarativeWorldContentState WorldContent => worldContent;

    private ContentPackageManifest GetContentManifest(string packageId)
    {
        ContentPackageRules.ValidatePackageId(packageId);
        return contentRegistry.ExportState().Packages
            .SingleOrDefault(package => package.Manifest.PackageId == packageId)?.Manifest
            ?? throw new KeyNotFoundException($"Package '{packageId}' has no lifecycle record.");
    }

    private static DeclarativeWorldContentState RebuildWorldContent(ContentRegistryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = new DeclarativeWorldContentState([], []);
        foreach (var package in state.Packages
                     .Where(package => package.Lifecycle == ContentPackageLifecycle.Active)
                     .OrderBy(package => package.ActivationTick ?? long.MaxValue)
                     .ThenBy(package => package.Manifest.PackageId, StringComparer.Ordinal))
        {
            result = ContentDefinitionPayloadCodec.ApplyPackage(result, package.Manifest);
        }

        return result;
    }

    private static WorldSystemsState CreateWorldSystems(string worldSeed, SeededMap map)
    {
        var config = WorldSystemsConfig.Default;
        var resources = map.Resources
            .Select(resource => resource.IsRenewable
                ? new EcologyResource(
                    resource.Id,
                    resource.Kind,
                    resource.Position,
                    true,
                    8,
                    12,
                    2,
                    1,
                    SeasonKind.Spring,
                    1,
                    EcologyResourceState.Available)
                : new EcologyResource(
                    resource.Id,
                    resource.Kind,
                    resource.Position,
                    false,
                    3,
                    3,
                    0,
                    0,
                    SeasonKind.Spring,
                    0,
                    EcologyResourceState.Available))
            .ToArray();
        var culture = new CultureState(
            [new CultureDefinition("camp", "Camp", ["cooperation", "survival"])],
            [
                new CultureAssignment("founder-scout", "camp", ["mapping"]),
                new CultureAssignment("founder-mira", "camp", ["harvest"]),
                new CultureAssignment("founder-rowan", "camp", ["building"]),
                new CultureAssignment("founder-ilya", "camp", ["memory"]),
            ]);
        var factions = new FactionState(
            [new FactionDefinition("camp-alpha", "Camp Alpha", ["camp"])],
            [
                new FactionStanding("founder-scout", "camp-alpha", 0),
                new FactionStanding("founder-mira", "camp-alpha", 0),
                new FactionStanding("founder-rowan", "camp-alpha", 0),
                new FactionStanding("founder-ilya", "camp-alpha", 0),
            ],
            [new LawRule("camp-no-theft", "camp-alpha", LawActionKind.Theft, LawSeverity.Major, 500, 25)],
            []);
        var currency = new CurrencyState(
            [new CurrencyDefinition("copper", "Copper", "cp")],
            [new CurrencyAccount("camp-wallet", HouseholdId, "copper", 100)],
            []);
        var chunk = ChunkManifestCodec.WithDigest(new ChunkManifest(
            new ChunkCoordinate(0, 0),
            ChunkRules.DefaultChunkSize,
            map.Width,
            map.Height,
            SeededMapGenerator.GeneratorId,
            SeededMapGenerator.GeneratorVersion,
            map.Resources.Select(resource => new ChunkResourceMetadata(
                resource.Id,
                resource.Kind,
                resource.Position,
                resource.IsRenewable)).ToArray()));
        return WorldSystemsRules.CreateGenesis(
            worldSeed,
            config,
            resources,
            factions,
            currency,
            culture,
            [chunk]);
    }

    private static WorldSystemsState AdvanceWorldSystemsTo(WorldSystemsState state, long targetTick)
    {
        while (state.WorldTick < targetTick)
        {
            state = WorldSystemsRules.AdvanceOneTick(state);
        }

        return state;
    }

    private void SyncEcologyResourceStates()
    {
        foreach (var resource in worldSystems.Ecology.Resources)
        {
            resources[resource.Id] = resource.State == EcologyResourceState.Depleted
                ? ResourceState.Depleted
                : ResourceState.Available;
        }
    }

    private static SocietyWorldRuntime CreateSociety(
        string worldSeed,
        Func<string, IDecisionProvider>? providerFactory,
        int maxCognitionQueueLength,
        int maxCognitionDispatchPerCycle,
        double minimumCognitionConfidence)
    {
        var config = new SocietyConfig();
        var founders = new[]
        {
            SocietyFixture.CreateFounder("founder-scout", "Scout", "model:scout", config: config),
            SocietyFixture.CreateFounder("founder-mira", "Mira", "model:mira", config: config),
            SocietyFixture.CreateFounder("founder-rowan", "Rowan", "model:rowan", config: config),
            SocietyFixture.CreateFounder("founder-ilya", "Ilya", "model:ilya", config: config),
        };
        var checkpoint = SocietyFixture.CreateGenesis(
            worldSeed,
            founders,
            [
                new InventoryLot(FoodLotId, "food", HouseholdId, 32, 10_000, 10_000, 0),
                new InventoryLot("wood:camp-alpha", "wood", HouseholdId, 48, 10_000, 10_000, 0),
                new InventoryLot("tools:camp-alpha", "tool", HouseholdId, 4, 10_000, 10_000, 0),
            ],
            config,
            "model:world-default");
        checkpoint = SocietyFixture.CreateHousehold(
            checkpoint,
            HouseholdId,
            "Camp Alpha",
            founders.Select(item => item.Id)).Checkpoint;
        checkpoint = SocietyFixture.AssignRole(checkpoint, "founder-scout", SocietyWorkRole.Trader).Checkpoint;
        checkpoint = SocietyFixture.AssignRole(checkpoint, "founder-mira", SocietyWorkRole.Farmer).Checkpoint;
        checkpoint = SocietyFixture.AssignRole(checkpoint, "founder-rowan", SocietyWorkRole.Builder).Checkpoint;
        checkpoint = SocietyFixture.AssignRole(checkpoint, "founder-ilya", SocietyWorkRole.Teacher).Checkpoint;
        return new SocietyWorldRuntime(
            checkpoint,
            providerFactory,
            maxCognitionQueueLength,
            maxCognitionDispatchPerCycle,
            minimumCognitionConfidence);
    }

    private void CreatePhysicalState()
    {
        var startingPositions = new[]
        {
            new GridPoint(0, 0),
            new GridPoint(1, 1),
            new GridPoint(2, 1),
            new GridPoint(3, 1),
        };
        var profiles = new (string Id, string Personality, string Aspiration)[]
        {
            ("founder-scout", "curious", "map the nearby world"),
            ("founder-mira", "practical", "make the camp self-sufficient"),
            ("founder-rowan", "patient", "build something lasting"),
            ("founder-ilya", "observant", "teach and preserve memory"),
        };
        for (var index = 0; index < profiles.Length; index++)
        {
            var profile = profiles[index];
            inhabitants.Add(
                profile.Id,
                new PlaytestInhabitantState(
                    profile.Id,
                    startingPositions[index],
                    6_500,
                    6_500,
                    0,
                    profile.Personality,
                    profile.Aspiration));
        }
    }

    private void DrainNeeds()
    {
        foreach (var state in inhabitants.Values.ToArray())
        {
            inhabitants[state.InhabitantId] = state with
            {
                HungerBasisPoints = Math.Max(0, state.HungerBasisPoints - 80),
                EnergyBasisPoints = Math.Max(0, state.EnergyBasisPoints - 60),
                MoveWaitTicks = checked(state.MoveWaitTicks + 1),
            };
        }
    }

    private void RemoveDeadPhysicalState()
    {
        var activeIds = society.Checkpoint.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in inhabitants.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            inhabitants.Remove(id);
            AppendEvent("inhabitant_removed", id);
        }
    }

    private void EnqueueDueCognition()
    {
        foreach (var inhabitant in society.Checkpoint.Inhabitants
                     .Where(item => item.Status == SocietyInhabitantStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var physical = inhabitants[inhabitant.Id];
            var candidates = CreateCandidates(inhabitant.Id, physical);
            var generation = checked((int)(WorldTick + 1));
            var observation = new InhabitantObservation(
                inhabitant.Id,
                WorldTick,
                society.Checkpoint.RunEpoch,
                generation,
                ObservationDigest(inhabitant.Id, physical, candidates),
                physical.HungerBasisPoints,
                physical.EnergyBasisPoints,
                candidates);
            var accepted = society.EnqueueCognition(new SocietyCognitionScheduleEntry(
                $"tick:{WorldTick}:{inhabitant.Id}",
                inhabitant.Id,
                PriorityFor(physical),
                WorldTick,
                ["routine_tick"],
                observation));
            if (!accepted)
            {
                AppendEvent("cognition_backpressure", inhabitant.Id);
            }
        }
    }

    private void ApplyDecision(SocietyCognitionDispatchResult decision)
    {
        if (!decision.Admission.Accepted || decision.Admission.Intention is null ||
            !inhabitants.TryGetValue(decision.InhabitantId, out var state))
        {
            AppendEvent("cognition_rejected", $"{decision.InhabitantId}:{decision.Admission.Outcome}");
            return;
        }

        var pendingInstruction = PendingInstructionFor(decision.InhabitantId);
        var candidateId = decision.Admission.Intention.CandidateId;
        var forcedCandidate = pendingInstruction?.Kind == OwnerInstructionKind.MustDo
            ? InstructionCandidate(pendingInstruction.Text)
            : null;
        if (forcedCandidate is not null && CreateCandidates(decision.InhabitantId, state)
            .Any(candidate => candidate.Id == forcedCandidate))
        {
            candidateId = forcedCandidate;
        }

        switch (candidateId)
        {
            case "seek_food":
                MoveToward(decision.InhabitantId, state, map.GetResource(BerryResourceId).Position, "food");
                break;
            case "harvest_food":
                HarvestFood(decision.InhabitantId, state);
                break;
            case "consume_food":
                ConsumeFood(decision.InhabitantId, state);
                break;
            case "sleep":
                Sleep(decision.InhabitantId, state);
                break;
            default:
                AppendEvent("inhabitant_idle", decision.InhabitantId);
                break;
        }

        if (pendingInstruction is not null &&
            (pendingInstruction.Kind == OwnerInstructionKind.Suggestive || forcedCandidate is not null))
        {
            completedInstructionIds.Add(pendingInstruction.InstructionId);
            AppendEvent("instruction_applied", $"{pendingInstruction.InstructionId}:{candidateId}");
        }
    }

    private void MoveToward(string inhabitantId, PlaytestInhabitantState state, GridPoint destination, string reason)
    {
        if (state.Position == destination)
        {
            AppendEvent("destination_reached", $"{inhabitantId}:{reason}");
            return;
        }

        var route = DeterministicRouteFinder.Find(map, state.Position, destination);
        if (route.Count < 2)
        {
            AppendEvent("movement_blocked", $"{inhabitantId}:no_route");
            return;
        }

        var next = route[1];
        if (inhabitants.Values.Any(other => other.InhabitantId != inhabitantId && other.Position == next))
        {
            inhabitants[inhabitantId] = state with { MoveWaitTicks = checked(state.MoveWaitTicks + 1) };
            AppendEvent("movement_blocked", $"{inhabitantId}:occupied");
            return;
        }

        inhabitants[inhabitantId] = state with { Position = next, MoveWaitTicks = 0 };
        AppendEvent("inhabitant_moved", $"{inhabitantId}:{state.Position.X},{state.Position.Y}->{next.X},{next.Y}:{reason}");
    }

    private void HarvestFood(string inhabitantId, PlaytestInhabitantState state)
    {
        if (state.Position != map.GetResource(BerryResourceId).Position ||
            resources[BerryResourceId] != ResourceState.Available)
        {
            AppendEvent("harvest_failed", $"{inhabitantId}:not_at_available_food");
            return;
        }

        var lot = society.Checkpoint.Inventory.Lots
            .FirstOrDefault(item => item.Id == FoodLotId && item.OwnerId == HouseholdId && item.Quantity > 0);
        if (lot is null)
        {
            resources[BerryResourceId] = ResourceState.Depleted;
            AppendEvent("harvest_failed", $"{inhabitantId}:food_depleted");
            return;
        }

        society.Apply(checkpoint => SocietyFixture.TransferInventory(
            checkpoint,
            $"harvest:{WorldTick}:{inhabitantId}",
            HouseholdId,
            inhabitantId,
            FoodLotId,
            1,
            "harvested_food"));
        var ecologyResource = worldSystems.Ecology.GetResource(BerryResourceId);
        var harvest = EcologyRules.Harvest(ecologyResource, 1);
        if (harvest.IsValid && harvest.Resource is not null)
        {
            worldSystems = worldSystems with
            {
                Ecology = worldSystems.Ecology with
                {
                    Resources = worldSystems.Ecology.Resources
                        .Select(resource => resource.Id == BerryResourceId ? harvest.Resource : resource)
                        .ToArray(),
                },
            };
            SyncEcologyResourceStates();
        }

        AppendEvent("food_harvested", inhabitantId);
    }

    private void ConsumeFood(string inhabitantId, PlaytestInhabitantState state)
    {
        var lot = society.Checkpoint.Inventory.Lots
            .FirstOrDefault(item => item.OwnerId == inhabitantId && item.ItemKind == "food" && item.Quantity > 0);
        if (lot is null)
        {
            AppendEvent("consumption_failed", $"{inhabitantId}:no_food");
            return;
        }

        society.Apply(checkpoint => SocietyFixture.ConsumeInventory(checkpoint, inhabitantId, lot.Id, 1));
        inhabitants[inhabitantId] = state with { HungerBasisPoints = Math.Min(10_000, state.HungerBasisPoints + 2_500) };
        AppendEvent("food_consumed", inhabitantId);
    }

    private void Sleep(string inhabitantId, PlaytestInhabitantState state)
    {
        var bedroll = map.GetObject("bedroll");
        if (state.Position != bedroll.Position)
        {
            MoveToward(inhabitantId, state, bedroll.Position, "sleep");
            return;
        }

        inhabitants[inhabitantId] = state with { EnergyBasisPoints = Math.Min(10_000, state.EnergyBasisPoints + 2_000) };
        AppendEvent("inhabitant_slept", inhabitantId);
    }

    private List<CognitionCandidate> CreateCandidates(
        string inhabitantId,
        PlaytestInhabitantState state)
    {
        var candidates = new List<CognitionCandidate>();
        var instruction = PendingInstructionFor(inhabitantId);
        var instructionCandidate = instruction is null ? null : InstructionCandidate(instruction.Text);
        if (instructionCandidate == "sleep")
        {
            candidates.Add(new CognitionCandidate("sleep", "Follow the owner's rest instruction.", 0, "bedroll"));
        }

        var hasFood = society.Checkpoint.Inventory.Lots.Any(item =>
            item.OwnerId == inhabitantId && item.ItemKind == "food" && item.Quantity > 0);
        if (hasFood && state.HungerBasisPoints < 8_500)
        {
            candidates.Add(new CognitionCandidate("consume_food", "Eat one carried food item.", 0));
        }

        if (instructionCandidate == "consume_food" && hasFood && !candidates.Any(item => item.Id == "consume_food"))
        {
            candidates.Add(new CognitionCandidate("consume_food", "Follow the owner's food instruction.", 0));
        }

        var berry = map.GetResource(BerryResourceId);
        if (resources[BerryResourceId] == ResourceState.Available && state.Position == berry.Position)
        {
            candidates.Add(new CognitionCandidate("harvest_food", "Gather food at the berry patch.", 0, BerryResourceId));
        }
        else if (resources[BerryResourceId] == ResourceState.Available && state.HungerBasisPoints < 7_000)
        {
            candidates.Add(new CognitionCandidate("seek_food", "Travel to the available berry patch.", 5, BerryResourceId));
        }

        if (instructionCandidate == "seek_food" && resources[BerryResourceId] == ResourceState.Available &&
            !candidates.Any(item => item.Id == "seek_food"))
        {
            candidates.Add(new CognitionCandidate("seek_food", "Follow the owner's travel instruction.", 0, BerryResourceId));
        }

        if (instructionCandidate == "harvest_food" &&
            resources[BerryResourceId] == ResourceState.Available &&
            state.Position == berry.Position &&
            !candidates.Any(item => item.Id == "harvest_food"))
        {
            candidates.Add(new CognitionCandidate("harvest_food", "Follow the owner's harvest instruction.", 0, BerryResourceId));
        }

        if (state.EnergyBasisPoints < 3_500 && !candidates.Any(item => item.Id == "sleep"))
        {
            candidates.Add(new CognitionCandidate("sleep", "Sleep to recover energy.", 10, "bedroll"));
        }

        candidates.Add(new CognitionCandidate("safe_idle", "Continue safely without starting a new task.", 100));
        return candidates;
    }

    private static int PriorityFor(PlaytestInhabitantState state) =>
        state.HungerBasisPoints < 2_500 || state.EnergyBasisPoints < 1_500 ? 20 : 0;

    private OwnerQueuedInstruction? PendingInstructionFor(string inhabitantId) =>
        instructionsByIdempotency.Values
            .Where(item => item.TargetInhabitantId == inhabitantId &&
                !completedInstructionIds.Contains(item.InstructionId))
            .OrderBy(item => item.SubmissionSequence)
            .FirstOrDefault();

    private static string? InstructionCandidate(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (normalized.Contains("sleep") || normalized.Contains("rest"))
        {
            return "sleep";
        }

        if (normalized.Contains("harvest") || normalized.Contains("gather") || normalized.Contains("berry"))
        {
            return normalized.Contains("harvest") || normalized.Contains("gather")
                ? "harvest_food"
                : "seek_food";
        }

        if (normalized.Contains("eat") || normalized.Contains("food") || normalized.Contains("hungry"))
        {
            return "consume_food";
        }

        if (normalized.Contains("go") || normalized.Contains("travel") || normalized.Contains("move"))
        {
            return "seek_food";
        }

        return null;
    }

    private static bool Matches(OwnerQueuedInstruction existing, OwnerInstructionRequest request) =>
        existing.IssuerId == request.IssuerId.Trim() &&
        existing.TargetInhabitantId == request.TargetInhabitantId.Trim() &&
        existing.Kind == request.Kind &&
        existing.Text == request.Text.Trim();

    private static void ValidateInstructionRequest(OwnerInstructionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IssuerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetInhabitantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Text);
    }

    private static string ToWireValue(OwnerInstructionKind kind) => kind switch
    {
        OwnerInstructionKind.Suggestive => "suggestive",
        OwnerInstructionKind.MustDo => "must_do",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ObservationDigest(
        string inhabitantId,
        PlaytestInhabitantState state,
        IReadOnlyList<CognitionCandidate> candidates)
    {
        var text = new StringBuilder()
            .Append("agentworld.private-world-observation/v1|")
            .Append(inhabitantId).Append('|')
            .Append(state.Position.X).Append(',').Append(state.Position.Y).Append('|')
            .Append(state.HungerBasisPoints).Append('|').Append(state.EnergyBasisPoints).Append('|')
            .Append(string.Join(',', candidates.Select(candidate => candidate.Id)))
            .ToString();
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}";
    }

    private void AppendEvent(string kind, string detail)
    {
        events.Add(new PlaytestWorldEvent(nextEventId++, WorldTick, kind, detail));
    }

    internal static void ValidateStateForCodec(PrivateWorldRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion is not (1 or StateSchemaVersion) || string.IsNullOrWhiteSpace(state.WorldSeed))
        {
            throw new InvalidDataException("The private-world runtime state schema or seed is invalid.");
        }

        if (!MapAcceptance.Validate(state.Map).IsValid)
        {
            throw new InvalidDataException("The private-world runtime contains an invalid map.");
        }

        using var society = SocietyWorldRuntime.Restore(state.Society);
        ContentPackageRegistry.Restore(state.Content);
        if (state.SchemaVersion >= StateSchemaVersion && state.Content is null)
        {
            throw new InvalidDataException("The current private-world schema requires content governance state.");
        }

        if (state.WorldSystems is not null)
        {
            WorldSystemsRules.Validate(state.WorldSystems);
            if (state.WorldSystems.WorldTick != state.Society.Society.WorldTick ||
                !string.Equals(state.WorldSystems.WorldSeed, state.WorldSeed, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The saved richer-systems state does not match the saved society clock or seed.");
            }
        }
        else if (state.SchemaVersion >= StateSchemaVersion)
        {
            throw new InvalidDataException("The current private-world schema requires richer-systems state.");
        }

        state.WorldContent?.Validate();
        var activeIds = state.Society.Society.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal);
        var physicalIds = state.Inhabitants
            .Select(item => item.InhabitantId)
            .OrderBy(item => item, StringComparer.Ordinal);
        if (!activeIds.SequenceEqual(physicalIds))
        {
            throw new InvalidDataException("The saved private-world populations disagree.");
        }
    }

    private static string NormalizeRequiredText(string? value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}

public static class PrivateWorldRuntimeCodec
{
    private const string Header = "agentworld.private-world-runtime/v1";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static byte[] Encode(PrivateWorldRuntimeState state)
    {
        PrivateWorldRuntime.ValidateStateForCodec(state);
        return JsonSerializer.SerializeToUtf8Bytes(new RuntimeDocument(Header, state), Options);
    }

    public static PrivateWorldRuntimeState Decode(ReadOnlyMemory<byte> bytes)
    {
        var document = JsonSerializer.Deserialize<RuntimeDocument>(bytes.Span, Options)
            ?? throw new InvalidDataException("The private-world runtime checkpoint is empty.");
        if (!string.Equals(document.Format, Header, StringComparison.Ordinal) || document.State is null)
        {
            throw new InvalidDataException("The private-world runtime checkpoint format is unsupported.");
        }

        PrivateWorldRuntime.ValidateStateForCodec(document.State);
        return document.State;
    }

    private sealed record RuntimeDocument(string Format, PrivateWorldRuntimeState State);
}
