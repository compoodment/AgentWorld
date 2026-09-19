using AgentWorld.Simulation.Harness;

namespace AgentWorld.Simulation.Tests;

public sealed class LiveSeededWorldRuntimeTests
{
    [Fact]
    public void RuntimeAdvancesTheExistingFixtureToItsDeterministicTerminalState()
    {
        var runtime = new LiveSeededWorldRuntime("camp-alpha");
        var expected = ScriptedHarness.RunEntireSequence("camp-alpha");
        var committedActions = 0;

        while (runtime.TryAdvanceOneAction())
        {
            committedActions++;
        }

        var capture = runtime.Capture(afterEventId: 0);

        Assert.Equal(expected.Events.Count, committedActions);
        Assert.Equal(HarnessPersistence.StateDigest(expected), HarnessPersistence.StateDigest(capture.World));
        Assert.Equal(HarnessPersistence.EventDigest(expected), HarnessPersistence.EventDigest(capture.World));
        Assert.Equal(expected.Events, capture.Events);
        Assert.False(runtime.TryAdvanceOneAction());
    }

    [Fact]
    public void CaptureReturnsAnOrderedSuffixFromTheSameWorldState()
    {
        var runtime = new LiveSeededWorldRuntime("camp-alpha");
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.True(runtime.TryAdvanceOneAction());

        var capture = runtime.Capture(afterEventId: 1);

        Assert.Equal(3, capture.World.Identity.WorldTick);
        Assert.Equal(1, capture.AfterEventId);
        Assert.Equal([2L, 3L], capture.Events.Select(worldEvent => worldEvent.EventId));
        Assert.All(capture.Events, worldEvent => Assert.True(capture.World.Identity.WorldTick >= worldEvent.WorldTick));
    }
}
