using AgentWorld.Simulation.Harness;

namespace AgentWorld.Simulation.Tests;

public sealed class SeededHarnessTests
{
    public static IEnumerable<object[]> SeedCorpus =>
    [
        ["camp-alpha", "474c407c3d48c4dac77e345651f1b722a2ddcff9125758a0296397ce542018fc"],
        ["camp-beta", "4c507e58e904d0027cd581b5373f63c95be9a35c81031e5a8c503ab1bffcef8b"],
        ["camp-gamma", "db597df62ba46f22ff21957ada2f9203b0246c35882eacabf3f1fd48a4038526"],
    ];

    [Theory]
    [MemberData(nameof(SeedCorpus))]
    public void FixedSeedCorpusProducesValidCanonicalMapManifest(string seed, string expectedManifestDigest)
    {
        var first = SeededMapGenerator.Generate(seed);
        var second = SeededMapGenerator.Generate(seed);

        Assert.True(MapAcceptance.Validate(first).IsValid);
        Assert.Equal(expectedManifestDigest, first.ManifestDigest);
        Assert.Equal(first.ManifestDigest, second.ManifestDigest);
        Assert.True(MapManifestCodec.Encode(first).SequenceEqual(MapManifestCodec.Encode(second)));
    }

    [Fact]
    public void EqualCostRouteUsesTheDeclaredStableTieBreakOrder()
    {
        var map = SeededMapGenerator.Generate("camp-alpha");
        var origin = new GridPoint(0, 0);
        var destination = new GridPoint(2, 1);
        var expected = new[]
        {
            new GridPoint(0, 0),
            new GridPoint(1, 0),
            new GridPoint(2, 0),
            new GridPoint(2, 1),
        };

        for (var run = 0; run < 10; run++)
        {
            Assert.Equal(expected, DeterministicRouteFinder.Find(map, origin, destination));
        }
    }

    [Fact]
    public void ScriptedActorMovesHarvestsConsumesAndSleepsInOrderedTicks()
    {
        var genesis = ScriptedHarness.CreateGenesis("camp-alpha");
        var final = ScriptedHarness.RunEntireSequence("camp-alpha");

        Assert.Equal(final.Map.GetObject("bedroll").Position, final.Actor.Position);
        Assert.Equal(ResourceState.Depleted, final.GetResource("berry-patch").State);
        Assert.Equal(0, final.Actor.FoodItems);
        Assert.True(final.Actor.HungerBasisPoints > genesis.Actor.HungerBasisPoints);
        Assert.True(final.Actor.EnergyBasisPoints > genesis.Actor.EnergyBasisPoints);
        Assert.Equal(
            Enumerable.Range(1, final.Events.Count).Select(number => (long)number),
            final.Events.Select(worldEvent => worldEvent.EventId));
        Assert.Equal(
            Enumerable.Range(1, final.Events.Count).Select(number => (long)number),
            final.Events.Select(worldEvent => worldEvent.WorldTick));
        Assert.Contains(final.Events, worldEvent => worldEvent.Detail == "harvest:berry-patch");
        Assert.Contains(final.Events, worldEvent => worldEvent.Detail == "consume:actor-scout");
        Assert.Equal("sleep:actor-scout", final.Events[^1].Detail);
    }

    [Fact]
    public void SameSeedAndScriptProduceIdenticalCanonicalStateAndEventDigests()
    {
        var first = ScriptedHarness.RunEntireSequence("camp-beta");
        var second = ScriptedHarness.RunEntireSequence("camp-beta");

        Assert.Equal(HarnessPersistence.StateDigest(first), HarnessPersistence.StateDigest(second));
        Assert.Equal(HarnessPersistence.EventDigest(first), HarnessPersistence.EventDigest(second));
    }

    [Fact]
    public void SaveReloadAndPhysicalReplayProduceTheSameFinalDigestsAsTheCleanRun()
    {
        var clean = ScriptedHarness.RunEntireSequence("camp-gamma");
        var beforeSave = ScriptedHarness.RunToFoodConsumed(ScriptedHarness.CreateGenesis("camp-gamma"));
        var save = HarnessPersistence.Save(beforeSave);
        var loaded = HarnessPersistence.Load(save);
        var resumed = ScriptedHarness.FinishAfterFood(loaded);
        var persistedFinal = HarnessPersistence.Save(resumed);

        Assert.Equal(HarnessPersistence.StateDigest(clean), HarnessPersistence.StateDigest(resumed));
        Assert.Equal(HarnessPersistence.EventDigest(clean), HarnessPersistence.EventDigest(resumed));
        Assert.True(
            persistedFinal.SnapshotBytes.SequenceEqual(HarnessPersistence.Save(resumed).SnapshotBytes));
        Assert.True(
            persistedFinal.EventLogBytes.SequenceEqual(HarnessPersistence.Save(resumed).EventLogBytes));
    }
}
