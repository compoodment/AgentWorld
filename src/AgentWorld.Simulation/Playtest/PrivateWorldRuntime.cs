using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Kernel;
using AgentWorld.Simulation.Society;

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
    IReadOnlyList<PlaytestWorldEvent> Events);

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
    public const int StateSchemaVersion = 1;
    private const string HouseholdId = "household:camp-alpha";
    private const string FoodLotId = "food:camp-alpha";
    private const string BerryResourceId = "berry-patch";

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string worldSeed;
    private readonly Func<string, IDecisionProvider>? providerFactory;
    private readonly double minimumCognitionConfidence;
    private SeededMap map;
    private SocietyWorldRuntime society;
    private readonly Dictionary<string, PlaytestInhabitantState> inhabitants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResourceState> resources = new(StringComparer.Ordinal);
    private readonly List<PlaytestWorldEvent> events = [];
    private long nextEventId = 1;

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
        map = SeededMapGenerator.Generate(this.worldSeed);
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
        events.ToArray());

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

        var candidateId = decision.Admission.Intention.CandidateId;
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
        var hasFood = society.Checkpoint.Inventory.Lots.Any(item =>
            item.OwnerId == inhabitantId && item.ItemKind == "food" && item.Quantity > 0);
        if (hasFood && state.HungerBasisPoints < 8_500)
        {
            candidates.Add(new CognitionCandidate("consume_food", "Eat one carried food item.", 0));
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

        if (state.EnergyBasisPoints < 3_500)
        {
            candidates.Add(new CognitionCandidate("sleep", "Sleep to recover energy.", 10, "bedroll"));
        }

        candidates.Add(new CognitionCandidate("safe_idle", "Continue safely without starting a new task.", 100));
        return candidates;
    }

    private static int PriorityFor(PlaytestInhabitantState state) =>
        state.HungerBasisPoints < 2_500 || state.EnergyBasisPoints < 1_500 ? 20 : 0;

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
        if (state.SchemaVersion != StateSchemaVersion || string.IsNullOrWhiteSpace(state.WorldSeed))
        {
            throw new InvalidDataException("The private-world runtime state schema or seed is invalid.");
        }

        if (!MapAcceptance.Validate(state.Map).IsValid)
        {
            throw new InvalidDataException("The private-world runtime contains an invalid map.");
        }

        using var society = SocietyWorldRuntime.Restore(state.Society);
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
