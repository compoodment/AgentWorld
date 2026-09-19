namespace AgentWorld.Simulation.Persistence;

/// <summary>
/// Applies the spike's ordered event log without consulting wall-clock time,
/// a provider, or a visual client.
/// </summary>
public static class WorldReplay
{
    public static MiniatureWorldState ReplayGenesis(
        WorldIdentity genesisIdentity,
        IEnumerable<PersistenceEvent> events) =>
        ApplyAll(MiniatureWorldState.Genesis(genesisIdentity), events);

    public static MiniatureWorldState ReplaySnapshotSuffix(
        WorldSnapshot snapshot,
        IEnumerable<PersistenceEvent> fullEventLog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(fullEventLog);

        return ApplyAll(
            snapshot.State,
            fullEventLog.Where(worldEvent => worldEvent.EventId > snapshot.LastEventId));
    }

    public static MiniatureWorldState ApplyAll(
        MiniatureWorldState state,
        IEnumerable<PersistenceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(events);

        foreach (var worldEvent in events)
        {
            state = Apply(state, worldEvent);
        }

        return state;
    }

    public static MiniatureWorldState Apply(MiniatureWorldState state, PersistenceEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(worldEvent);

        if (worldEvent.EventId != state.LastEventId + 1)
        {
            throw new InvalidDataException("Events must continue from the previous committed event ID.");
        }

        if (worldEvent.WorldTick < state.Identity.WorldTick)
        {
            throw new InvalidDataException("Events cannot move the world clock backwards.");
        }

        var identity = state.Identity with { WorldTick = worldEvent.WorldTick };
        var counter = checked(state.Counter + worldEvent.CounterDelta);

        if (worldEvent.Kind == PersistenceEventKind.MigrationApplied)
        {
            if (string.IsNullOrWhiteSpace(worldEvent.TargetSchemaVersion))
            {
                throw new InvalidDataException("A migration event must name its target schema version.");
            }

            identity = identity with { SchemaVersion = worldEvent.TargetSchemaVersion };
        }
        else if (worldEvent.TargetSchemaVersion is not null)
        {
            throw new InvalidDataException("Only migration events can change the schema version.");
        }

        return new MiniatureWorldState(identity, counter, worldEvent.EventId);
    }
}
