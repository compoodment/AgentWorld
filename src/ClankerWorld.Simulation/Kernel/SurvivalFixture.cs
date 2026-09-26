using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.Kernel;

/// <summary>
/// Versioned, integer-only survival rules for the Phase 1 acceptance fixture.
/// The fixture is deliberately small, but all mutations happen by replacing an
/// immutable checkpoint so a rejected request cannot expose partial state.
/// </summary>
public sealed record SurvivalRules(
    string TransitionFunctionId,
    int HungerDrainPerTick,
    int EnergyDrainPerTick,
    int ExhaustionDamagePerTick,
    int StarvationDamagePerTick,
    int FoodRecovery,
    int SleepRecovery,
    long RenewableRegenerationTicks)
{
    public static SurvivalRules Fixture { get; } = new(
        "needs-transition/v1",
        HungerDrainPerTick: 100,
        EnergyDrainPerTick: 100,
        ExhaustionDamagePerTick: 75,
        StarvationDamagePerTick: 50,
        FoodRecovery: 2_000,
        SleepRecovery: 2_500,
        RenewableRegenerationTicks: 3);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TransitionFunctionId);
        if (HungerDrainPerTick < 0 || EnergyDrainPerTick < 0 ||
            ExhaustionDamagePerTick < 0 || StarvationDamagePerTick < 0 ||
            FoodRecovery < 0 || SleepRecovery < 0 || RenewableRegenerationTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SurvivalRules));
        }
    }
}

public enum SurvivalResourceState
{
    Available,
    Depleted,
    Regenerating,
    Transformed,
}

public enum SurvivalActionKind
{
    Idle,
    Eat,
    Sleep,
    Harvest,
}

public sealed record SurvivalAction(SurvivalActionKind Kind, string? ResourceId = null)
{
    public static SurvivalAction Idle { get; } = new(SurvivalActionKind.Idle);
    public static SurvivalAction Eat { get; } = new(SurvivalActionKind.Eat);
    public static SurvivalAction Sleep { get; } = new(SurvivalActionKind.Sleep);

    public static SurvivalAction Harvest(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        return new SurvivalAction(SurvivalActionKind.Harvest, resourceId);
    }
}

/// <summary>
/// All need values are basis points in the closed interval [0, 10,000].
/// </summary>
public sealed record SurvivalActor(
    string Id,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    int HealthBasisPoints,
    int FoodItems);

/// <summary>
/// Quantity is the currently extractable yield. A renewable node becomes
/// regenerating only after its final yield is harvested; it never silently
/// respawns from a wall-clock timer.
/// </summary>
public sealed record SurvivalResource(
    string Id,
    bool IsRenewable,
    int Quantity,
    int MaximumQuantity,
    SurvivalResourceState State,
    long? RegenerationDueTick);

public sealed record SurvivalEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record SurvivalCheckpoint(
    long WorldTick,
    SurvivalActor Actor,
    IReadOnlyList<SurvivalResource> Resources,
    IReadOnlyList<SurvivalEvent> Events)
{
    public SurvivalResource GetResource(string id) =>
        Resources.Single(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));
}

/// <summary>
/// Atomic, deterministic survival transition. Passive regeneration happens
/// before needs; requested work/recovery is then validated against that staged
/// state and the resulting checkpoint receives a contiguous event sequence.
/// </summary>
public static class SurvivalFixture
{
    public static SurvivalCheckpoint CreateGenesis(
        SurvivalActor actor,
        IEnumerable<SurvivalResource> resources)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(resources);
        ValidateActor(actor);
        var orderedResources = resources.OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray();
        ValidateResources(orderedResources);
        return new SurvivalCheckpoint(0, actor, orderedResources, []);
    }

    public static SurvivalCheckpoint Advance(
        SurvivalCheckpoint checkpoint,
        SurvivalRules rules,
        SurvivalAction? requestedAction = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(rules);
        rules.Validate();
        ValidateCheckpoint(checkpoint);

        var action = requestedAction ?? SurvivalAction.Idle;
        ValidateAction(action);
        var nextTick = checked(checkpoint.WorldTick + 1);
        var pendingEvents = new List<(string Kind, string Detail)>();
        var regeneratedResources = ApplyPassiveRegeneration(checkpoint.Resources, nextTick, pendingEvents);
        var needsApplied = ApplyNeeds(checkpoint.Actor, rules, pendingEvents);
        var (actor, resources) = ApplyAction(needsApplied, regeneratedResources, action, rules, nextTick, pendingEvents);
        pendingEvents.Add(("action_committed", ToActionDetail(action)));

        var events = checkpoint.Events.ToList();
        foreach (var pending in pendingEvents)
        {
            events.Add(new SurvivalEvent(
                checked(events.Count + 1L),
                nextTick,
                pending.Kind,
                pending.Detail));
        }

        return new SurvivalCheckpoint(nextTick, actor, resources, events);
    }

    /// <summary>
    /// Replays the single durable action record from each committed tick and
    /// verifies the whole event slice, rather than trusting a saved state blob.
    /// </summary>
    public static SurvivalCheckpoint Replay(
        SurvivalCheckpoint genesis,
        SurvivalRules rules,
        IReadOnlyList<SurvivalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(events);
        if (genesis.WorldTick != 0 || genesis.Events.Count != 0)
        {
            throw new ArgumentException("Replay requires an event-free genesis checkpoint.", nameof(genesis));
        }

        var replay = genesis;
        foreach (var tickEvents in events.GroupBy(worldEvent => worldEvent.WorldTick).OrderBy(group => group.Key))
        {
            var expected = tickEvents.OrderBy(worldEvent => worldEvent.EventId).ToArray();
            if (expected.Length == 0 || expected[0].WorldTick != checked(replay.WorldTick + 1))
            {
                throw new InvalidDataException("Survival events must cover contiguous committed ticks.");
            }

            var actionEvent = expected.SingleOrDefault(worldEvent =>
                string.Equals(worldEvent.Kind, "action_committed", StringComparison.Ordinal));
            if (actionEvent is null)
            {
                throw new InvalidDataException("Every committed survival tick must contain one action record.");
            }

            replay = Advance(replay, rules, ParseAction(actionEvent.Detail));
            var actual = replay.Events.Skip(replay.Events.Count - expected.Length).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new InvalidDataException("Replaying the survival action log produced different events.");
            }
        }

        return replay;
    }

    private static SurvivalResource[] ApplyPassiveRegeneration(
        IReadOnlyList<SurvivalResource> resources,
        long nextTick,
        List<(string Kind, string Detail)> pendingEvents)
    {
        return resources.Select(resource =>
        {
            if (resource.State == SurvivalResourceState.Regenerating &&
                resource.RegenerationDueTick is { } dueTick && dueTick <= nextTick)
            {
                pendingEvents.Add(("resource_regenerated", resource.Id));
                return resource with
                {
                    Quantity = resource.MaximumQuantity,
                    State = SurvivalResourceState.Available,
                    RegenerationDueTick = null,
                };
            }

            return resource;
        }).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray();
    }

    private static SurvivalActor ApplyNeeds(
        SurvivalActor actor,
        SurvivalRules rules,
        List<(string Kind, string Detail)> pendingEvents)
    {
        var hunger = ClampBasisPoints(checked(actor.HungerBasisPoints - rules.HungerDrainPerTick));
        var energy = ClampBasisPoints(checked(actor.EnergyBasisPoints - rules.EnergyDrainPerTick));
        var healthDamage = 0;
        if (actor.HungerBasisPoints > 0 && hunger == 0)
        {
            pendingEvents.Add(("need_exhausted", "hunger:passive_drain"));
        }

        if (actor.EnergyBasisPoints > 0 && energy == 0)
        {
            pendingEvents.Add(("need_exhausted", "energy:passive_drain"));
        }

        if (hunger == 0)
        {
            healthDamage = checked(healthDamage + rules.StarvationDamagePerTick);
        }

        if (energy == 0)
        {
            healthDamage = checked(healthDamage + rules.ExhaustionDamagePerTick);
        }

        var health = ClampBasisPoints(checked(actor.HealthBasisPoints - healthDamage));
        if (healthDamage > 0)
        {
            pendingEvents.Add(("health_damaged", $"needs:{healthDamage.ToString(CultureInfo.InvariantCulture)}"));
        }

        return actor with
        {
            HungerBasisPoints = hunger,
            EnergyBasisPoints = energy,
            HealthBasisPoints = health,
        };
    }

    private static (SurvivalActor Actor, IReadOnlyList<SurvivalResource> Resources) ApplyAction(
        SurvivalActor actor,
        IReadOnlyList<SurvivalResource> resources,
        SurvivalAction action,
        SurvivalRules rules,
        long nextTick,
        List<(string Kind, string Detail)> pendingEvents)
    {
        return action.Kind switch
        {
            SurvivalActionKind.Idle => (actor, resources),
            SurvivalActionKind.Eat => (RecoverHunger(actor, rules, pendingEvents), resources),
            SurvivalActionKind.Sleep => (RecoverEnergy(actor, rules, pendingEvents), resources),
            SurvivalActionKind.Harvest => Harvest(actor, resources, action.ResourceId!, rules, nextTick, pendingEvents),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static SurvivalActor RecoverHunger(
        SurvivalActor actor,
        SurvivalRules rules,
        List<(string Kind, string Detail)> pendingEvents)
    {
        if (actor.FoodItems <= 0)
        {
            throw new InvalidOperationException("Eating requires an available food item.");
        }

        var hunger = ClampBasisPoints(checked(actor.HungerBasisPoints + rules.FoodRecovery));
        if (actor.HungerBasisPoints == 0 && hunger > 0)
        {
            pendingEvents.Add(("need_recovered", "hunger:eat"));
        }

        return actor with { HungerBasisPoints = hunger, FoodItems = actor.FoodItems - 1 };
    }

    private static SurvivalActor RecoverEnergy(
        SurvivalActor actor,
        SurvivalRules rules,
        List<(string Kind, string Detail)> pendingEvents)
    {
        var energy = ClampBasisPoints(checked(actor.EnergyBasisPoints + rules.SleepRecovery));
        if (actor.EnergyBasisPoints == 0 && energy > 0)
        {
            pendingEvents.Add(("need_recovered", "energy:sleep"));
        }

        return actor with { EnergyBasisPoints = energy };
    }

    private static (SurvivalActor Actor, IReadOnlyList<SurvivalResource> Resources) Harvest(
        SurvivalActor actor,
        IReadOnlyList<SurvivalResource> resources,
        string resourceId,
        SurvivalRules rules,
        long nextTick,
        List<(string Kind, string Detail)> pendingEvents)
    {
        var resource = resources.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, resourceId, StringComparison.Ordinal));
        if (resource is null || resource.State != SurvivalResourceState.Available || resource.Quantity <= 0)
        {
            throw new InvalidOperationException("Harvesting requires an available resource with remaining yield.");
        }

        var remaining = resource.Quantity - 1;
        var next = resource with { Quantity = remaining };
        if (remaining == 0)
        {
            next = resource.IsRenewable
                ? next with
                {
                    State = SurvivalResourceState.Regenerating,
                    RegenerationDueTick = checked(nextTick + rules.RenewableRegenerationTicks),
                }
                : next with { State = SurvivalResourceState.Depleted, RegenerationDueTick = null };
            pendingEvents.Add((
                resource.IsRenewable ? "resource_regeneration_scheduled" : "resource_depleted",
                resource.Id));
        }

        pendingEvents.Add(("resource_harvested", resource.Id));
        return (
            actor with { FoodItems = checked(actor.FoodItems + 1) },
            resources.Select(candidate => string.Equals(candidate.Id, resource.Id, StringComparison.Ordinal)
                    ? next
                    : candidate)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ValidateCheckpoint(SurvivalCheckpoint checkpoint)
    {
        if (checkpoint.WorldTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        ValidateActor(checkpoint.Actor);
        ValidateResources(checkpoint.Resources);
        var expectedId = 1L;
        var previousTick = 0L;
        foreach (var worldEvent in checkpoint.Events)
        {
            if (worldEvent.EventId != expectedId || worldEvent.WorldTick < previousTick ||
                worldEvent.WorldTick > checkpoint.WorldTick)
            {
                throw new InvalidDataException("Survival events are not a valid committed sequence.");
            }

            expectedId++;
            previousTick = worldEvent.WorldTick;
        }
    }

    private static void ValidateActor(SurvivalActor actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.Id);
        if (actor.FoodItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actor));
        }

        _ = RequireBasisPoints(actor.HungerBasisPoints, nameof(actor));
        _ = RequireBasisPoints(actor.EnergyBasisPoints, nameof(actor));
        _ = RequireBasisPoints(actor.HealthBasisPoints, nameof(actor));
    }

    private static void ValidateResources(IEnumerable<SurvivalResource> resources)
    {
        var ordered = resources.ToArray();
        if (ordered.Select(resource => resource.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            !ordered.SequenceEqual(ordered.OrderBy(resource => resource.Id, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Survival resources must have unique IDs in canonical order.");
        }

        foreach (var resource in ordered)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resource.Id);
            if (resource.Quantity < 0 || resource.MaximumQuantity <= 0 || resource.Quantity > resource.MaximumQuantity)
            {
                throw new ArgumentOutOfRangeException(nameof(resources));
            }

            if (resource.State == SurvivalResourceState.Available &&
                (resource.Quantity == 0 || resource.RegenerationDueTick is not null))
            {
                throw new InvalidDataException("An available resource must have yield and no regeneration timer.");
            }

            if (resource.State == SurvivalResourceState.Regenerating &&
                (!resource.IsRenewable || resource.Quantity != 0 || resource.RegenerationDueTick is null))
            {
                throw new InvalidDataException("A regenerating resource requires a renewable empty node and due tick.");
            }

            if ((resource.State == SurvivalResourceState.Depleted || resource.State == SurvivalResourceState.Transformed) &&
                (resource.Quantity != 0 || resource.RegenerationDueTick is not null))
            {
                throw new InvalidDataException("A terminal resource state cannot retain yield or a regeneration timer.");
            }
        }
    }

    private static void ValidateAction(SurvivalAction action)
    {
        if (action.Kind == SurvivalActionKind.Harvest)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(action.ResourceId);
        }
        else if (action.ResourceId is not null)
        {
            throw new ArgumentException("Only harvest actions may identify a resource.", nameof(action));
        }
    }

    private static int RequireBasisPoints(int value, string paramName) =>
        value is >= 0 and <= 10_000
            ? value
            : throw new ArgumentOutOfRangeException(paramName);

    private static int ClampBasisPoints(int value) => Math.Clamp(value, 0, 10_000);

    private static string ToActionDetail(SurvivalAction action) => action.Kind switch
    {
        SurvivalActionKind.Idle => "idle",
        SurvivalActionKind.Eat => "eat",
        SurvivalActionKind.Sleep => "sleep",
        SurvivalActionKind.Harvest => $"harvest:{action.ResourceId}",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static SurvivalAction ParseAction(string detail) => detail switch
    {
        "idle" => SurvivalAction.Idle,
        "eat" => SurvivalAction.Eat,
        "sleep" => SurvivalAction.Sleep,
        _ when detail.StartsWith("harvest:", StringComparison.Ordinal) && detail.Length > "harvest:".Length =>
            SurvivalAction.Harvest(detail["harvest:".Length..]),
        _ => throw new InvalidDataException("The survival action record is invalid."),
    };
}

/// <summary>
/// Canonical save format for the survival fixture. The codec is intentionally
/// text-only so test failures expose data rather than opaque serialization.
/// </summary>
public static class SurvivalCheckpointCodec
{
    private const string Header = "agentworld.survival-fixture/v1";

    public static byte[] Encode(SurvivalCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("world_tick=").Append(checkpoint.WorldTick).Append('\n');
        builder.Append("actor=").Append(checkpoint.Actor.Id).Append('|')
            .Append(checkpoint.Actor.HungerBasisPoints).Append('|')
            .Append(checkpoint.Actor.EnergyBasisPoints).Append('|')
            .Append(checkpoint.Actor.HealthBasisPoints).Append('|')
            .Append(checkpoint.Actor.FoodItems).Append('\n');
        foreach (var resource in checkpoint.Resources)
        {
            builder.Append("resource=").Append(resource.Id).Append('|')
                .Append(resource.IsRenewable ? "renewable" : "finite").Append('|')
                .Append(resource.Quantity).Append('|').Append(resource.MaximumQuantity).Append('|')
                .Append(ToWireState(resource.State)).Append('|')
                .Append(resource.RegenerationDueTick?.ToString(CultureInfo.InvariantCulture) ?? "-").Append('\n');
        }

        foreach (var worldEvent in checkpoint.Events)
        {
            builder.Append("event=").Append(worldEvent.EventId).Append('|')
                .Append(worldEvent.WorldTick).Append('|').Append(worldEvent.Kind).Append('|')
                .Append(worldEvent.Detail).Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static SurvivalCheckpoint Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The survival checkpoint format is not supported.");
        }

        var worldTick = ParseLong(ValueAfterPrefix(lines[1], "world_tick="));
        var actorParts = ValueAfterPrefix(lines[2], "actor=").Split('|', StringSplitOptions.None);
        if (actorParts.Length != 5)
        {
            throw new InvalidDataException("The saved survival actor is invalid.");
        }

        var actor = new SurvivalActor(
            actorParts[0],
            ParseInteger(actorParts[1]),
            ParseInteger(actorParts[2]),
            ParseInteger(actorParts[3]),
            ParseInteger(actorParts[4]));
        var resources = lines.Skip(3).Where(line => line.StartsWith("resource=", StringComparison.Ordinal))
            .Select(ParseResource).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray();
        var events = lines.Skip(3).Where(line => line.StartsWith("event=", StringComparison.Ordinal))
            .Select(ParseEvent).OrderBy(worldEvent => worldEvent.EventId).ToArray();
        var checkpoint = new SurvivalCheckpoint(worldTick, actor, resources, events);
        // Advance with no action is not used here: validation belongs to the
        // authoritative transition and is exercised by a no-op-free codec round trip.
        _ = SurvivalDigest.State(checkpoint);
        return checkpoint;
    }

    private static SurvivalResource ParseResource(string line)
    {
        var parts = ValueAfterPrefix(line, "resource=").Split('|', StringSplitOptions.None);
        if (parts.Length != 6)
        {
            throw new InvalidDataException("The saved survival resource is invalid.");
        }

        long? dueTick = parts[5] == "-" ? null : ParseLong(parts[5]);
        return new SurvivalResource(
            parts[0],
            parts[1] switch
            {
                "renewable" => true,
                "finite" => false,
                _ => throw new InvalidDataException("The saved resource renewability is invalid."),
            },
            ParseInteger(parts[2]),
            ParseInteger(parts[3]),
            ParseWireState(parts[4]),
            dueTick);
    }

    private static SurvivalEvent ParseEvent(string line)
    {
        var parts = ValueAfterPrefix(line, "event=").Split('|', StringSplitOptions.None);
        if (parts.Length != 4)
        {
            throw new InvalidDataException("The saved survival event is invalid.");
        }

        return new SurvivalEvent(ParseLong(parts[0]), ParseLong(parts[1]), parts[2], parts[3]);
    }

    private static string ValueAfterPrefix(string line, string prefix) =>
        line.StartsWith(prefix, StringComparison.Ordinal)
            ? line[prefix.Length..]
            : throw new InvalidDataException($"Expected survival checkpoint line '{prefix}'.");

    private static int ParseInteger(string value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException("A saved survival integer is invalid.");

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException("A saved survival long integer is invalid.");

    private static string ToWireState(SurvivalResourceState state) => state switch
    {
        SurvivalResourceState.Available => "available",
        SurvivalResourceState.Depleted => "depleted",
        SurvivalResourceState.Regenerating => "regenerating",
        SurvivalResourceState.Transformed => "transformed",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static SurvivalResourceState ParseWireState(string value) => value switch
    {
        "available" => SurvivalResourceState.Available,
        "depleted" => SurvivalResourceState.Depleted,
        "regenerating" => SurvivalResourceState.Regenerating,
        "transformed" => SurvivalResourceState.Transformed,
        _ => throw new InvalidDataException("The saved survival resource state is invalid."),
    };
}

public static class SurvivalDigest
{
    public static string State(SurvivalCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var canonical = Encoding.UTF8.GetString(SurvivalCheckpointCodec.Encode(checkpoint));
        var stateOnly = string.Join('\n', canonical.Split('\n')
            .Where(line => !line.StartsWith("event=", StringComparison.Ordinal)));
        return Digest(stateOnly);
    }

    public static string Events(IEnumerable<SurvivalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var canonical = string.Join('\n', events.OrderBy(worldEvent => worldEvent.EventId)
            .Select(worldEvent => $"{worldEvent.EventId}|{worldEvent.WorldTick}|{worldEvent.Kind}|{worldEvent.Detail}"));
        return Digest(canonical);
    }

    private static string Digest(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
