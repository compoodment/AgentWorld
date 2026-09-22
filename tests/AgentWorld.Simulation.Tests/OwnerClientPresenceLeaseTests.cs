using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Viewer.Observation;

namespace AgentWorld.Simulation.Tests;

public sealed class OwnerClientPresenceLeaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisconnectOrPauseCancelsAnInFlightProviderWithoutAdvancing(bool manualPause)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"agentworld-presence-cancel-{Guid.NewGuid():N}");
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            var provider = new WaitingProvider();
            using var runtime = new PrivateWorldRuntime("presence-cancel", _ => provider);
            using var service = new PrivateWorldRuntimeService(runtime,
                new PrivateWorldStateFile(Path.Combine(directory, "world.json")), presence);
            presence.RecordAuthenticatedReconnect("owner");
            var tick = service.TryAdvanceOnceAsync().AsTask();
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (manualPause)
            {
                runtime.Pause();
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(5));
            }
            var expected = PrivateWorldRuntimeCodec.Encode(runtime.ExportState());
            Assert.False(await tick.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(0, runtime.WorldTick);
            Assert.Equal(expected, PrivateWorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(manualPause, runtime.Society.IsPaused);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class WaitingProvider : IDecisionProvider
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The test provider must be cancelled.");
        }
    }

    [Fact]
    public void AuthenticatedReconnectLeaseExpiresAndSupportsMultipleDevices()
    {
        var clock = new ManualTimeProvider();
        var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);

        Assert.False(presence.HasActiveClient);

        presence.RecordAuthenticatedReconnect("device-one");
        Assert.True(presence.HasActiveClient);
        Assert.Equal(1, presence.ActiveClientCount);

        clock.Advance(TimeSpan.FromSeconds(3));
        presence.RecordAuthenticatedReconnect("device-two");
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.True(presence.HasActiveClient);
        Assert.Equal(1, presence.ActiveClientCount);

        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(presence.HasActiveClient);
        Assert.Equal(0, presence.ActiveClientCount);
    }

    [Fact]
    public async Task PrivateWorldAdvancesOnlyDuringPresenceAndStillHonorsManualPause()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"agentworld-client-presence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            using var runtime = new PrivateWorldRuntime("client-presence-seed");
            var stateFile = new PrivateWorldStateFile(Path.Combine(directory, "world.json"));
            var logger = new RecordingLogger<PrivateWorldRuntimeService>();
            var service = new PrivateWorldRuntimeService(runtime, stateFile, presence, logger);

            Assert.False(await service.TryAdvanceOnceAsync());
            Assert.Equal(0, runtime.WorldTick);

            presence.RecordAuthenticatedReconnect("device-one");
            Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Equal(1, runtime.WorldTick);

            runtime.Pause();
            Assert.False(await service.TryAdvanceOnceAsync());
            Assert.Equal(1, runtime.WorldTick);
            Assert.Contains(logger.Messages, message =>
                message.Contains("world_tick_gate state=waiting_for_client", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("world_tick_gate state=advancing", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("world_tick_gate state=paused", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("cognition_decision tick=1", StringComparison.Ordinal));

            runtime.Resume();
            for (var tick = 0; tick < 20; tick++)
            {
                Assert.True(await service.TryAdvanceOnceAsync());
            }
            Assert.Contains(logger.Messages, message => message.Contains("settlement_activity", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("Cooking fire", StringComparison.Ordinal));
            var tickBeforeDisconnect = runtime.WorldTick;
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.TryAdvanceOnceAsync());
            Assert.Equal(tickBeforeDisconnect, runtime.WorldTick);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration) => timestamp = checked(timestamp + duration.Ticks);
    }
}
