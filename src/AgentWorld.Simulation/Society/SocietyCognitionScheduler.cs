using AgentWorld.Simulation.Cognition;

namespace AgentWorld.Simulation.Society;

public sealed record SocietyCognitionScheduleEntry(
    string ScheduleId,
    string InhabitantId,
    int Priority,
    long EnqueuedTick,
    IReadOnlyList<string> TriggerIds,
    InhabitantObservation Observation);

public sealed record SocietyCognitionSchedulerEvent(
    long EventId,
    long WorldTick,
    string Kind,
    string Detail);

public sealed record SocietyCognitionDispatchResult(
    string InhabitantId,
    CognitionAdmissionResult Admission);

public sealed record SocietyCognitionSchedulerState(
    int SchemaVersion,
    int MaxQueueLength,
    int MaxDispatchPerCycle,
    IReadOnlyList<SocietyCognitionScheduleEntry> Queue,
    IReadOnlyList<CognitionRuntimeState> Runtimes,
    IReadOnlyList<SocietyCognitionSchedulerEvent> Events);

/// <summary>
/// Fair multi-inhabitant cognition admission. Each inhabitant has one
/// CognitionRuntime and therefore one in-flight request; the shared queue is
/// bounded and ordered by priority, age, inhabitant ID, and schedule ID.
/// </summary>
public sealed class SocietyCognitionScheduler
{
    public const int StateSchemaVersion = 1;

    private readonly Dictionary<string, CognitionRuntime> runtimes;
    private readonly List<SocietyCognitionScheduleEntry> queue = [];
    private readonly List<SocietyCognitionSchedulerEvent> events = [];
    private readonly int maxQueueLength;
    private readonly int maxDispatchPerCycle;
    private readonly Func<string, IDecisionProvider> providerFactory;
    private readonly double minimumConfidence;
    private long nextEventId = 1;

    public SocietyCognitionScheduler(
        IEnumerable<SocietyInhabitant> inhabitants,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxQueueLength = 64,
        int maxDispatchPerCycle = 8,
        double minimumConfidence = 0.5)
    {
        ArgumentNullException.ThrowIfNull(inhabitants);
        if (maxQueueLength <= 0 || maxDispatchPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQueueLength));
        }

        this.maxQueueLength = maxQueueLength;
        this.maxDispatchPerCycle = maxDispatchPerCycle;
        this.providerFactory = providerFactory ?? (_ => new DeterministicDecisionProvider());
        this.minimumConfidence = minimumConfidence;
        runtimes = inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Id,
                item => new CognitionRuntime(item.Id, this.providerFactory(item.Id), minimumConfidence),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<string> InhabitantIds => runtimes.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();

    public bool Enqueue(SocietyCognitionScheduleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);
        if (!runtimes.ContainsKey(entry.InhabitantId))
        {
            throw new InvalidOperationException($"Unknown inhabitant '{entry.InhabitantId}'.");
        }

        var existing = queue.SingleOrDefault(item => item.InhabitantId == entry.InhabitantId);
        if (existing is not null)
        {
            var merged = existing with
            {
                Priority = Math.Max(existing.Priority, entry.Priority),
                EnqueuedTick = Math.Min(existing.EnqueuedTick, entry.EnqueuedTick),
                TriggerIds = existing.TriggerIds.Concat(entry.TriggerIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                Observation = entry.Observation,
            };
            queue[queue.IndexOf(existing)] = merged;
            AppendEvent(entry.EnqueuedTick, "cognition_trigger_coalesced", entry.InhabitantId);
            return true;
        }

        if (queue.Count >= maxQueueLength)
        {
            AppendEvent(entry.EnqueuedTick, "cognition_backpressure", entry.ScheduleId);
            return false;
        }

        queue.Add(entry);
        AppendEvent(entry.EnqueuedTick, "cognition_queued", entry.ScheduleId);
        return true;
    }

    /// <summary>
    /// Reconciles cognition runtimes with the authoritative lifecycle set.
    /// Dead inhabitants cannot receive new work, and newborns get a fresh
    /// provider binding without disturbing surviving runtimes or queued work.
    /// </summary>
    public void SyncInhabitants(IEnumerable<SocietyInhabitant> inhabitants)
    {
        ArgumentNullException.ThrowIfNull(inhabitants);
        var activeIds = inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var removedId in runtimes.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            runtimes.Remove(removedId);
            queue.RemoveAll(entry => entry.InhabitantId == removedId);
            AppendEvent(0, "cognition_runtime_removed", removedId);
        }

        foreach (var inhabitant in inhabitants
                     .Where(item => item.Status == SocietyInhabitantStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (runtimes.ContainsKey(inhabitant.Id))
            {
                continue;
            }

            runtimes.Add(
                inhabitant.Id,
                new CognitionRuntime(inhabitant.Id, providerFactory(inhabitant.Id), minimumConfidence));
            AppendEvent(0, "cognition_runtime_added", inhabitant.Id);
        }
    }

    public async ValueTask<IReadOnlyList<SocietyCognitionDispatchResult>> DispatchAsync(
        CancellationToken cancellationToken = default)
    {
        var selected = queue
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.EnqueuedTick)
            .ThenBy(item => item.InhabitantId, StringComparer.Ordinal)
            .ThenBy(item => item.ScheduleId, StringComparer.Ordinal)
            .Take(maxDispatchPerCycle)
            .ToArray();

        var results = new List<SocietyCognitionDispatchResult>(selected.Length);
        foreach (var entry in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runtime = runtimes[entry.InhabitantId];
            CognitionAdmissionResult admission;
            try
            {
                admission = await runtime.RequestAndDecideAsync(entry.Observation, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                admission = new CognitionAdmissionResult(false, false, exception.Message, null);
            }

            if (string.Equals(admission.Outcome, "provider_cancelled", StringComparison.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            // A queue entry is durable until its dispatch attempt has
            // completed. If cancellation interrupts the provider call, the
            // current entry and every unprocessed selected entry remain in
            // the queue for a later retry.
            queue.Remove(entry);
            results.Add(new SocietyCognitionDispatchResult(entry.InhabitantId, admission));
            AppendEvent(
                entry.Observation.WorldTick,
                admission.Accepted ? "cognition_dispatched" : "cognition_dispatch_rejected",
                $"{entry.InhabitantId}:{admission.Outcome}");
        }

        return results;
    }

    public CognitionRuntimeSnapshot CaptureRuntime(string inhabitantId) =>
        GetRuntime(inhabitantId).Capture();

    public SocietyCognitionSchedulerState ExportState()
    {
        var runtimeStates = runtimes.Values
            .OrderBy(runtime => runtime.InhabitantId, StringComparer.Ordinal)
            .Select(runtime => runtime.ExportState())
            .ToArray();
        return new SocietyCognitionSchedulerState(
            StateSchemaVersion,
            maxQueueLength,
            maxDispatchPerCycle,
            queue.OrderBy(item => item.ScheduleId, StringComparer.Ordinal).ToArray(),
            runtimeStates,
            events.ToArray());
    }

    public static SocietyCognitionScheduler Restore(
        SocietyCognitionSchedulerState state,
        Func<string, IDecisionProvider>? providerFactory = null,
        double minimumConfidence = 0.5)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != StateSchemaVersion ||
            state.MaxQueueLength <= 0 || state.MaxDispatchPerCycle <= 0)
        {
            throw new InvalidDataException("The society cognition scheduler state is invalid.");
        }

        var inhabitants = state.Runtimes
            .Select(item => new SocietyInhabitant(
                item.InhabitantId,
                item.InhabitantId,
                0,
                SocietyInhabitantStatus.Active,
                SocietyAgeBand.Adult,
                10_000,
                null,
                null,
                SocietyWorkRole.Unassigned,
                0))
            .ToArray();
        var scheduler = new SocietyCognitionScheduler(
            inhabitants,
            providerFactory,
            state.MaxQueueLength,
            state.MaxDispatchPerCycle,
            minimumConfidence);
        scheduler.queue.AddRange(state.Queue);
        foreach (var runtimeState in state.Runtimes)
        {
            if (!scheduler.runtimes.ContainsKey(runtimeState.InhabitantId))
            {
                throw new InvalidDataException("A scheduler runtime has no matching inhabitant.");
            }

            scheduler.runtimes[runtimeState.InhabitantId] = CognitionRuntime.Restore(
                runtimeState,
                providerFactory?.Invoke(runtimeState.InhabitantId),
                minimumConfidence);
        }

        scheduler.events.AddRange(state.Events);
        scheduler.nextEventId = checked(scheduler.events.Count + 1L);
        scheduler.Validate();
        return scheduler;
    }

    public void Validate()
    {
        if (queue.Count > maxQueueLength)
        {
            throw new InvalidDataException("The society cognition queue exceeds its configured limit.");
        }

        var expected = 1L;
        var previousTick = 0L;
        foreach (var schedulerEvent in events)
        {
            if (schedulerEvent.EventId != expected || schedulerEvent.WorldTick < previousTick)
            {
                throw new InvalidDataException("Society cognition events are not ordered.");
            }

            expected++;
            previousTick = schedulerEvent.WorldTick;
        }

        foreach (var entry in queue)
        {
            ValidateEntry(entry);
            if (!runtimes.ContainsKey(entry.InhabitantId))
            {
                throw new InvalidDataException("A queued cognition entry references an unknown inhabitant.");
            }
        }
    }

    private CognitionRuntime GetRuntime(string inhabitantId)
    {
        var id = string.IsNullOrWhiteSpace(inhabitantId) ? inhabitantId : inhabitantId.Trim();
        return runtimes.TryGetValue(id, out var runtime)
            ? runtime
            : throw new KeyNotFoundException($"Unknown inhabitant '{id}'.");
    }

    private void AppendEvent(long worldTick, string kind, string detail)
    {
        var committedTick = events.Count == 0
            ? worldTick
            : Math.Max(worldTick, events[^1].WorldTick);
        events.Add(new SocietyCognitionSchedulerEvent(
            checked(nextEventId++),
            committedTick,
            kind,
            detail));
    }

    private static void ValidateEntry(SocietyCognitionScheduleEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ScheduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.InhabitantId);
        if (entry.Priority < 0 || entry.EnqueuedTick < 0 || entry.TriggerIds.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entry));
        }

        entry.Observation.Validate();
        if (entry.Observation.InhabitantId != entry.InhabitantId ||
            entry.TriggerIds.Any(string.IsNullOrWhiteSpace) ||
            entry.TriggerIds.Distinct(StringComparer.Ordinal).Count() != entry.TriggerIds.Count)
        {
            throw new InvalidDataException("A cognition schedule entry has invalid identity or triggers.");
        }
    }
}
