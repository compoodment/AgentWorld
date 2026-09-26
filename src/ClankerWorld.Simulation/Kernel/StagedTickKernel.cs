using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.Kernel;

/// <summary>
/// The total order that every authoritative first-world tick must follow.
/// </summary>
public enum KernelPhase
{
    Ingress = 1,
    ClockAndPassiveEffects = 2,
    NeedsAndHealth = 3,
    Lifecycle = 4,
    ReservationsAndMovement = 5,
    RoutineWorkAndEconomy = 6,
    CommunicationAndObservation = 7,
    CognitionQueue = 8,
    DecisionsAndControl = 9,
    Commit = 10,
}

/// <summary>
/// Saved integer clock arithmetic. No host wall-clock data participates.
/// </summary>
public sealed record KernelClock(long WorldTick, long DayIndex, int DayOfYear, int MinuteOfDay)
{
    public const int TicksPerMinute = 1;
    public const int TicksPerDay = 1_440;
    public const int DaysPerYear = 365;
    public const int ScheduledTicksPerSecond = 6;

    public static KernelClock FromWorldTick(long worldTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);

        var dayIndex = worldTick / TicksPerDay;
        return new KernelClock(
            worldTick,
            dayIndex,
            (int)(dayIndex % DaysPerYear),
            (int)(worldTick % TicksPerDay));
    }
}

/// <summary>
/// The deliberately small shared kernel state used to prove staged commit and
/// recovery rules before later world systems attach their own state.
/// </summary>
public sealed record KernelState(KernelClock Clock, int Counter, bool IsPaused, long RunEpoch)
{
    public static KernelState Genesis { get; } = new(KernelClock.FromWorldTick(0), 0, false, 0);
}

/// <summary>
/// A durable event is assigned an ID only when the enclosing tick commits.
/// </summary>
public sealed record KernelEvent(
    long EventId,
    long WorldTick,
    KernelPhase? Phase,
    string Kind,
    string Detail);

/// <summary>
/// The last complete state and its ordered durable event history.
/// </summary>
public sealed record KernelCheckpoint(KernelState State, IReadOnlyList<KernelEvent> Events)
{
    public static KernelCheckpoint Genesis { get; } = new(KernelState.Genesis, []);
}

/// <summary>
/// The only fixture work item currently performed in a staged tick. Later
/// issues replace this with validated movement, needs, inventory, and controls.
/// </summary>
public sealed record KernelTickInput(int CounterDelta);

/// <summary>
/// Test-only fault and control injection. Both are evaluated against the local
/// staged copy; neither can leak a partial tick into the committed checkpoint.
/// </summary>
public sealed record KernelTickOptions(
    KernelPhase? PauseRequestAtPhase = null,
    KernelPhase? InterruptAfterPhase = null);

public sealed record KernelInterruptedTick(
    KernelCheckpoint Checkpoint,
    KernelTickInput Input,
    KernelTickOptions Options,
    KernelPhase Boundary)
{
    public KernelTickResult Resume() =>
        StagedTickKernel.Advance(Checkpoint, Input, Options with { InterruptAfterPhase = null });
}

public sealed record KernelTickResult(KernelCheckpoint? Checkpoint, KernelInterruptedTick? Interrupted)
{
    public bool IsCommitted => Checkpoint is not null;

    public static KernelTickResult Committed(KernelCheckpoint checkpoint) => new(checkpoint, null);

    public static KernelTickResult InterruptedAt(KernelInterruptedTick interrupted) => new(null, interrupted);
}

/// <summary>
/// Executes one atomic kernel tick against a private staged copy. An injected
/// crash leaves only the prior checkpoint recoverable; a completed tick exposes
/// all ten phase records and its state together.
/// </summary>
public static class StagedTickKernel
{
    private static readonly KernelPhase[] OrderedPhases =
    [
        KernelPhase.Ingress,
        KernelPhase.ClockAndPassiveEffects,
        KernelPhase.NeedsAndHealth,
        KernelPhase.Lifecycle,
        KernelPhase.ReservationsAndMovement,
        KernelPhase.RoutineWorkAndEconomy,
        KernelPhase.CommunicationAndObservation,
        KernelPhase.CognitionQueue,
        KernelPhase.DecisionsAndControl,
        KernelPhase.Commit,
    ];

    public static KernelTickResult Advance(
        KernelCheckpoint checkpoint,
        KernelTickInput input,
        KernelTickOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(input);
        if (checkpoint.State.IsPaused)
        {
            throw new InvalidOperationException("A paused world must resume before it can advance.");
        }

        var execution = options ?? new KernelTickOptions();
        ValidateOptions(execution);
        var nextTick = checked(checkpoint.State.Clock.WorldTick + 1);
        var stagedState = checkpoint.State;
        var stagedEvents = checkpoint.Events.ToList();
        var nextEventId = checked(stagedEvents.Count + 1L);
        var pauseRequested = false;

        foreach (var phase in OrderedPhases)
        {
            stagedState = ApplyPhase(stagedState, input, phase, nextTick);
            stagedEvents.Add(CreateEvent(nextEventId++, nextTick, phase, "phase_completed", ToWireValue(phase)));

            if (execution.PauseRequestAtPhase == phase)
            {
                pauseRequested = true;
                stagedEvents.Add(CreateEvent(nextEventId++, nextTick, phase, "pause_requested", ToWireValue(phase)));
            }

            if (execution.InterruptAfterPhase == phase)
            {
                return KernelTickResult.InterruptedAt(
                    new KernelInterruptedTick(checkpoint, input, execution, phase));
            }
        }

        if (pauseRequested)
        {
            stagedState = stagedState with { IsPaused = true };
            stagedEvents.Add(CreateEvent(nextEventId, nextTick, KernelPhase.Commit, "paused", "after_tick_commit"));
        }

        return KernelTickResult.Committed(new KernelCheckpoint(stagedState, stagedEvents));
    }

    /// <summary>
    /// Resume creates a durable epoch boundary without advancing world time.
    /// Calling it while already running is intentionally idempotent.
    /// </summary>
    public static KernelCheckpoint Resume(KernelCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!checkpoint.State.IsPaused)
        {
            return checkpoint;
        }

        var state = checkpoint.State with
        {
            IsPaused = false,
            RunEpoch = checked(checkpoint.State.RunEpoch + 1),
        };
        var events = checkpoint.Events.ToList();
        events.Add(CreateEvent(
            checked(events.Count + 1L),
            state.Clock.WorldTick,
            null,
            "resumed",
            $"epoch:{state.RunEpoch}"));
        return new KernelCheckpoint(state, events);
    }

    private static KernelState ApplyPhase(
        KernelState state,
        KernelTickInput input,
        KernelPhase phase,
        long nextTick) =>
        phase switch
        {
            KernelPhase.ClockAndPassiveEffects => state with { Clock = KernelClock.FromWorldTick(nextTick) },
            KernelPhase.RoutineWorkAndEconomy => state with { Counter = checked(state.Counter + input.CounterDelta) },
            _ => state,
        };

    private static KernelEvent CreateEvent(
        long eventId,
        long worldTick,
        KernelPhase? phase,
        string kind,
        string detail) => new(eventId, worldTick, phase, kind, detail);

    private static void ValidateOptions(KernelTickOptions options)
    {
        if (options.PauseRequestAtPhase is { } pausePhase && !OrderedPhases.Contains(pausePhase))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.InterruptAfterPhase is { } interruptPhase && !OrderedPhases.Contains(interruptPhase))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private static string ToWireValue(KernelPhase phase) => phase switch
    {
        KernelPhase.Ingress => "ingress",
        KernelPhase.ClockAndPassiveEffects => "clock_and_passive_effects",
        KernelPhase.NeedsAndHealth => "needs_and_health",
        KernelPhase.Lifecycle => "lifecycle",
        KernelPhase.ReservationsAndMovement => "reservations_and_movement",
        KernelPhase.RoutineWorkAndEconomy => "routine_work_and_economy",
        KernelPhase.CommunicationAndObservation => "communication_and_observation",
        KernelPhase.CognitionQueue => "cognition_queue",
        KernelPhase.DecisionsAndControl => "decisions_and_control",
        KernelPhase.Commit => "commit",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };
}

/// <summary>
/// Canonical fixture digests for checking that staged execution and recovery
/// reach exactly the same state and ordered event history.
/// </summary>
public static class KernelDigest
{
    public static string State(KernelState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canonical = string.Join(
            '\n',
            "agentworld.kernel-state/v1",
            $"world_tick={state.Clock.WorldTick}",
            $"day_index={state.Clock.DayIndex}",
            $"day_of_year={state.Clock.DayOfYear}",
            $"minute_of_day={state.Clock.MinuteOfDay}",
            $"counter={state.Counter}",
            $"paused={state.IsPaused.ToString().ToLowerInvariant()}",
            $"run_epoch={state.RunEpoch}",
            string.Empty);
        return Digest(canonical);
    }

    public static string Events(IEnumerable<KernelEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var ordered = events.ToArray();
        var expectedId = 1L;
        var previousTick = 0L;
        var builder = new StringBuilder("agentworld.kernel-events/v1\n");
        foreach (var worldEvent in ordered)
        {
            if (worldEvent.EventId != expectedId || worldEvent.WorldTick < previousTick)
            {
                throw new InvalidDataException("Kernel events must have strictly increasing IDs and nondecreasing ticks.");
            }

            builder.Append(worldEvent.EventId).Append('|')
                .Append(worldEvent.WorldTick).Append('|')
                .Append(worldEvent.Phase?.ToString() ?? "control").Append('|')
                .Append(worldEvent.Kind).Append('|')
                .Append(worldEvent.Detail).Append('\n');
            expectedId++;
            previousTick = worldEvent.WorldTick;
        }

        return Digest(builder.ToString());
    }

    private static string Digest(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
