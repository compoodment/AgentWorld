using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.Tests;

public sealed class DeterministicMovementTests
{
    [Fact]
    public void SameTargetContentionPrefersWaitTicksThenImmutableActorIdRegardlessOfInputOrder()
    {
        var actors = new[]
        {
            new MovementActor("alpha", new GridPoint(0, 0), 0),
            new MovementActor("bravo", new GridPoint(1, 1), 3),
        };
        var intents = new[]
        {
            new MovementIntent("alpha", new GridPoint(1, 0)),
            new MovementIntent("bravo", new GridPoint(1, 0)),
        };

        var first = DeterministicMovementResolver.Resolve(Map(), actors, intents);
        var reversed = DeterministicMovementResolver.Resolve(Map(), actors.Reverse(), intents.Reverse());

        Assert.Equal(new GridPoint(0, 0), first.GetActor("alpha").Position);
        Assert.Equal(1, first.GetActor("alpha").MoveWaitTicks);
        Assert.Equal(new GridPoint(1, 0), first.GetActor("bravo").Position);
        Assert.Equal(0, first.GetActor("bravo").MoveWaitTicks);
        Assert.Equal("destination_reserved", first.Events.Single(worldEvent => worldEvent.ActorId == "alpha").Reason);
        Assert.Equal(MovementDigest.State(first.Actors), MovementDigest.State(reversed.Actors));
        Assert.Equal(MovementDigest.Events(first.Events), MovementDigest.Events(reversed.Events));
    }

    [Fact]
    public void EqualWaitContentionUsesActorIdAsTheFinalTieBreak()
    {
        var resolution = DeterministicMovementResolver.Resolve(
            Map(),
            [
                new MovementActor("bravo", new GridPoint(1, 1), 2),
                new MovementActor("alpha", new GridPoint(0, 0), 2),
            ],
            [
                new MovementIntent("bravo", new GridPoint(1, 0)),
                new MovementIntent("alpha", new GridPoint(1, 0)),
            ]);

        Assert.Equal(new GridPoint(1, 0), resolution.GetActor("alpha").Position);
        Assert.Equal(new GridPoint(1, 1), resolution.GetActor("bravo").Position);
        Assert.Equal(3, resolution.GetActor("bravo").MoveWaitTicks);
    }

    [Fact]
    public void DirectReciprocalSwapIsLegalAndLongerCycleIsRejectedAtomically()
    {
        var swap = DeterministicMovementResolver.Resolve(
            Map(),
            [
                new MovementActor("alpha", new GridPoint(0, 0), 4),
                new MovementActor("bravo", new GridPoint(1, 0), 0),
            ],
            [
                new MovementIntent("bravo", new GridPoint(0, 0)),
                new MovementIntent("alpha", new GridPoint(1, 0)),
            ]);
        var cycle = DeterministicMovementResolver.Resolve(
            Map(),
            [
                new MovementActor("alpha", new GridPoint(0, 0), 0),
                new MovementActor("bravo", new GridPoint(1, 0), 0),
                new MovementActor("charlie", new GridPoint(1, 1), 0),
                new MovementActor("delta", new GridPoint(0, 1), 0),
            ],
            [
                new MovementIntent("alpha", new GridPoint(1, 0)),
                new MovementIntent("bravo", new GridPoint(1, 1)),
                new MovementIntent("charlie", new GridPoint(0, 1)),
                new MovementIntent("delta", new GridPoint(0, 0)),
            ]);

        Assert.Equal(new GridPoint(1, 0), swap.GetActor("alpha").Position);
        Assert.Equal(new GridPoint(0, 0), swap.GetActor("bravo").Position);
        Assert.All(swap.Actors, actor => Assert.Equal(0, actor.MoveWaitTicks));
        Assert.All(swap.Events, worldEvent => Assert.Equal("moved", worldEvent.Kind));

        Assert.Equal(new GridPoint(0, 0), cycle.GetActor("alpha").Position);
        Assert.Equal(new GridPoint(1, 0), cycle.GetActor("bravo").Position);
        Assert.Equal(new GridPoint(1, 1), cycle.GetActor("charlie").Position);
        Assert.Equal(new GridPoint(0, 1), cycle.GetActor("delta").Position);
        Assert.All(cycle.Actors, actor => Assert.Equal(1, actor.MoveWaitTicks));
        Assert.All(cycle.Events, worldEvent =>
        {
            Assert.Equal("movement_blocked", worldEvent.Kind);
            Assert.Equal("occupied_destination", worldEvent.Reason);
        });
    }

    [Fact]
    public void RouteCacheRecomputesWhenCapabilityOrTransportEpochChanges()
    {
        var cache = new NonAuthoritativeRouteCache();
        var baseline = new RouteCacheKey(
            new GridPoint(0, 0),
            new GridPoint(4, 1),
            "foot",
            0,
            0,
            0,
            "route-config/v1",
            "simulation/v1");

        var original = cache.GetOrCompute(Map(), baseline);
        var cached = cache.GetOrCompute(Map(), baseline);
        var afterInjury = cache.GetOrCompute(Map(), baseline with { ActorCapabilityEpoch = 1 });
        var afterTransport = cache.GetOrCompute(Map(), baseline with { TransportEpoch = 1 });

        Assert.Equal(original, cached);
        Assert.Equal(original, afterInjury);
        Assert.Equal(original, afterTransport);
        Assert.Equal(3, cache.ComputationCount);
    }

    private static SeededMap Map() => SeededMapGenerator.Generate("camp-alpha");
}
