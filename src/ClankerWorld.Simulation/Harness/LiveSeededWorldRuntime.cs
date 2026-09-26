using ClankerWorld.Simulation.Persistence;

namespace ClankerWorld.Simulation.Harness;

/// <summary>
/// A coherent server-side capture: immutable world state plus the durable event
/// suffix requested by a reconnecting client.
/// </summary>
public sealed record LiveWorldCapture(
    HarnessWorld World,
    long AfterEventId,
    IReadOnlyList<PersistenceEvent> Events);

/// <summary>
/// Owns one mutable execution of the deterministic camp-alpha fixture. The
/// runtime is intentionally in the simulation layer; viewers receive only
/// projections of captures and never this object or its state records.
/// </summary>
public sealed class LiveSeededWorldRuntime
{
    private readonly object sync = new();
    private HarnessWorld world;

    public LiveSeededWorldRuntime(string worldSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        world = ScriptedHarness.CreateGenesis(worldSeed);
    }

    /// <summary>
    /// Advances one complete scripted action while work remains. Once the
    /// fixture has reached its terminal state, it holds that durable state
    /// rather than inventing client-visible mutations.
    /// </summary>
    public bool TryAdvanceOneAction()
    {
        lock (sync)
        {
            if (!ScriptedHarness.TryAdvanceOneAction(world, out var advanced))
            {
                return false;
            }

            world = advanced;
            return true;
        }
    }

    /// <summary>
    /// Captures the current state and ordered suffix under one lock so a
    /// reconnect baseline cannot combine a newer snapshot with an older log.
    /// </summary>
    public LiveWorldCapture Capture(long afterEventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterEventId);

        lock (sync)
        {
            return new LiveWorldCapture(
                world,
                afterEventId,
                world.Events
                    .Where(worldEvent => worldEvent.EventId > afterEventId)
                    .OrderBy(worldEvent => worldEvent.EventId)
                    .ToArray());
        }
    }
}
