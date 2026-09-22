using AgentWorld.Simulation.Playtest;
using AgentWorld.Viewer.Observation;

namespace AgentWorld.Simulation.Tests;

public sealed class OwnerClientPresenceLeaseTests
{
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
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.TryAdvanceOnceAsync());
            Assert.Equal(1, runtime.WorldTick);
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
