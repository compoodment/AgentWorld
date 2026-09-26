using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class PrivateWorldDeferredCognitionTests
{
    [Fact]
    public async Task ActorEventRetainsItsLocationAcrossSaveAndViewerProjection()
    {
        using var world = new PrivateWorldRuntime("located-events");
        for (var tick = 0; tick < 12; tick++) await world.AdvanceOneTickAsync();
        var located = Assert.Single(world.ExportState().Events
            .Where(item => item.Kind == "inhabitant_moved")
            .Take(1));
        Assert.NotNull(located.Position);
        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(saved);
        var projected = new OwnerWorldObservationStore(restored).GetEventsAfter(0).Events
            .Single(item => item.EventId == located.EventId);
        Assert.Equal(located.Position!.Value.X, projected.Position?.X);
        Assert.Equal(located.Position.Value.Y, projected.Position?.Y);
    }

    [Fact]
    public async Task SlowHostedFounderDoesNotHoldWorldOrOtherFounders()
    {
        var hosted = new HeldHostedProvider();
        using var world = new PrivateWorldRuntime("deferred-founder", id =>
            id == "founder-scout" ? hosted : new DeterministicDecisionProvider());

        var first = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(first.Advanced);
        Assert.Equal(1, world.WorldTick);
        Assert.Contains(first.Decisions, item => item.InhabitantId != "founder-scout" && item.Admission.Accepted);
        await hosted.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains(world.ExportState().Society.Cognition.Queue, item => item.InhabitantId == "founder-scout");
        Assert.Contains(new OwnerWorldObservationStore(world).GetSnapshot().Inhabitants
                .Single(item => item.Id == "founder-scout").DecisionFactors,
            item => item.Key == "decision-pending");

        for (var tick = 0; tick < 3; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3))).Advanced);
        Assert.Equal(4, world.WorldTick);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");

        hosted.Release.TrySetResult(true);
        await hosted.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var admitted = await world.AdvanceOneTickNonBlockingAsync();
        Assert.True(admitted.Advanced);
        Assert.Contains(admitted.Decisions, item => item.InhabitantId == "founder-scout" && item.Admission.Accepted);
        Assert.Contains(admitted.Events, item => item.Kind == "hosted_decision_completed");
        Assert.DoesNotContain(world.ExportState().Society.Cognition.Queue, item => item.InhabitantId == "founder-scout");
    }

    [Fact]
    public async Task PausedRequestCannotActAndSavedQueueCanBeRetriedAfterReload()
    {
        var hosted = new HeldHostedProvider(ignoreCancellation: true);
        using var world = new PrivateWorldRuntime("deferred-reload", id =>
            id == "founder-scout" ? hosted : new DeterministicDecisionProvider());
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await hosted.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        world.Pause();
        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        hosted.Release.TrySetResult(true);
        await hosted.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");

        var replacement = new HeldHostedProvider();
        using var restored = PrivateWorldRuntime.Restore(saved, id =>
            id == "founder-scout" ? replacement : new DeterministicDecisionProvider());
        restored.Resume();
        Assert.True((await restored.AdvanceOneTickNonBlockingAsync()).Advanced);
        await replacement.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        replacement.Release.TrySetResult(true);
        await replacement.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var result = await restored.AdvanceOneTickNonBlockingAsync();
        Assert.Contains(result.Decisions, item => item.InhabitantId == "founder-scout" && item.Admission.Accepted);
    }

    private sealed class HeldHostedProvider(bool ignoreCancellation = false) : IDecisionProvider
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DecisionProviderKind Kind => DecisionProviderKind.Jev;
        public long ProviderEpoch => 1;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            if (ignoreCancellation) await Release.Task;
            else await Release.Task.WaitAsync(cancellationToken);
            var selected = request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            var probabilities = request.Observation.Candidates.ToDictionary(item => item.Id,
                item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal);
            Returned.TrySetResult(true);
            return new CognitionDecisionResponse(
                request.RequestId, request.Observation.InhabitantId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1d, probabilities);
        }
    }
}
