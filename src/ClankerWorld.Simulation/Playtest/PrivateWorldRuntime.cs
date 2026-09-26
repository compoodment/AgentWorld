using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed record PlaytestInhabitantState(
    string InhabitantId,
    GridPoint Position,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    int MoveWaitTicks,
    string Personality,
    string Aspiration,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LastDecisionContext = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementProject? Project = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SurvivalCondition? Survival = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementLesson? Lesson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementParenthood? Parenthood = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementProficiency? Proficiency = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SettlementSocialStanding>? SocialStanding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PlaytestPrivateThought>? RecentThoughts = null);

public sealed record PlaytestPrivateThought(long WorldTick, string Text);

public sealed record PlaytestResourceState(string ResourceId, ResourceState State);

public sealed record PlaytestDeceasedInhabitantState(
    string InhabitantId,
    long DeathTick,
    int AgeAtDeath,
    PlaytestInhabitantState LastPhysical);

public sealed record PlaytestWorldEvent(
    long EventId,
    long WorldTick,
    string Kind,
    string Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? Position = null);

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
    DeclarativeWorldContentState? WorldContent = null,
    WorldContentSimulationState? WorldSimulation = null,
    WorldAssetReservationLedgerState? AssetReservations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] long EventHistoryFloor = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HistoryArchiveHead = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementSurvivalState? Survival = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementCouncil? Council = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PlaytestDeceasedInhabitantState>? DeceasedInhabitants = null);

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
public sealed partial class PrivateWorldRuntime : IDisposable
{
    public const int StateSchemaVersion = 14;
    private const int MaximumRecentThoughts = 8;
    private const string HouseholdId = "household:camp-alpha";
    private const string FoodLotId = "food:camp-alpha";
    private const string BerryResourceId = "berry-patch";
    private const long CognitionReevaluationIntervalTicks = 30;
    private const int ResourceInteractionRange = 1;
    private const int HarvestFoodYield = 4;

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SemaphoreSlim tickGate = new(1, 1);
    private readonly string worldSeed;
    private readonly Func<string, IDecisionProvider>? providerFactory;
    private readonly double minimumCognitionConfidence;
    private readonly int maxCognitionDispatchPerCycle;
    private SeededMap map;
    private SocietyWorldRuntime society;
    private ContentPackageRegistry contentRegistry;
    private WorldSystemsState worldSystems;
    private DeclarativeWorldContentState worldContent;
    private WorldContentSimulationState worldSimulation;
    private WorldAssetReservationLedger assetReservations;
    private Dictionary<string, PlaytestInhabitantState> inhabitants = new(StringComparer.Ordinal);
    private Dictionary<string, PlaytestDeceasedInhabitantState> deceasedInhabitants = new(StringComparer.Ordinal);
    private Dictionary<string, ResourceState> resources = new(StringComparer.Ordinal);
    private Dictionary<string, OwnerQueuedInstruction> instructionsByIdempotency =
        new(StringComparer.Ordinal);
    private Dictionary<string, OwnerInstructionReceipt> instructionReceipts =
        new(StringComparer.Ordinal);
    private HashSet<string> completedInstructionIds = new(StringComparer.Ordinal);
    private List<PlaytestWorldEvent> events = [];
    private long nextEventId = 1;
    private long eventHistoryFloor;
    private string? historyArchiveHead;
    private int checkpointSchemaVersion = StateSchemaVersion;
    private long nextInstructionSequence = 1;
    private readonly Dictionary<string, PendingHostedDecision> pendingHosted = new(StringComparer.Ordinal);

    private sealed record HostedDecisionOutcome(CognitionDecisionResponse? Response, string? Failure);
    private sealed record PendingHostedDecision(
        CognitionDecisionRequest Request,
        Task<HostedDecisionOutcome> Task,
        CancellationTokenSource Cancellation);

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
        this.maxCognitionDispatchPerCycle = maxCognitionDispatchPerCycle;
        contentRegistry = new ContentPackageRegistry();
        worldContent = new DeclarativeWorldContentState([], []);
        worldSimulation = WorldContentSimulationState.Empty;
        assetReservations = new WorldAssetReservationLedger();
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

    public WorldContentSimulationState WorldSimulation => worldSimulation;

    public WorldAssetReservationLedgerState AssetReservations => assetReservations.ExportState();

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
        if (!IsCompatibleSavedMap(runtime.map, state))
        {
            runtime.Dispose();
            throw new InvalidDataException("The private-world map does not match deterministic regeneration.");
        }

        runtime.map = state.Map;
        runtime.eventHistoryFloor = state.EventHistoryFloor;
        runtime.historyArchiveHead = state.HistoryArchiveHead;
        runtime.checkpointSchemaVersion = Math.Max(3, state.SchemaVersion);
        runtime.society.Dispose();
        runtime.society = SocietyWorldRuntime.Restore(
            state.Society,
            providerFactory,
            minimumCognitionConfidence);
        runtime.contentRegistry = ContentPackageRegistry.Restore(state.Content);
        runtime.worldContent = state.WorldContent ?? RebuildWorldContent(runtime.contentRegistry.ExportState());
        runtime.worldSimulation = state.WorldSimulation is null
            ? WorldContentSimulationState.Empty
            : state.WorldSimulation with { CropBuilds = state.WorldSimulation.CropBuilds ?? [] };
        runtime.assetReservations = WorldAssetReservationLedger.Restore(state.AssetReservations);
        runtime.survivalState = state.Survival;
        runtime.council = state.Council;
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
        runtime.deceasedInhabitants.Clear();
        foreach (var inhabitant in state.DeceasedInhabitants ?? [])
        {
            runtime.deceasedInhabitants.Add(inhabitant.InhabitantId, inhabitant);
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
        runtime.nextEventId = runtime.events.Count == 0 ? checked(runtime.eventHistoryFloor + 1) : checked(runtime.events[^1].EventId + 1);
        runtime.Validate();
        return runtime;
    }

    public ValueTask<PrivateWorldStepResult> AdvanceOneTickAsync(CancellationToken cancellationToken = default) =>
        AdvanceOneTickAsync(null, cancellationToken);

    public async ValueTask<PrivateWorldStepResult> AdvanceOneTickAsync(
        Func<bool>? commitPermitted, CancellationToken cancellationToken = default) =>
        await AdvanceOneTickCoreAsync(false, commitPermitted, cancellationToken).ConfigureAwait(false);

    /// <summary>Playable-host path: hosted decisions run between ticks, never inside a tick transaction.</summary>
    public ValueTask<PrivateWorldStepResult> AdvanceOneTickNonBlockingAsync(
        Func<bool>? commitPermitted = null, CancellationToken cancellationToken = default) =>
        AdvanceOneTickCoreAsync(true, commitPermitted, cancellationToken);

    private async ValueTask<PrivateWorldStepResult> AdvanceOneTickCoreAsync(
        bool deferHosted, Func<bool>? commitPermitted, CancellationToken cancellationToken)
    {
        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PrivateWorldRuntimeState baseline;
            long baselineEventId;
            PendingHostedDecision[] completed = [];
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (society.Checkpoint.IsPaused)
                {
                    return new PrivateWorldStepResult(false, "paused", WorldTick, [], []);
                }
                if (deferHosted)
                {
                    foreach (var (id, pending) in pendingHosted.ToArray())
                    {
                        if (!society.Checkpoint.Inhabitants.Any(person => person.Id == id && person.Status == SocietyInhabitantStatus.Active) ||
                            society.CurrentProviderEpoch(id) != pending.Request.ProviderEpoch ||
                            society.Checkpoint.RunEpoch != pending.Request.Observation.RunEpoch)
                        {
                            CancelPendingHosted(id);
                        }
                    }
                    completed = pendingHosted.Values.Where(item => item.Task.IsCompleted).ToArray();
                }
                baseline = CaptureState();
                baselineEventId = nextEventId;
            }
            finally
            {
                gate.Release();
            }

            // World mutations operate on an isolated proposed tick. In the
            // playable path, external cognition itself runs between ticks.
            // Readers and owner controls use the last committed world.
            using var proposed = Restore(baseline, providerFactory, maxCognitionDispatchPerCycle, minimumCognitionConfidence);
            var result = await proposed.AdvancePreparedTickAsync(deferHosted, completed, cancellationToken).ConfigureAwait(false);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (commitPermitted is not null && !commitPermitted())
                {
                    return new PrivateWorldStepResult(false, "waiting_for_client", WorldTick, [], []);
                }
                if (nextEventId != baselineEventId || WorldTick != baseline.Society.Society.WorldTick || historyArchiveHead != baseline.HistoryArchiveHead)
                {
                    return new PrivateWorldStepResult(false, "tick_superseded_by_owner_change", WorldTick, [], []);
                }
                if (!result.Advanced)
                {
                    return result;
                }
                CommitPreparedTick(proposed);
                if (deferHosted)
                {
                    foreach (var item in completed)
                    {
                        pendingHosted.Remove(item.Request.Observation.InhabitantId);
                        item.Cancellation.Dispose();
                    }
                    if (commitPermitted is null || commitPermitted()) StartHostedDecisions();
                }
                return result with { Events = events.Where(item => item.EventId >= baselineEventId).ToArray() };
            }
            finally
            {
                gate.Release();
            }
        }
        finally
        {
            tickGate.Release();
        }
    }

    private void StartHostedDecisions()
    {
        var capacity = Math.Max(0, maxCognitionDispatchPerCycle - pendingHosted.Count);
        if (capacity == 0) return;
        foreach (var preview in society.PreviewHostedRequests(pendingHosted.Keys.ToHashSet(StringComparer.Ordinal)).Take(capacity))
        {
            var cancellation = new CancellationTokenSource();
            var task = Task.Run(async () =>
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        return new HostedDecisionOutcome(
                            await preview.DecideAsync(cancellation.Token).ConfigureAwait(false), null);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        return new HostedDecisionOutcome(null, "provider_cancelled");
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        if (attempt == 1)
                            return new HostedDecisionOutcome(null, $"provider_failure:{exception.GetType().Name}");
                    }
                }
                return new HostedDecisionOutcome(null, "provider_failure:retry_exhausted");
            });
            pendingHosted.Add(preview.InhabitantId, new PendingHostedDecision(preview.Request, task, cancellation));
            AppendEvent("hosted_decision_started", preview.InhabitantId);
        }
    }

    private void CancelPendingHosted(string inhabitantId)
    {
        if (!pendingHosted.Remove(inhabitantId, out var pending)) return;
        pending.Cancellation.Cancel();
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
    }

    public void CancelPendingHostedDecisions()
    {
        gate.Wait();
        try
        {
            foreach (var id in pendingHosted.Keys.ToArray()) CancelPendingHosted(id);
        }
        finally { gate.Release(); }
    }

    private void CommitPreparedTick(PrivateWorldRuntime proposed)
    {
        // Transfer the committed society; disposing the proposal retires the old one.
        (society, proposed.society) = (proposed.society, society);
        map = proposed.map;
        contentRegistry = proposed.contentRegistry;
        worldSystems = proposed.worldSystems;
        survivalState = proposed.survivalState;
        council = proposed.council;
        worldContent = proposed.worldContent;
        worldSimulation = proposed.worldSimulation;
        assetReservations = proposed.assetReservations;
        inhabitants = proposed.inhabitants;
        deceasedInhabitants = proposed.deceasedInhabitants;
        resources = proposed.resources;
        instructionsByIdempotency = proposed.instructionsByIdempotency;
        instructionReceipts = proposed.instructionReceipts;
        completedInstructionIds = proposed.completedInstructionIds;
        events = proposed.events;
        nextEventId = proposed.nextEventId;
        eventHistoryFloor = proposed.eventHistoryFloor;
        historyArchiveHead = proposed.historyArchiveHead;
        checkpointSchemaVersion = proposed.checkpointSchemaVersion;
        nextInstructionSequence = proposed.nextInstructionSequence;
    }

    private async ValueTask<PrivateWorldStepResult> AdvancePreparedTickAsync(
        bool deferHosted, IReadOnlyList<PendingHostedDecision> completed,
        CancellationToken cancellationToken)
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
            StageSettlementContent();
            StageForestryContent();
            var readyPackages = contentRegistry.GetActivationCandidates(targetTick);
            var reservationPreview = WorldAssetReservationLedger.Restore(
                assetReservations.ExportState(),
                assetReservations.Policy);
            foreach (var package in readyPackages)
            {
                var reservation = reservationPreview.TryReservePackage(
                    package.Manifest.PackageId,
                    package.Manifest.AssetReservations ?? [],
                    targetTick);
                if (!reservation.IsSuccess)
                {
                    return new PrivateWorldStepResult(
                        false,
                        $"asset_reservation_rejected:{reservation.FailureCode ?? "invalid"}",
                        WorldTick,
                        [],
                        []);
                }
            }

            assetReservations = reservationPreview;
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
                if (activated.Manifest.PackageId == SettlementContent.PackageId)
                {
                    AddSettlementResources();
                }
                if (activated.Manifest.Definitions.Any(definition => !string.IsNullOrWhiteSpace(definition.PayloadJson)))
                {
                    AppendEvent(
                        "content_definitions_activated",
                        $"{activated.Manifest.PackageId}:buildings={activatedWorldContent.Buildings.Count}:recipes={activatedWorldContent.Recipes.Count}");
                }
            }
            worldContent = activatedWorldContent;
            CancelUnavailableWorkers();
            ProcessProduction(targetTick);
            ProcessCropBuilds(targetTick);

            AdvanceSettlementSurvival();
            MaintainSettlementTrades();
            DrainNeeds();
            RemoveDeadPhysicalState();
            AdvanceSettlementCouncil();
            MaintainLessons();
            MaintainPartnerships();
            MaintainParenthood();
            MaintainDependentCare();
            EnqueueDueCognition();
            var deferredDecisions = new List<SocietyCognitionDispatchResult>();
            if (deferHosted)
            {
                foreach (var item in completed)
                {
                    var id = item.Request.Observation.InhabitantId;
                    if (!inhabitants.TryGetValue(id, out var physical)) continue;
                    var outcome = await item.Task.ConfigureAwait(false);
                    var legal = CreateCandidates(id, physical).Select(candidate => candidate.Id)
                        .ToHashSet(StringComparer.Ordinal);
                    var decision = society.CompleteDeferredCognition(item.Request, outcome.Response,
                        outcome.Failure, legal);
                    if (decision is not null)
                    {
                        if (decision.Admission.Accepted && !decision.Admission.FellBack &&
                            outcome.Response is
                            {
                                Provider: DecisionProviderKind.LargeLanguageModel,
                                PrivateThought: { } thought
                            } && inhabitants.TryGetValue(id, out var thinking))
                        {
                            inhabitants[id] = thinking with
                            {
                                RecentThoughts = (thinking.RecentThoughts ?? [])
                                    .Append(new PlaytestPrivateThought(targetTick, thought))
                                    .TakeLast(MaximumRecentThoughts).ToArray(),
                            };
                            checkpointSchemaVersion = StateSchemaVersion;
                        }
                        deferredDecisions.Add(decision);
                        AppendEvent("hosted_decision_completed", $"{id}:{decision.Admission.Outcome}");
                    }
                    else AppendEvent("hosted_decision_discarded", id);
                }
            }
            var dispatch = deferHosted
                ? await society.DispatchDeterministicCognitionAsync(cancellationToken).ConfigureAwait(false)
                : await society.DispatchCognitionAsync(cancellationToken).ConfigureAwait(false);
            var decisions = deferredDecisions.Concat(dispatch.Decisions)
                .OrderBy(item => item.InhabitantId, StringComparer.Ordinal).ToArray();
            foreach (var decision in decisions)
            {
                ApplyDecision(decision);
            }
            var waiting = deferHosted ? society.PendingHostedInhabitantIds() : new HashSet<string>(StringComparer.Ordinal);
            ApplyContinuingIntentions(decisions.Select(item => item.InhabitantId).Concat(waiting));
            if (deferHosted) ApplySafeRoutinesWhileWaiting(waiting);

            AppendEvent("tick_advanced", targetTick.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var newEvents = events.Skip(startingEvent).ToArray();
            return new PrivateWorldStepResult(true, "advanced", targetTick, decisions, newEvents);
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
            if (target.AgeBand == SocietyAgeBand.Infant)
            {
                throw new ArgumentException("Infants cannot carry out owner instructions; direct care through an adult caregiver.", nameof(request));
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

    public static ContentPreviewResult PreviewWorldContent(
        IEnumerable<ContentPackageManifest> availablePackages,
        IEnumerable<string> rootPackageIds,
        DeclarativeWorldContentState? baseWorldContent = null) =>
        ContentPackagePreview.Run(availablePackages, rootPackageIds, baseWorldContent);

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
            var record = contentRegistry.Propose(manifest, WorldTick);
            AppendEvent("content_proposed", manifest.PackageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool StageStarterContent()
    {
        gate.Wait();
        try
        {
            if (society.Checkpoint.IsPaused || contentRegistry.ExportState().Packages
                .Any(package => package.Manifest.PackageId == StarterContent.PackageId))
            {
                // Never undo an owner's rollback or quarantine, or mutate a paused save.
                return false;
            }

            var manifest = StarterContent.Create();
            _ = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
            var resolution = ContentPackageResolver.Resolve([manifest], [manifest.PackageId]);
            contentRegistry.Propose(manifest, WorldTick);
            contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
            contentRegistry.Approve(manifest.PackageId, WorldTick);
            contentRegistry.Stage(manifest.PackageId, WorldTick);
            AppendEvent("starter_content_staged", manifest.PackageId);
            return true;
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

    public BuildingPlacementResult PlaceBuilding(
        string instanceId,
        string definitionId,
        GridPoint position)
    {
        gate.Wait();
        try
        {
            return PlaceBuildingCore(instanceId, definitionId, position, "building_placed");
        }
        finally
        {
            gate.Release();
        }
    }

    private BuildingPlacementResult PlaceBuildingCore(
        string instanceId,
        string definitionId,
        GridPoint position,
        string eventKind)
    {
        try
        {
            var normalizedInstanceId = NormalizeRequiredText(instanceId, nameof(instanceId));
            var normalizedDefinitionId = NormalizeRequiredText(definitionId, nameof(definitionId));
            ContentPackageRules.ValidateLocalId(normalizedInstanceId);
            var definition = worldContent.Buildings.SingleOrDefault(item => item.CanonicalId == normalizedDefinitionId);
            if (definition is null)
            {
                return BuildingPlacementResult.Rejected(
                    normalizedInstanceId,
                    normalizedDefinitionId,
                    position,
                    $"Building definition '{normalizedDefinitionId}' is not active.");
            }

            if (worldSimulation.Buildings.Any(item => item.InstanceId == normalizedInstanceId))
            {
                return BuildingPlacementResult.Rejected(
                    normalizedInstanceId,
                    normalizedDefinitionId,
                    position,
                    $"Building instance '{normalizedInstanceId}' already exists.");
            }

            if (!CanPlaceBuilding(definition, position, out var placementFailure))
            {
                return BuildingPlacementResult.Rejected(
                    normalizedInstanceId,
                    normalizedDefinitionId,
                    position,
                    placementFailure);
            }

            ApplyInventoryTransition(inventory => ConsumeQuantities(
                inventory,
                definition.BuildCosts,
                $"building:{normalizedInstanceId}"));
            var placed = new PlacedBuilding(
                normalizedInstanceId,
                definition.CanonicalId,
                position,
                WorldTick);
            worldSimulation = new WorldContentSimulationState(
                worldSimulation.Buildings
                    .Append(placed)
                    .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                    .ToArray(),
                worldSimulation.ProductionJobs,
                worldSimulation.NextProductionJobSequence,
                worldSimulation.CropBuilds);
            AppendEvent(eventKind, $"{placed.InstanceId}:{placed.DefinitionId}:{position.X},{position.Y}");
            return BuildingPlacementResult.Success(placed);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return BuildingPlacementResult.Rejected(
                instanceId?.Trim() ?? string.Empty,
                definitionId?.Trim() ?? string.Empty,
                position,
                exception.Message);
        }
    }

    public ProductionStartResult StartProduction(
        string recipeId,
        string buildingInstanceId,
        string workerId)
    {
        gate.Wait();
        try
        {
            return StartProductionCore(recipeId, buildingInstanceId, workerId, "recipe_started");
        }
        finally
        {
            gate.Release();
        }
    }

    private ProductionStartResult StartProductionCore(
        string recipeId,
        string buildingInstanceId,
        string workerId,
        string eventKind)
    {
        try
        {
            var normalizedRecipeId = NormalizeRequiredText(recipeId, nameof(recipeId));
            var normalizedBuildingId = NormalizeRequiredText(buildingInstanceId, nameof(buildingInstanceId));
            var normalizedWorkerId = NormalizeRequiredText(workerId, nameof(workerId));
            var recipe = worldContent.Recipes.SingleOrDefault(item => item.CanonicalId == normalizedRecipeId);
            if (recipe is null)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId, "The recipe is not active.");
            }

            GridPoint workPosition;
            var isFertileLandBuild = recipe.IsCrop && recipe.WorkstationBuildingId is null;
            PlacedBuilding? placed = null;
            if (isFertileLandBuild)
            {
                if (!WorldBuildSiteRules.TryGetFertileLandPosition(normalizedBuildingId, out workPosition) ||
                    !WorldContentSimulationRules.IsFertileLandPosition(map, workPosition))
                {
                    return ProductionStartResult.Rejected(normalizedRecipeId, "The crop must use a generated fertile-land site.");
                }

                if ((worldSimulation.CropBuilds ?? []).Any(job =>
                        job.State == WorldProductionJobState.Running &&
                        job.BuildingInstanceId == normalizedBuildingId))
                {
                    return ProductionStartResult.Rejected(normalizedRecipeId, "The fertile-land site is already being used.");
                }
            }
            else
            {
                placed = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == normalizedBuildingId);
                if (placed is null)
                {
                    return ProductionStartResult.Rejected(normalizedRecipeId, "The workstation building is not placed.");
                }

                if (recipe.WorkstationBuildingId is not null && recipe.WorkstationBuildingId != placed.DefinitionId)
                {
                    return ProductionStartResult.Rejected(normalizedRecipeId, "The placed building is not a valid workstation for this recipe.");
                }

                var buildingDefinition = worldContent.Buildings.Single(item => item.CanonicalId == placed.DefinitionId);
                var activeJobs = worldSimulation.ProductionJobs.Count(item =>
                    item.BuildingInstanceId == placed.InstanceId && item.State == WorldProductionJobState.Running);
                if (activeJobs >= buildingDefinition.Capacity)
                {
                    return ProductionStartResult.Rejected(normalizedRecipeId, "The workstation has no free production capacity.");
                }

                workPosition = placed.Position;
            }

            var worker = society.Checkpoint.Inhabitants.SingleOrDefault(item => item.Id == normalizedWorkerId);
            if (worker is null || worker.Status != SocietyInhabitantStatus.Active)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId, "The production worker is not active.");
            }

            if (!inhabitants.TryGetValue(normalizedWorkerId, out var physical) || physical.Position != workPosition)
            {
                return ProductionStartResult.Rejected(normalizedRecipeId, "The worker must be standing at the build site.");
            }

            var jobId = $"production-{worldSimulation.NextProductionJobSequence.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)}";
            var completionTick = checked(WorldTick + recipe.DurationTicks);
            IReadOnlyList<string> reservationIds = [];
            ApplyInventoryTransition(inventory =>
            {
                var reserved = ReserveQuantities(
                    inventory,
                    recipe.Inputs,
                    $"{jobId}:input",
                    completionTick,
                    out reservationIds);
                return reserved;
            });

            var job = new WorldProductionJob(
                jobId,
                recipe.CanonicalId,
                normalizedBuildingId,
                normalizedWorkerId,
                WorldTick,
                completionTick,
                WorldProductionJobState.Running,
                reservationIds.ToArray());
            var productionJobs = isFertileLandBuild
                ? worldSimulation.ProductionJobs
                : worldSimulation.ProductionJobs.Append(job).OrderBy(item => item.JobId, StringComparer.Ordinal).ToArray();
            var cropBuilds = isFertileLandBuild
                ? (worldSimulation.CropBuilds ?? []).Append(job).OrderBy(item => item.JobId, StringComparer.Ordinal).ToArray()
                : worldSimulation.CropBuilds;
            worldSimulation = new WorldContentSimulationState(
                worldSimulation.Buildings,
                productionJobs,
                checked(worldSimulation.NextProductionJobSequence + 1),
                cropBuilds);
            AppendEvent(eventKind == "recipe_started" && isFertileLandBuild ? "build_started" : eventKind,
                $"{job.JobId}:{job.RecipeId}:{job.BuildingInstanceId}");
            return ProductionStartResult.Success(job);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ProductionStartResult.Rejected(recipeId?.Trim() ?? string.Empty, exception.Message);
        }
    }

    public ContentPackageRecord RollbackContent(string packageId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        gate.Wait();
        try
        {
            var manifest = GetContentManifest(packageId);
            var remainingSimulation = WorldContentSimulationRules.RemovePackage(worldSimulation, manifest.PackageDigest);
            if (inhabitants.Values.Any(person => person.Project is { } project &&
                (project.CandidateId.StartsWith($"build:building:{manifest.PackageDigest}/", StringComparison.Ordinal) ||
                 project.CandidateId.StartsWith($"build:recipe:{manifest.PackageDigest}/", StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("Content referenced by settlement projects requires an explicit migration before removal.");
            }
            var record = contentRegistry.Rollback(packageId, WorldTick, reason);
            worldContent = ContentDefinitionApplicator.RemovePackage(worldContent, record.Manifest.PackageDigest);
            worldSimulation = remainingSimulation;
            if (survivalState is not null)
            {
                survivalState = survivalState with
                {
                    Fires = survivalState.Fires.Where(fire =>
                    worldSimulation.Buildings.Any(building => building.InstanceId == fire.BuildingId)).ToArray()
                };
            }
            assetReservations.ReleasePackage(packageId, WorldTick);
            AppendEvent("content_rolled_back", $"{packageId}:{reason.Trim()}");
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool SetLifePace(int rate)
    {
        gate.Wait();
        try
        {
            var before = society.Checkpoint.LifeClock;
            society.Apply(checkpoint => SocietyFixture.SetLifePace(checkpoint, rate));
            if (before == society.Checkpoint.LifeClock) return false;
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent("life_pace_changed", rate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
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
                foreach (var id in pendingHosted.Keys.ToArray()) CancelPendingHosted(id);
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
        assetReservations.Validate();
        ValidateAssetReservationsAgainstActivePackages();
        WorldContentSimulationRules.Validate(worldSimulation, worldContent, map, WorldTick);
        var inventoryReservationIds = society.Checkpoint.Inventory.Reservations
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var job in worldSimulation.ProductionJobs
                     .Concat(worldSimulation.CropBuilds ?? [])
                     .Where(item => item.State == WorldProductionJobState.Running))
        {
            if (job.InputReservationIds.Any(id => !inventoryReservationIds.Contains(id)))
            {
                throw new InvalidDataException($"Production job '{job.JobId}' has a missing inventory reservation.");
            }
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
        ValidateDeceasedArchive(deceasedInhabitants.Values, society.Checkpoint, map, checkpointSchemaVersion);

        foreach (var inhabitant in inhabitants.Values)
        {
            ValidateProficiency(inhabitant, checkpointSchemaVersion);
            ValidateSocialStanding(inhabitant, society.Checkpoint.Inhabitants.Select(item => item.Id), checkpointSchemaVersion, WorldTick);
            ValidatePrivateThoughts(inhabitant.RecentThoughts, checkpointSchemaVersion, WorldTick);
            if (inhabitant.Project is { } project)
            {
                ValidateProject(project, WorldTick);
                if (checkpointSchemaVersion < 5)
                {
                    throw new InvalidDataException("Persistent projects require private-world schema 5.");
                }
            }
            if (!map.IsPassable(inhabitant.Position) ||
                inhabitant.HungerBasisPoints is < 0 or > 10_000 ||
                inhabitant.EnergyBasisPoints is < 0 or > 10_000 ||
                inhabitant.MoveWaitTicks < 0)
            {
                throw new InvalidDataException($"Physical state for '{inhabitant.InhabitantId}' is invalid.");
            }
        }

        var expectedEventId = checked(eventHistoryFloor + 1);
        var previousTick = 0L;
        foreach (var worldEvent in events)
        {
            if (worldEvent.EventId != expectedEventId ||
                worldEvent.WorldTick < previousTick ||
                worldEvent.WorldTick > WorldTick ||
                worldEvent.Position is { } eventPosition && !map.Contains(eventPosition))
            {
                throw new InvalidDataException("Private-world events are not a committed ordered sequence.");
            }

            expectedEventId++;
            previousTick = worldEvent.WorldTick;
        }
    }

    public void Dispose()
    {
        foreach (var id in pendingHosted.Keys.ToArray()) CancelPendingHosted(id);
        society.Dispose();
        gate.Dispose();
        tickGate.Dispose();
    }

    public void PersistCheckpoint(Func<PrivateWorldRuntimeState, PrivateWorldRuntimeState> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        gate.Wait();
        try
        {
            var saved = persist(CaptureState());
            if (saved.HistoryArchiveHead != historyArchiveHead)
            {
                using var compacted = Restore(saved, providerFactory, maxCognitionDispatchPerCycle, minimumCognitionConfidence);
                CommitPreparedTick(compacted);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private PrivateWorldRuntimeState CaptureState() => new(
        checkpointSchemaVersion,
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
        worldContent,
        worldSimulation,
        assetReservations.ExportState(), eventHistoryFloor, historyArchiveHead, survivalState, council,
        deceasedInhabitants.Count == 0 ? null : deceasedInhabitants.Values.OrderBy(item => item.InhabitantId, StringComparer.Ordinal).ToArray());

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
        var active = state.Packages.Where(package => package.Lifecycle == ContentPackageLifecycle.Active)
            .ToDictionary(package => package.Manifest.PackageId, StringComparer.Ordinal);
        var resolution = ContentPackageResolver.Resolve(active.Values.Select(package => package.Manifest), active.Keys);
        if (!resolution.IsSuccess)
        {
            throw new InvalidDataException("Active world content has an invalid dependency graph.");
        }
        foreach (var entry in resolution.Lock)
        {
            result = ContentDefinitionPayloadCodec.ApplyPackage(result, active[entry.PackageId].Manifest);
        }

        return result;
    }

    private void ValidateAssetReservationsAgainstActivePackages()
    {
        var expected = new WorldAssetReservationLedger(assetReservations.Policy);
        foreach (var package in contentRegistry.ExportState().Packages
                     .Where(item => item.Lifecycle == ContentPackageLifecycle.Active)
                     .OrderBy(item => item.ActivationTick ?? long.MaxValue)
                     .ThenBy(item => item.Manifest.PackageId, StringComparer.Ordinal))
        {
            var result = expected.TryReservePackage(
                package.Manifest.PackageId,
                package.Manifest.AssetReservations ?? [],
                package.ActivationTick ?? WorldTick);
            if (!result.IsSuccess)
            {
                throw new InvalidDataException(
                    $"Active package '{package.Manifest.PackageId}' cannot be reconstructed in the world asset reservation ledger: {result.Diagnostic}");
            }
        }

        var expectedReservations = expected.ExportState().Reservations;
        var actualReservations = assetReservations.ExportState().Reservations;
        if (!expectedReservations.SequenceEqual(actualReservations))
        {
            throw new InvalidDataException("The world asset reservation ledger does not match active package reservations.");
        }
    }

    private bool TryFindBuildingPosition(BuildingDefinition definition, string actor, out GridPoint position)
    {
        // Once the worker reaches a legal site, retain it even if another
        // inhabitant has since vacated an earlier tile in the map scan.
        var current = inhabitants[actor].Position;
        if (CanPlaceBuilding(definition, current, out _) &&
            !WorldContentSimulationRules.Footprint(definition, current).Any(point =>
                inhabitants.Values.Any(person => person.InhabitantId != actor && person.Position == point)))
        {
            position = current;
            return true;
        }
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var candidate = new GridPoint(x, y);
                if (CanPlaceBuilding(definition, candidate, out _) &&
                    !WorldContentSimulationRules.Footprint(definition, candidate).Any(point =>
                        inhabitants.Values.Any(person => person.InhabitantId != actor && person.Position == point)) &&
                    FindUnoccupiedRoute(actor, inhabitants[actor].Position, candidate, 0).Count > 0)
                {
                    position = candidate;
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    private bool TryFindRecipeSite(
        RecipeDefinition recipe,
        out string siteId,
        out GridPoint position)
    {
        if (recipe.IsCrop)
        {
            foreach (var resource in map.Resources
                         .Where(item => item.Id == SeededMapGenerator.FertileLandResourceId &&
                             item.Kind == "fertile_land")
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (resources.TryGetValue(resource.Id, out var resourceState) &&
                    resourceState == ResourceState.Available &&
                    !(worldSimulation.CropBuilds ?? []).Any(job =>
                        job.State == WorldProductionJobState.Running &&
                        job.BuildingInstanceId == WorldBuildSiteRules.FertileLandSiteId(resource.Position)))
                {
                    siteId = WorldBuildSiteRules.FertileLandSiteId(resource.Position);
                    position = resource.Position;
                    return true;
                }
            }

            siteId = string.Empty;
            position = default;
            return false;
        }

        if (recipe.WorkstationBuildingId is null)
        {
            siteId = string.Empty;
            position = default;
            return false;
        }

        foreach (var placed in worldSimulation.Buildings.OrderBy(item => item.InstanceId, StringComparer.Ordinal))
        {
            if (placed.DefinitionId != recipe.WorkstationBuildingId)
            {
                continue;
            }

            var definition = worldContent.Buildings.Single(item => item.CanonicalId == placed.DefinitionId);
            var activeJobs = worldSimulation.ProductionJobs.Count(item =>
                item.BuildingInstanceId == placed.InstanceId && item.State == WorldProductionJobState.Running);
            if (activeJobs < definition.Capacity)
            {
                siteId = placed.InstanceId;
                position = placed.Position;
                return true;
            }
        }

        siteId = string.Empty;
        position = default;
        return false;
    }

    private bool HasAvailableQuantities(IReadOnlyList<ContentQuantity> quantities)
    {
        var inventory = society.Checkpoint.Inventory;
        foreach (var requested in quantities)
        {
            var available = inventory.Lots
                .Where(lot => lot.OwnerId == HouseholdId && lot.ItemKind == requested.ResourceId)
                .Sum(AvailableLotQuantity);
            if (available < requested.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildInstanceId(string inhabitantId, BuildingDefinition definition)
    {
        // Preserve valid legacy IDs; descendant identities contain separators
        // that are legal society IDs but invalid content instance IDs.
        if (inhabitantId.All(character => char.IsLower(character) || char.IsDigit(character) || character is '.' or '-' or '_'))
            return $"build-{inhabitantId}-{definition.PackageDigest[7..15]}-{definition.LocalId}";
        return "build-v2-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inhabitantId + "\n" + definition.CanonicalId)));
    }

    private bool CanPlaceBuilding(
        BuildingDefinition definition,
        GridPoint position,
        out string failure)
    {
        var footprint = WorldContentSimulationRules.Footprint(definition, position).ToArray();
        if (footprint.Any(point => !map.IsPassable(point)))
        {
            failure = "Every building footprint tile must be inside the map on passable ground.";
            return false;
        }

        var occupied = map.CampObjects
            .Select(item => item.Position)
            .Concat(map.Resources.Select(item => item.Position))
            .ToHashSet();
        var buildingDefinitions = worldContent.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        foreach (var placed in worldSimulation.Buildings)
        {
            if (!buildingDefinitions.TryGetValue(placed.DefinitionId, out var existingDefinition))
            {
                failure = $"Placed building '{placed.InstanceId}' references an unavailable definition.";
                return false;
            }

            foreach (var existingPoint in WorldContentSimulationRules.Footprint(existingDefinition, placed.Position))
            {
                occupied.Add(existingPoint);
            }
        }

        if (footprint.Any(occupied.Contains))
        {
            failure = "The building footprint overlaps an existing object, resource, or building.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        society.Apply(checkpoint => new SocietyOperationResult(
            checkpoint with { Inventory = transition(checkpoint.Inventory) },
            null,
            []));
    }

    private static InventoryCheckpoint ConsumeQuantities(
        InventoryCheckpoint inventory,
        IReadOnlyList<ContentQuantity> quantities,
        string purpose)
    {
        var current = inventory;
        for (var quantityIndex = 0; quantityIndex < quantities.Count; quantityIndex++)
        {
            var requested = quantities[quantityIndex];
            var remaining = requested.Amount;
            var lots = current.Lots
                .Where(lot => lot.OwnerId == HouseholdId && lot.ItemKind == requested.ResourceId && lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal)
                .ToArray();
            foreach (var lot in lots)
            {
                if (remaining == 0)
                {
                    break;
                }

                var reserved = current.Reservations
                    .Where(reservation => reservation.LotId == lot.Id &&
                        reservation.State is InventoryReservationState.Reserved or
                            InventoryReservationState.PartiallyConsumed or
                            InventoryReservationState.Committed)
                    .Sum(reservation => reservation.Quantity);
                var available = lot.Quantity - reserved;
                if (available <= 0)
                {
                    continue;
                }

                var amount = Math.Min(remaining, available);
                var reservationId = $"{purpose}:quantity:{quantityIndex}:lot:{lot.Id}";
                current = InventoryFixture.Reserve(
                    current,
                    reservationId,
                    HouseholdId,
                    lot.Id,
                    amount,
                    purpose,
                    current.WorldTick);
                current = InventoryFixture.ConsumeReservation(current, reservationId);
                remaining -= amount;
            }

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Insufficient '{requested.ResourceId}' for {purpose}; missing {remaining.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            }
        }

        return current;
    }

    private static InventoryCheckpoint ReserveQuantities(
        InventoryCheckpoint inventory,
        IReadOnlyList<ContentQuantity> quantities,
        string purpose,
        long expiryTick,
        out IReadOnlyList<string> reservationIds)
    {
        var current = inventory;
        var created = new List<string>();
        for (var quantityIndex = 0; quantityIndex < quantities.Count; quantityIndex++)
        {
            var requested = quantities[quantityIndex];
            var remaining = requested.Amount;
            var lots = current.Lots
                .Where(lot => lot.OwnerId == HouseholdId && lot.ItemKind == requested.ResourceId && lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal)
                .ToArray();
            foreach (var lot in lots)
            {
                if (remaining == 0)
                {
                    break;
                }

                var reserved = current.Reservations
                    .Where(reservation => reservation.LotId == lot.Id &&
                        reservation.State is InventoryReservationState.Reserved or
                            InventoryReservationState.PartiallyConsumed or
                            InventoryReservationState.Committed)
                    .Sum(reservation => reservation.Quantity);
                var available = lot.Quantity - reserved;
                if (available <= 0)
                {
                    continue;
                }

                var amount = Math.Min(remaining, available);
                var reservationId = $"{purpose}:quantity:{quantityIndex}:lot:{lot.Id}";
                current = InventoryFixture.Reserve(
                    current,
                    reservationId,
                    HouseholdId,
                    lot.Id,
                    amount,
                    purpose,
                    expiryTick);
                created.Add(reservationId);
                remaining -= amount;
            }

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Insufficient '{requested.ResourceId}' for production; missing {remaining.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            }
        }

        reservationIds = created.ToArray();
        return current;
    }

    private void ProcessProduction(long targetTick)
    {
        var due = worldSimulation.ProductionJobs
            .Where(job => job.State == WorldProductionJobState.Running && job.CompletionTick <= targetTick)
            .OrderBy(job => job.CompletionTick)
            .ThenBy(job => job.JobId, StringComparer.Ordinal)
            .ToArray();
        foreach (var job in due)
        {
            var recipe = worldContent.Recipes.SingleOrDefault(item => item.CanonicalId == job.RecipeId);
            if (recipe is null)
            {
                throw new InvalidDataException($"Production job '{job.JobId}' references a recipe that is no longer active.");
            }

            var completed = CompleteProductionJob(job, recipe, targetTick);

            worldSimulation = new WorldContentSimulationState(
                worldSimulation.Buildings,
                worldSimulation.ProductionJobs
                    .Select(candidate => candidate.JobId == job.JobId
                        ? candidate with { State = completed ? WorldProductionJobState.Completed : WorldProductionJobState.Cancelled }
                        : candidate)
                    .OrderBy(candidate => candidate.JobId, StringComparer.Ordinal)
                    .ToArray(),
                worldSimulation.NextProductionJobSequence,
                worldSimulation.CropBuilds);
            AppendEvent(completed ? "recipe_completed" : "recipe_cancelled", $"{job.JobId}:{recipe.CanonicalId}");
        }
    }

    private void ProcessCropBuilds(long targetTick)
    {
        var due = (worldSimulation.CropBuilds ?? [])
            .Where(job => job.State == WorldProductionJobState.Running && job.CompletionTick <= targetTick)
            .OrderBy(job => job.CompletionTick)
            .ThenBy(job => job.JobId, StringComparer.Ordinal)
            .ToArray();
        foreach (var job in due)
        {
            var recipe = worldContent.Recipes.SingleOrDefault(item => item.CanonicalId == job.RecipeId);
            if (recipe is null || !recipe.IsCrop)
            {
                throw new InvalidDataException($"Crop build '{job.JobId}' references a recipe that is no longer active.");
            }

            var completed = CompleteProductionJob(job, recipe, targetTick);
            worldSimulation = new WorldContentSimulationState(
                worldSimulation.Buildings,
                worldSimulation.ProductionJobs,
                worldSimulation.NextProductionJobSequence,
                (worldSimulation.CropBuilds ?? [])
                    .Select(candidate => candidate.JobId == job.JobId
                        ? candidate with { State = completed ? WorldProductionJobState.Completed : WorldProductionJobState.Cancelled }
                        : candidate)
                    .OrderBy(candidate => candidate.JobId, StringComparer.Ordinal)
                    .ToArray());
            AppendEvent(completed ? "build_completed" : "build_cancelled", $"{job.JobId}:{recipe.CanonicalId}");
        }
    }

    private bool CompleteProductionJob(
        WorldProductionJob job,
        RecipeDefinition recipe,
        long targetTick)
    {
        var inventoryState = society.Checkpoint.Inventory;
        var inputs = job.InputReservationIds.Select(inventoryState.GetReservation).ToArray();
        if (inputs.Any(reservation => reservation.State is not (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) ||
            reservation.ExpiryTick < targetTick || inventoryState.Lots.FirstOrDefault(lot => lot.Id == reservation.LotId) is not { FreshnessBasisPoints: > 0, ConditionBasisPoints: > 0 }))
        {
            ApplyInventoryTransition(inventory =>
            {
                foreach (var reservation in inputs.Where(reservation => reservation.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed))
                {
                    inventory = InventoryFixture.ReleaseReservation(inventory, reservation.Id, "production_input_unusable");
                }
                return inventory;
            });
            AppendEvent("production_input_unusable", job.JobId);
            return false;
        }
        ApplyInventoryTransition(inventory =>
        {
            var current = inventory;
            foreach (var reservationId in job.InputReservationIds.Order(StringComparer.Ordinal))
            {
                current = InventoryFixture.ConsumeReservation(current, reservationId);
            }

            for (var outputIndex = 0; outputIndex < recipe.Outputs.Count; outputIndex++)
            {
                var output = recipe.Outputs[outputIndex];
                current = InventoryFixture.AddLot(
                    current,
                    $"{job.JobId}:output:{outputIndex.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}",
                    output.ResourceId,
                    HouseholdId,
                    CropOutputQuantity(recipe, output),
                    targetTick);
            }

            return current;
        });
        if (recipe.IsCrop && survivalState is not null && worldSystems.Climate.Weather is WeatherKind.Snow or WeatherKind.Storm)
        {
            AppendEvent("crop_weather_loss", $"{job.JobId}:{worldSystems.Climate.Weather.ToString().ToLowerInvariant()}");
        }
        CreditCompletedWork(job.WorkerId, recipe.IsCrop ? "farming" : "crafting");
        return true;
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
            resources[resource.Id] = resource.State == EcologyResourceState.Available && resource.Quantity > 0
                ? ResourceState.Available
                : ResourceState.Depleted;
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
                HungerBasisPoints = Math.Max(0, state.HungerBasisPoints - 4),
                EnergyBasisPoints = Math.Max(0, state.EnergyBasisPoints - 3 - (state.Survival?.IllnessBasisPoints ?? 0) / 2_000 -
                    (state.Survival is { NutritionBasisPoints: < 2_000 } ? 1 : 0)),
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
            var deceased = society.Checkpoint.GetInhabitant(id);
            var deathTick = deceased.DeathTick ?? throw new InvalidDataException("A removed inhabitant has no committed death.");
            deceasedInhabitants.Add(id, new PlaytestDeceasedInhabitantState(
                id, deathTick, society.Checkpoint.AgeAt(deceased, deathTick), inhabitants[id]));
            inhabitants.Remove(id);
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent("inhabitant_removed", id);
        }
    }

    private void EnqueueDueCognition()
    {
        var runtimes = society.Capture().Cognition.Runtimes
            .ToDictionary(item => item.InhabitantId, StringComparer.Ordinal);
        foreach (var inhabitant in society.Checkpoint.Inhabitants
                     .Where(item => item.Status == SocietyInhabitantStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (inhabitant.AgeBand == SocietyAgeBand.Infant)
            {
                continue;
            }
            var physical = inhabitants[inhabitant.Id];
            if (physical.Project is { Stage: not ("completed" or "cancelled") } project &&
                (physical.HungerBasisPoints < 3_500 || physical.EnergyBasisPoints < 2_500 || HasUrgentExposure(physical) && !IsProtectiveProject(project)))
            {
                SetProject(inhabitant.Id, project with { Stage = "paused", Blocker = HasUrgentExposure(physical) ? "Seeking warmth or recovering" : "Meeting food or rest needs" });
                physical = inhabitants[inhabitant.Id];
            }
            var candidates = CreateCandidates(inhabitant.Id, physical);
            var current = runtimes[inhabitant.Id].CurrentIntention;
            if (!NeedsCognition(inhabitant.Id, current, candidates))
            {
                continue;
            }

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
            else
            {
                inhabitants[inhabitant.Id] = inhabitants[inhabitant.Id] with
                {
                    LastDecisionContext = DecisionContext(physical, candidates),
                };
            }
        }
    }

    private bool NeedsCognition(
        string inhabitantId,
        CognitionIntention? current,
        List<CognitionCandidate> candidates)
    {
        if (PendingInstructionFor(inhabitantId) is not null)
        {
            return true;
        }

        if (CanContinueLesson(inhabitantId) || CanContinueProject(inhabitants[inhabitantId]))
        {
            return false;
        }

        if (candidates.Count == 1 && candidates[0].Id == "safe_idle")
        {
            return false;
        }

        if (current is null)
        {
            return true;
        }

        if (!candidates.Any(candidate => candidate.Id == current.CandidateId))
        {
            return true;
        }

        if (current.CandidateId is "harvest_food" or "consume_food")
        {
            return true;
        }

        if (current.CandidateId == "safe_idle")
        {
            var physical = inhabitants[inhabitantId];
            return DecisionContext(physical, candidates) != physical.LastDecisionContext ||
                checked(WorldTick - current.WorldTick) >= 300;
        }

        return checked(WorldTick - current.WorldTick) >= CognitionReevaluationIntervalTicks;
    }

    private static string DecisionContext(PlaytestInhabitantState state, List<CognitionCandidate> candidates) =>
        $"{state.HungerBasisPoints < 2_500}:{state.EnergyBasisPoints < 1_500}:{HasUrgentExposure(state)}:" +
        string.Join('|', candidates.Select(candidate => candidate.Id).Order(StringComparer.Ordinal));

    private void ApplyContinuingIntentions(IEnumerable<string> dispatchedInhabitantIds)
    {
        var dispatched = dispatchedInhabitantIds.ToHashSet(StringComparer.Ordinal);
        var runtimes = society.Capture().Cognition.Runtimes
            .ToDictionary(item => item.InhabitantId, StringComparer.Ordinal);
        foreach (var inhabitant in society.Checkpoint.Inhabitants
                     .Where(item => item.Status == SocietyInhabitantStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (dispatched.Contains(inhabitant.Id) || !inhabitants.TryGetValue(inhabitant.Id, out var state))
            {
                continue;
            }
            if (inhabitant.AgeBand == SocietyAgeBand.Infant)
            {
                inhabitants[inhabitant.Id] = state with { EnergyBasisPoints = Math.Min(10_000, state.EnergyBasisPoints + 10) };
                continue;
            }
            if (CanContinueLesson(inhabitant.Id))
            {
                ContinueLesson(inhabitant.Id);
                continue;
            }
            if (CanContinueProject(state))
            {
                ContinueProject(inhabitant.Id, state);
                continue;
            }
            if (runtimes[inhabitant.Id].CurrentIntention is not { } intention ||
                !CreateCandidates(inhabitant.Id, state).Any(candidate => candidate.Id == intention.CandidateId))
            {
                continue;
            }

            ApplyCandidate(inhabitant.Id, state, intention.CandidateId, reportIdle: false);
        }
    }

    private void ApplySafeRoutinesWhileWaiting(IEnumerable<string> waitingIds)
    {
        var safe = new HashSet<string>(StringComparer.Ordinal)
        {
            "consume_food", "collect_shared_food", "harvest_food", "seek_food", "sleep",
            "wear_clothing", "tend_fire", "seek_warmth",
        };
        foreach (var id in waitingIds.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (!inhabitants.TryGetValue(id, out var state)) continue;
            if (state.HungerBasisPoints >= 3_500 && state.EnergyBasisPoints >= 2_500 && !HasUrgentExposure(state))
                continue;
            var candidate = CreateCandidates(id, state)
                .Where(item => safe.Contains(item.Id))
                .OrderBy(item => item.DeterministicPriority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is not null) ApplyCandidate(id, state, candidate.Id, reportIdle: false);
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
        var forcedCandidate = !decision.Admission.FellBack && pendingInstruction?.Kind == OwnerInstructionKind.MustDo
            ? InstructionCandidate(pendingInstruction.Text)
            : null;
        if (forcedCandidate is not null && CreateCandidates(decision.InhabitantId, state)
            .Any(candidate => candidate.Id == forcedCandidate))
        {
            candidateId = forcedCandidate;
        }

        ApplyCandidate(decision.InhabitantId, state, candidateId, reportIdle: true);

        if (!decision.Admission.FellBack && pendingInstruction is not null &&
            (pendingInstruction.Kind == OwnerInstructionKind.Suggestive || forcedCandidate is not null))
        {
            completedInstructionIds.Add(pendingInstruction.InstructionId);
            AppendEvent("instruction_applied", $"{pendingInstruction.InstructionId}:{candidateId}");
        }
    }

    private void ApplyCandidate(
        string inhabitantId,
        PlaytestInhabitantState state,
        string candidateId,
        bool reportIdle)
    {
        if (candidateId.StartsWith("guardian_", StringComparison.Ordinal))
        {
            ApplyDependentCareCandidate(inhabitantId, candidateId);
            return;
        }
        if (candidateId.StartsWith("parent_", StringComparison.Ordinal) || candidateId.StartsWith("care:", StringComparison.Ordinal))
        {
            ApplyParenthoodCandidate(inhabitantId, candidateId);
            return;
        }
        if (candidateId.StartsWith("partner_", StringComparison.Ordinal))
        {
            ApplyFamilyCandidate(inhabitantId, candidateId);
            return;
        }
        if (candidateId.StartsWith("learn:", StringComparison.Ordinal) || candidateId.StartsWith("lesson_", StringComparison.Ordinal))
        {
            ApplyLearningCandidate(inhabitantId, candidateId);
            return;
        }
        if (candidateId.StartsWith("council_", StringComparison.Ordinal))
        {
            ApplyCouncilCandidate(inhabitantId, candidateId);
            return;
        }
        if (candidateId.StartsWith("trade_", StringComparison.Ordinal))
        {
            ApplyTradeCandidate(inhabitantId, state, candidateId);
            return;
        }
        if (candidateId.StartsWith("build:", StringComparison.Ordinal))
        {
            BeginProject(inhabitantId, state, candidateId);
            return;
        }
        if (candidateId.StartsWith("invent:building:", StringComparison.Ordinal))
        {
            ApplyInhabitantBuildingDesignCandidate(inhabitantId, candidateId);
            return;
        }
        if (candidateId.StartsWith("assist:", StringComparison.Ordinal))
        {
            AssistProject(inhabitantId, state, candidateId[7..]);
            return;
        }

        switch (candidateId)
        {
            case "wear_clothing":
                CollectEquipment(inhabitantId, state, "clothing");
                break;
            case "tend_fire":
                TendFire(inhabitantId, state);
                break;
            case "seek_warmth":
                SeekWarmth(inhabitantId, state);
                break;
            case "seek_food":
                MoveToward(
                    inhabitantId,
                    state,
                    map.GetResource(BerryResourceId).Position,
                    "food",
                    ResourceInteractionRange);
                break;
            case "harvest_food":
                HarvestFood(inhabitantId, state);
                break;
            case "consume_food":
                ConsumeFood(inhabitantId, state);
                break;
            case "collect_shared_food":
                CollectSharedFood(inhabitantId, state);
                break;
            case "sleep":
                Sleep(inhabitantId, state);
                break;
            default:
                if (reportIdle)
                {
                    AppendEvent("inhabitant_idle", inhabitantId);
                }
                break;
        }
    }

    private void ApplyBuildDecision(
        string inhabitantId,
        PlaytestInhabitantState state,
        string candidateId)
    {
        const string buildingPrefix = "build:building:";
        const string recipePrefix = "build:recipe:";
        if (candidateId.StartsWith(buildingPrefix, StringComparison.Ordinal))
        {
            var definitionId = candidateId[buildingPrefix.Length..];
            var definition = worldContent.Buildings.SingleOrDefault(item => item.CanonicalId == definitionId);
            if (definition is null || !TryFindBuildingPosition(definition, inhabitantId, out var position))
            {
                AppendEvent("build_rejected", $"{inhabitantId}:{candidateId}:no_valid_site");
                return;
            }

            if (state.Position != position)
            {
                MoveToward(inhabitantId, state, position, "build");
                return;
            }

            var placement = PlaceBuildingCore(
                BuildInstanceId(inhabitantId, definition),
                definition.CanonicalId,
                position,
                "build_completed");
            if (!placement.Applied)
            {
                AppendEvent("build_rejected", $"{inhabitantId}:{candidateId}:{placement.Failure}");
            }
            else
            {
                CreditCompletedWork(inhabitantId, "building");
            }

            return;
        }

        if (!candidateId.StartsWith(recipePrefix, StringComparison.Ordinal))
        {
            AppendEvent("build_rejected", $"{inhabitantId}:{candidateId}:unknown_target");
            return;
        }

        var recipeId = candidateId[recipePrefix.Length..];
        var recipe = worldContent.Recipes.SingleOrDefault(item => item.CanonicalId == recipeId);
        if (recipe is null || !TryFindRecipeSite(recipe, out var siteId, out var sitePosition))
        {
            AppendEvent("build_rejected", $"{inhabitantId}:{candidateId}:no_valid_site");
            return;
        }

        if (state.Position != sitePosition)
        {
            MoveToward(inhabitantId, state, sitePosition, "build");
            return;
        }

        var started = StartProductionCore(recipe.CanonicalId, siteId, inhabitantId, "build_started");
        if (!started.Applied)
        {
            AppendEvent("build_rejected", $"{inhabitantId}:{candidateId}:{started.Failure}");
        }
    }

    private void MoveToward(
        string inhabitantId,
        PlaytestInhabitantState state,
        GridPoint destination,
        string reason,
        int interactionRange = 0)
    {
        if (IsWithinInteractionRange(state.Position, destination, interactionRange))
        {
            AppendEvent("destination_reached", $"{inhabitantId}:{reason}");
            return;
        }

        var route = FindUnoccupiedRoute(inhabitantId, state.Position, destination, interactionRange);
        if (route.Count < 2)
        {
            RecordMovementBlocked(inhabitantId, state, "no_route");
            return;
        }

        var next = route[1];
        var weatherCost = survivalState is null ? 0 : worldSystems.Climate.Weather switch
        {
            WeatherKind.Storm => 3,
            WeatherKind.Snow or WeatherKind.Rain => 1,
            _ => 0,
        };
        inhabitants[inhabitantId] = state with
        {
            Position = next,
            MoveWaitTicks = 0,
            EnergyBasisPoints = Math.Max(0, state.EnergyBasisPoints - weatherCost)
        };
        AppendEvent("inhabitant_moved", $"{inhabitantId}:{state.Position.X},{state.Position.Y}->{next.X},{next.Y}:{reason}");
    }

    private List<GridPoint> FindUnoccupiedRoute(
        string inhabitantId,
        GridPoint origin,
        GridPoint destination,
        int interactionRange)
    {
        var occupied = inhabitants.Values
            .Where(item => item.InhabitantId != inhabitantId)
            .Select(item => item.Position)
            .ToHashSet();
        var open = new Queue<GridPoint>();
        var visited = new HashSet<GridPoint> { origin };
        var predecessor = new Dictionary<GridPoint, GridPoint>();
        open.Enqueue(origin);

        while (open.TryDequeue(out var current))
        {
            if (IsWithinInteractionRange(current, destination, interactionRange))
            {
                var route = new List<GridPoint> { current };
                while (current != origin)
                {
                    current = predecessor[current];
                    route.Add(current);
                }

                route.Reverse();
                return route;
            }

            foreach (var next in MapAcceptance.CardinalNeighbors(current))
            {
                if (!map.IsPassable(next) || occupied.Contains(next) || !visited.Add(next))
                {
                    continue;
                }

                predecessor[next] = current;
                open.Enqueue(next);
            }
        }

        return [];
    }

    private void RecordMovementBlocked(
        string inhabitantId,
        PlaytestInhabitantState state,
        string reason)
    {
        var waitTicks = checked(state.MoveWaitTicks + 1);
        inhabitants[inhabitantId] = state with { MoveWaitTicks = waitTicks };
        if (waitTicks == 1 || waitTicks % 30 == 0)
        {
            AppendEvent("movement_blocked", $"{inhabitantId}:{reason}:wait={waitTicks}");
        }
    }

    private static bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) =>
        Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y) <= interactionRange;

    private void HarvestFood(string inhabitantId, PlaytestInhabitantState state)
    {
        if (!IsWithinInteractionRange(
                state.Position,
                map.GetResource(BerryResourceId).Position,
                ResourceInteractionRange) ||
            resources[BerryResourceId] != ResourceState.Available)
        {
            AppendEvent("harvest_failed", $"{inhabitantId}:not_at_available_food");
            return;
        }

        var ecologyResource = worldSystems.Ecology.GetResource(BerryResourceId);
        var harvest = EcologyRules.Harvest(ecologyResource, 1);
        if (!harvest.IsValid || harvest.Resource is null)
        {
            SyncEcologyResourceStates();
            AppendEvent("harvest_failed", $"{inhabitantId}:{harvest.Failure ?? "food_depleted"}");
            return;
        }

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
        ApplyInventoryTransition(inventory => InventoryFixture.AddLot(
            inventory,
            $"food:harvest:{WorldTick:D10}:{inhabitantId}",
            "food",
            inhabitantId,
            HarvestFoodYield,
            WorldTick));

        AppendEvent("food_harvested", $"{inhabitantId}:{HarvestFoodYield}");
    }

    private InventoryLot? AvailableSharedFood(string actor) => MayCollectSharedFood(actor) ? PreferredFood(HouseholdId, actor).FirstOrDefault() : null;

    private void CollectSharedFood(string inhabitantId, PlaytestInhabitantState state)
    {
        var supplyPoint = map.GetObject("bedroll").Position;
        if (!IsWithinInteractionRange(state.Position, supplyPoint, ResourceInteractionRange))
        {
            MoveToward(inhabitantId, state, supplyPoint, "household_food", ResourceInteractionRange);
            return;
        }

        if (AvailableSharedFood(inhabitantId) is not { } lot)
        {
            return;
        }

        ApplyInventoryTransition(inventory => InventoryFixture.Transfer(
            inventory, $"household-food:{WorldTick}:{inhabitantId}", HouseholdId, inhabitantId,
            lot.Id, 1, "household_food_share"));
        AppendEvent("household_food_collected", $"{inhabitantId}:{lot.Id}:1");
    }

    private void ConsumeFood(string inhabitantId, PlaytestInhabitantState state)
    {
        var lot = PreferredFood(inhabitantId, inhabitantId).FirstOrDefault();
        if (lot is null)
        {
            AppendEvent("consumption_failed", $"{inhabitantId}:no_food");
            return;
        }

        society.Apply(checkpoint => SocietyFixture.ConsumeInventory(checkpoint, inhabitantId, lot.Id, 1));
        inhabitants[inhabitantId] = state with
        {
            HungerBasisPoints = Math.Min(10_000, state.HungerBasisPoints + 3_000),
            Survival = AfterMeal(state, lot)
        };
        AppendEvent("food_consumed", inhabitantId);
    }

    private void Sleep(string inhabitantId, PlaytestInhabitantState state)
    {
        var restPosition = BuildingsWithTag("shelter")
            .Select(building => new { building.Position, Route = FindUnoccupiedRoute(inhabitantId, state.Position, building.Position, ResourceInteractionRange) })
            .Where(site => site.Route.Count > 0)
            .OrderBy(site => site.Route.Count)
            .Select(site => (GridPoint?)site.Position)
            .FirstOrDefault() ?? map.GetObject("bedroll").Position;
        if (!IsWithinInteractionRange(state.Position, restPosition, ResourceInteractionRange))
        {
            if (FindUnoccupiedRoute(inhabitantId, state.Position, restPosition, ResourceInteractionRange).Count < 2)
            {
                // Congestion cannot make rest physically impossible. Outdoor
                // rest is less effective and does not remove exposure hazards.
                inhabitants[inhabitantId] = state with { EnergyBasisPoints = Math.Min(10_000, state.EnergyBasisPoints + 500) };
                AppendEvent("inhabitant_rested_outdoors", inhabitantId);
                return;
            }
            MoveToward(inhabitantId, state, restPosition, "sleep", ResourceInteractionRange);
            return;
        }

        inhabitants[inhabitantId] = state with { EnergyBasisPoints = Math.Min(10_000, state.EnergyBasisPoints + RestRecovery(state)) };
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
            item.OwnerId == inhabitantId && item.ItemKind == "food" && AvailableLotQuantity(item) > 0);
        if (hasFood && state.HungerBasisPoints < 8_500)
        {
            candidates.Add(new CognitionCandidate("consume_food", "Eat one carried food item.", 0));
        }

        if (instructionCandidate == "consume_food" && hasFood && !candidates.Any(item => item.Id == "consume_food"))
        {
            candidates.Add(new CognitionCandidate("consume_food", "Follow the owner's food instruction.", 0));
        }

        var berry = map.GetResource(BerryResourceId);
        var foodPriority = state.HungerBasisPoints < 2_500 ? 2 : 5;
        var shouldGatherFood = !hasFood && state.HungerBasisPoints < 7_000;
        if (shouldGatherFood && contentRegistry.ExportState().Packages.Any(package =>
                package.Manifest.PackageId == StarterContent.PackageId && package.Lifecycle == ContentPackageLifecycle.Active) &&
            AvailableSharedFood(inhabitantId) is not null &&
            FindUnoccupiedRoute(inhabitantId, state.Position, map.GetObject("bedroll").Position, ResourceInteractionRange).Count > 0)
        {
            candidates.Add(new CognitionCandidate("collect_shared_food",
                "Collect one available household food serving at camp, then eat it.",
                foodPriority - 1, HouseholdId));
        }
        if (shouldGatherFood && resources[BerryResourceId] == ResourceState.Available &&
            IsWithinInteractionRange(state.Position, berry.Position, ResourceInteractionRange))
        {
            candidates.Add(new CognitionCandidate(
                "harvest_food",
                "Gather several food servings from the nearby berry patch.",
                foodPriority,
                BerryResourceId));
        }
        else if (shouldGatherFood && resources[BerryResourceId] == ResourceState.Available)
        {
            candidates.Add(new CognitionCandidate(
                "seek_food",
                "Travel within gathering range of the available berry patch.",
                foodPriority,
                BerryResourceId));
        }

        if (instructionCandidate == "seek_food" && resources[BerryResourceId] == ResourceState.Available &&
            !candidates.Any(item => item.Id == "seek_food"))
        {
            candidates.Add(new CognitionCandidate("seek_food", "Follow the owner's travel instruction.", 0, BerryResourceId));
        }

        if (instructionCandidate == "harvest_food" &&
            resources[BerryResourceId] == ResourceState.Available &&
            IsWithinInteractionRange(state.Position, berry.Position, ResourceInteractionRange) &&
            !candidates.Any(item => item.Id == "harvest_food"))
        {
            candidates.Add(new CognitionCandidate("harvest_food", "Follow the owner's harvest instruction.", 0, BerryResourceId));
        }

        if (state.EnergyBasisPoints < 3_500 && !candidates.Any(item => item.Id == "sleep"))
        {
            var sleepPriority = state.EnergyBasisPoints < 1_500 ? 1 : 10;
            candidates.Add(new CognitionCandidate("sleep", "Rest at reachable shelter or bedding; recover less outdoors if access is blocked.", sleepPriority, "bedroll"));
        }

        AddSurvivalCandidates(candidates, inhabitantId, state);
        AddDependentCareCandidates(candidates, inhabitantId);
        if (state.HungerBasisPoints >= 2_500 && state.EnergyBasisPoints >= 1_500 && AdultResident(inhabitantId))
        {
            var inhabitant = society.Checkpoint.GetInhabitant(inhabitantId);
            AddBuildCandidates(candidates, inhabitant, state);
            AddInhabitantBuildingDesignCandidates(candidates, inhabitant, state);
            AddProjectAssistanceCandidates(candidates, inhabitantId);
            AddTradeCandidates(candidates, inhabitantId);
            AddCouncilCandidates(candidates, inhabitantId);
            AddLearningCandidates(candidates, inhabitantId);
            AddFamilyCandidates(candidates, inhabitantId);
            AddParenthoodCandidates(candidates, inhabitantId);
        }

        candidates.Add(new CognitionCandidate("safe_idle", "Continue safely without starting a new task.", 100));
        return candidates;
    }

    private void AddBuildCandidates(
        List<CognitionCandidate> candidates,
        SocietyInhabitant inhabitant,
        PlaytestInhabitantState state)
    {
        var canBuildStructures = inhabitant.CurrentRole == SocietyWorkRole.Builder ||
            state.Aspiration.Contains("build", StringComparison.OrdinalIgnoreCase);
        if (canBuildStructures)
        {
            foreach (var definition in worldContent.Buildings)
            {
                if (HasUrgentExposure(state) && !definition.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking"))
                {
                    continue;
                }
                var instanceId = BuildInstanceId(inhabitant.Id, definition);
                if (worldSimulation.Buildings.Any(item => item.InstanceId == instanceId) ||
                    !CanAcquireProjectInputs(definition.BuildCosts) ||
                    !TryFindBuildingPosition(definition, inhabitant.Id, out var position))
                {
                    continue;
                }

                candidates.Add(new CognitionCandidate(
                    $"build:building:{definition.CanonicalId}",
                    $"Plan {definition.DisplayName}: acquire materials, travel, and build.",
                    20,
                    $"build-site:{position.X},{position.Y}"));
            }
        }

        var canGrow = inhabitant.CurrentRole == SocietyWorkRole.Farmer ||
            state.Aspiration.Contains("self-sufficient", StringComparison.OrdinalIgnoreCase);
        var canProduce = inhabitant.CurrentRole is SocietyWorkRole.Farmer or
            SocietyWorkRole.Builder or SocietyWorkRole.Trader or SocietyWorkRole.Organizer;
        foreach (var recipe in worldContent.Recipes.Where(item => item.IsCrop ? canGrow : canProduce))
        {
            if (HasUrgentExposure(state) && !recipe.Outputs.Any(output => output.ResourceId == "clothing"))
            {
                continue;
            }
            if (!NeedsRecipeOutput(recipe) || !CanAcquireProjectInputs(recipe.Inputs) || !TryFindRecipeSite(recipe, out _, out var position))
            {
                continue;
            }

            candidates.Add(new CognitionCandidate(
                $"build:recipe:{recipe.CanonicalId}",
                $"Build {recipe.DisplayName} at a valid site.",
                recipe.IsCrop ? 20 : WeatherExposure > 0 && recipe.Outputs.Any(output => output.ResourceId == "clothing") ? 25 : 30,
                $"build-site:{position.X},{position.Y}"));
        }
    }

    private static int PriorityFor(PlaytestInhabitantState state) =>
        state.HungerBasisPoints < 2_500 || state.EnergyBasisPoints < 1_500 || HasUrgentExposure(state) ? 20 : 0;

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
            .Append("clankerworld.private-world-observation/v1|")
            .Append(inhabitantId).Append('|')
            .Append(state.Position.X).Append(',').Append(state.Position.Y).Append('|')
            .Append(state.HungerBasisPoints).Append('|').Append(state.EnergyBasisPoints).Append('|')
            .Append(string.Join(',', candidates.Select(candidate => candidate.Id)))
            .ToString();
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}";
    }

    private void AppendEvent(string kind, string detail)
    {
        GridPoint? position = null;
        for (var length = detail.Length; length > 0; length = detail.LastIndexOf(':', length - 1))
        {
            var prefix = detail[..length];
            if (inhabitants.TryGetValue(prefix, out var living))
            {
                position = living.Position;
                break;
            }
            if (deceasedInhabitants.TryGetValue(prefix, out var deceased))
            {
                position = deceased.LastPhysical.Position;
                break;
            }
        }
        events.Add(new PlaytestWorldEvent(nextEventId++, WorldTick, kind, detail, position));
    }

    internal static void ValidateStateForCodec(PrivateWorldRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion < 10 && (state.Society.Society.LifeClock is not null ||
            state.Society.Society.Inhabitants.Any(person => person.BirthLifeTick is not null)))
        {
            throw new InvalidDataException("Biological life pacing requires private-world schema 10.");
        }
        if (state.SchemaVersion is < 1 or > StateSchemaVersion || string.IsNullOrWhiteSpace(state.WorldSeed) || state.EventHistoryFloor < 0)
        {
            throw new InvalidDataException("The private-world runtime state schema or seed is invalid.");
        }
        var hasArchivedEvents = state.EventHistoryFloor > 0 || state.Society.Society.EventHistoryFloor > 0 ||
            state.Society.Society.Inventory.EventHistoryFloor > 0 || state.Society.Cognition.EventHistoryFloor > 0 ||
            state.Society.Cognition.Runtimes.Any(runtime => runtime.EventHistoryFloor > 0);
        if ((hasArchivedEvents && state.HistoryArchiveHead is null) ||
            (state.SchemaVersion < 4 && (hasArchivedEvents || state.HistoryArchiveHead is not null)) ||
            (state.HistoryArchiveHead is { } head && (head.Length != 64 || head.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))))
        {
            throw new InvalidDataException("The private-world history reference or schema is invalid.");
        }

        if (!MapAcceptance.Validate(state.Map).IsValid)
        {
            throw new InvalidDataException("The private-world runtime contains an invalid map.");
        }

        using var society = SocietyWorldRuntime.Restore(state.Society);
        ValidateSurvival(state);
        ValidateCouncil(state);
        ValidateLessons(state);
        foreach (var person in state.Inhabitants)
        {
            ValidateProficiency(person, state.SchemaVersion);
            ValidateSocialStanding(person, state.Society.Society.Inhabitants.Select(item => item.Id),
                state.SchemaVersion, state.Society.Society.WorldTick);
            ValidatePrivateThoughts(person.RecentThoughts, state.SchemaVersion, state.Society.Society.WorldTick);
        }
        ValidateParenthood(state);
        ContentPackageRegistry.Restore(state.Content);
        if (state.SchemaVersion >= 3 && state.Content is null)
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
        else if (state.SchemaVersion >= 3)
        {
            throw new InvalidDataException("The current private-world schema requires richer-systems state.");
        }

        state.WorldContent?.Validate();
        if (state.SchemaVersion >= 3 &&
            (state.WorldContent is null || state.WorldSimulation is null || state.AssetReservations is null))
        {
            throw new InvalidDataException(
                "The current private-world schema requires content simulation and world asset reservation state.");
        }

        if (state.WorldContent is not null && state.WorldSimulation is not null)
        {
            WorldContentSimulationRules.Validate(
                state.WorldSimulation,
                state.WorldContent,
                state.Map,
                state.Society.Society.WorldTick);
        }

        if (state.AssetReservations is not null)
        {
            WorldAssetReservationLedger.Restore(state.AssetReservations);
        }
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
        ValidateDeceasedArchive(state.DeceasedInhabitants ?? [], state.Society.Society, state.Map, state.SchemaVersion);
        foreach (var inhabitant in state.Inhabitants)
        {
            if (inhabitant.Project is { } project)
            {
                if (state.SchemaVersion < 5)
                {
                    throw new InvalidDataException("Settlement projects require save schema 5.");
                }
                ValidateProject(project, state.Society.Society.WorldTick);
            }
        }
    }

    private static void ValidateDeceasedArchive(
        IEnumerable<PlaytestDeceasedInhabitantState> archive,
        SocietyCheckpoint society,
        SeededMap map,
        int schemaVersion)
    {
        var archived = archive.ToArray();
        if (archived.Length > 0 && schemaVersion < 13)
            throw new InvalidDataException("Deceased inhabitant archives require private-world schema 13.");
        if (archived.Select(item => item.InhabitantId).Distinct(StringComparer.Ordinal).Count() != archived.Length)
            throw new InvalidDataException("The deceased inhabitant archive contains duplicate identities.");
        var deceasedById = society.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Dead)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var person in archived)
        {
            if (!deceasedById.TryGetValue(person.InhabitantId, out var deceased) ||
                deceased.DeathTick != person.DeathTick || person.DeathTick < 0 || person.DeathTick > society.WorldTick ||
                person.AgeAtDeath < 0 || person.LastPhysical.InhabitantId != person.InhabitantId ||
                !map.IsPassable(person.LastPhysical.Position) ||
                person.LastPhysical.HungerBasisPoints is < 0 or > 10_000 ||
                person.LastPhysical.EnergyBasisPoints is < 0 or > 10_000)
                throw new InvalidDataException("The deceased inhabitant archive contains an invalid final state.");
            ValidatePrivateThoughts(person.LastPhysical.RecentThoughts, schemaVersion, person.DeathTick);
        }
    }

    private static void ValidatePrivateThoughts(
        IReadOnlyList<PlaytestPrivateThought>? thoughts, int schemaVersion, long latestTick)
    {
        if (thoughts is null) return;
        if (schemaVersion < 14 || thoughts.Count > MaximumRecentThoughts)
            throw new InvalidDataException("The private-thought history version or size is invalid.");
        long previousTick = -1;
        foreach (var thought in thoughts)
        {
            if (thought is null || thought.Text is null ||
                thought.WorldTick < 0 || thought.WorldTick < previousTick || thought.WorldTick > latestTick ||
                CognitionDecisionResponse.NormalizePrivateThought(thought.Text) != thought.Text)
                throw new InvalidDataException("The private-thought history contains an invalid entry.");
            previousTick = thought.WorldTick;
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
    private const string Header = "clankerworld.private-world-runtime/v1";
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
