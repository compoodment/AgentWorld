using AgentWorld.Simulation.Cognition;

namespace AgentWorld.Simulation.Society;

public sealed record PhaseFourWorldRuntimeState(
    int SchemaVersion,
    SocietyCheckpoint Society,
    SocietyCognitionSchedulerState Cognition);

public sealed record PhaseFourWorldCapture(
    SocietyCheckpoint Society,
    SocietyCognitionSchedulerState Cognition);

public sealed record PhaseFourDispatchCycleResult(
    SocietyCheckpoint Society,
    IReadOnlyList<SocietyCognitionDispatchResult> Decisions);

/// <summary>
/// The Phase 4 composition root. Society is the authoritative state boundary;
/// cognition is a derived, bounded service that is reconciled after every
/// lifecycle mutation. Saving this record captures both together without
/// allowing a provider or client to mutate society directly.
/// </summary>
public sealed class PhaseFourWorldRuntime : IDisposable
{
    public const int StateSchemaVersion = 1;

    private readonly SemaphoreSlim gate = new(1, 1);
    private SocietyCheckpoint society;
    private SocietyCognitionScheduler cognition;

    public PhaseFourWorldRuntime(
        SocietyCheckpoint checkpoint,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxCognitionQueueLength = 64,
        int maxCognitionDispatchPerCycle = 8,
        double minimumCognitionConfidence = 0.5)
    {
        SocietyFixture.Validate(checkpoint);
        society = checkpoint;
        cognition = new SocietyCognitionScheduler(
            checkpoint.Inhabitants,
            providerFactory,
            maxCognitionQueueLength,
            maxCognitionDispatchPerCycle,
            minimumCognitionConfidence);
    }

    public SocietyCheckpoint Checkpoint => society;

    public PhaseFourWorldCapture Capture()
    {
        gate.Wait();
        try
        {
            return new PhaseFourWorldCapture(society, cognition.ExportState());
        }
        finally
        {
            gate.Release();
        }
    }

    public PhaseFourWorldRuntimeState ExportState()
    {
        var capture = Capture();
        return new PhaseFourWorldRuntimeState(StateSchemaVersion, capture.Society, capture.Cognition);
    }

    public static PhaseFourWorldRuntime Restore(
        PhaseFourWorldRuntimeState state,
        Func<string, IDecisionProvider>? providerFactory = null,
        double minimumCognitionConfidence = 0.5)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != StateSchemaVersion)
        {
            throw new InvalidDataException("The Phase 4 runtime state schema is unsupported.");
        }

        SocietyFixture.Validate(state.Society);
        var scheduler = SocietyCognitionScheduler.Restore(
            state.Cognition,
            providerFactory,
            minimumCognitionConfidence);
        scheduler.SyncInhabitants(state.Society.Inhabitants);
        var runtime = new PhaseFourWorldRuntime(
            state.Society,
            providerFactory,
            state.Cognition.MaxQueueLength,
            state.Cognition.MaxDispatchPerCycle,
            minimumCognitionConfidence)
        {
            cognition = scheduler,
        };
        runtime.Validate();
        return runtime;
    }

    public SocietyOperationResult Apply(Func<SocietyCheckpoint, SocietyOperationResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        gate.Wait();
        try
        {
            var result = operation(society);
            SocietyFixture.Validate(result.Checkpoint);
            society = result.Checkpoint;
            cognition.SyncInhabitants(society.Inhabitants);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public SocietyOperationResult AdvanceTo(long targetTick) =>
        Apply(checkpoint => SocietyFixture.AdvanceTo(checkpoint, targetTick));

    public SocietyOperationResult Pause() => Apply(SocietyFixture.Pause);

    public SocietyOperationResult Resume() => Apply(SocietyFixture.Resume);

    public bool EnqueueCognition(SocietyCognitionScheduleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        gate.Wait();
        try
        {
            var inhabitant = society.GetInhabitant(entry.InhabitantId);
            if (inhabitant.Status != SocietyInhabitantStatus.Active)
            {
                throw new InvalidOperationException("Dead inhabitants cannot receive cognition work.");
            }

            if (entry.Observation.WorldTick > society.WorldTick)
            {
                throw new InvalidOperationException("Cognition cannot observe beyond the authoritative world tick.");
            }

            return cognition.Enqueue(entry);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<PhaseFourDispatchCycleResult> DispatchCognitionAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var decisions = await cognition.DispatchAsync(cancellationToken).ConfigureAwait(false);
            return new PhaseFourDispatchCycleResult(society, decisions);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Validate()
    {
        SocietyFixture.Validate(society);
        cognition.Validate();
        var activeIds = society.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal);
        if (!activeIds.SequenceEqual(cognition.InhabitantIds.OrderBy(item => item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Phase 4 society and cognition runtime populations disagree.");
        }
    }

    public void Dispose() => gate.Dispose();
}
