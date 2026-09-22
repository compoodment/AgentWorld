using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Playtest;
using AgentWorld.Viewer.Observation;

namespace AgentWorld.Simulation.Tests;

public sealed class ViewerObservationTests
{
    private static readonly HashSet<string> ResourceStates =
    [
        "available",
        "depleted",
    ];

    [Fact]
    public void HandshakeDeclaresTheReadOnlyObservationCapabilities()
    {
        var store = new SeededWorldObservationStore();

        var handshake = store.GetHandshake();

        Assert.Equal(new ProtocolVersion(1, 0), handshake.Protocol);
        Assert.Equal(
            ["event-replay.read.v1", "reconnect-baseline.read.v1", "seeded-map.read.v1", "snapshot.read.v1"],
            handshake.ServerCapabilities.OrderBy(capability => capability, StringComparer.Ordinal));
        Assert.Equal(
            ["event-replay.read.v1", "reconnect-baseline.read.v1", "snapshot.read.v1"],
            handshake.ClientCapabilities.OrderBy(capability => capability, StringComparer.Ordinal));
    }

    [Fact]
    public void SnapshotProjectsTheCompletedHarnessWithoutExposingItsRecords()
    {
        var source = ScriptedHarness.RunEntireSequence(SeededWorldObservationStore.SampleSeed);
        var store = new SeededWorldObservationStore(source);

        var snapshot = store.GetSnapshot();

        Assert.Equal(source.Identity.WorldId, snapshot.WorldId);
        Assert.Equal(source.Identity.WorldTick, snapshot.WorldTick);
        Assert.Equal(source.Map.ManifestDigest, snapshot.MapManifestDigest);
        Assert.Equal(source.Map.Width * source.Map.Height, snapshot.Tiles.Count);
        Assert.Equal(source.Actor.Id, snapshot.Actor.Id);
        Assert.Equal(source.Actor.Position.X, snapshot.Actor.Position.X);
        Assert.Equal(source.Actor.Position.Y, snapshot.Actor.Position.Y);
        Assert.Equal(source.Events[^1].EventId, snapshot.LatestEventId);
        Assert.Equal(
            snapshot.Tiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X),
            snapshot.Tiles);
        Assert.All(snapshot.Resources, resource => Assert.Contains(resource.State, ResourceStates));

        var mutableTiles = Assert.IsType<ViewerTile[]>(snapshot.Tiles);
        mutableTiles[0] = mutableTiles[0] with { Terrain = "corrupted-client-copy" };

        Assert.Equal("meadow", store.GetSnapshot().Tiles[0].Terrain);
    }

    [Fact]
    public void EventCursorReturnsOnlyTheOrderedSuffixAtTheSameSnapshotTick()
    {
        var store = new SeededWorldObservationStore();
        var snapshot = store.GetSnapshot();

        var allEvents = store.GetEventsAfter(0);
        var suffix = store.GetEventsAfter(3);

        Assert.Equal(snapshot.WorldTick, allEvents.SnapshotTick);
        Assert.Equal(snapshot.WorldTick, suffix.SnapshotTick);
        Assert.Equal(0, allEvents.AfterEventId);
        Assert.Equal(3, suffix.AfterEventId);
        Assert.Equal(
            Enumerable.Range(1, allEvents.Events.Count).Select(eventId => (long)eventId),
            allEvents.Events.Select(worldEvent => worldEvent.EventId));
        Assert.Equal(allEvents.Events.Skip(3), suffix.Events);
        Assert.All(suffix.Events, worldEvent => Assert.True(worldEvent.EventId > suffix.AfterEventId));
    }

    [Fact]
    public void NegativeEventCursorIsRejectedBeforeProjection()
    {
        var store = new SeededWorldObservationStore();

        Assert.Throws<ArgumentOutOfRangeException>(() => store.GetEventsAfter(-1));
    }

    [Fact]
    public void ReconnectBaselineProjectsOneCoherentLiveRuntimeCapture()
    {
        var runtime = new LiveSeededWorldRuntime(SeededWorldObservationStore.SampleSeed);
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.True(runtime.TryAdvanceOneAction());
        var store = new SeededWorldObservationStore(runtime);

        var baseline = store.GetReconnectBaseline(afterEventId: 1);

        Assert.Equal(baseline.Snapshot.WorldTick, baseline.Events.SnapshotTick);
        Assert.Equal(3, baseline.Snapshot.LatestEventId);
        Assert.Equal([2L, 3L], baseline.Events.Events.Select(worldEvent => worldEvent.EventId));
        Assert.All(baseline.Events.Events, worldEvent => Assert.True(worldEvent.EventId > baseline.Events.AfterEventId));
    }

    [Fact]
    public void OwnerInhabitantKnowledgeIsBoundedToLocalPerceptionAndItsCommittedRoute()
    {
        var runtime = new OwnerWorldRuntime("camp-alpha");
        var store = new OwnerWorldObservationStore(runtime);

        var snapshot = store.GetSnapshot();
        var inhabitant = Assert.Single(snapshot.Inhabitants);
        var knowledge = inhabitant.SpatialKnowledge;

        Assert.Equal(inhabitant.Position, knowledge.CurrentTile);
        Assert.All(knowledge.PerceivedTiles, tile => Assert.Contains(tile, knowledge.KnownTiles));
        Assert.All(inhabitant.Route.Steps, step => Assert.Contains(step, knowledge.KnownTiles));
        if (inhabitant.Route.Destination is not null)
        {
            Assert.Contains(inhabitant.Route.Destination, knowledge.KnownTiles);
        }

        Assert.Equal(
            knowledge.KnownTiles
                .Distinct()
                .OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X),
            knowledge.KnownTiles);
        Assert.True(
            knowledge.KnownTiles.Count < snapshot.Tiles.Count,
            "The fixture must not project the complete server map as inhabitant knowledge.");
    }

    [Fact]
    public void PrivateWorldObservationProjectsTheActiveSettlementAndNeeds()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var store = new OwnerWorldObservationStore(runtime);

        var snapshot = store.GetSnapshot();

        Assert.Equal("playtest-alpha", snapshot.WorldId);
        Assert.Equal(4, snapshot.Inhabitants.Count);
        Assert.All(snapshot.Inhabitants, inhabitant =>
        {
            Assert.False(inhabitant.IsDraft);
            Assert.Equal("active", inhabitant.Lifecycle);
            Assert.Contains(inhabitant.DecisionFactors, factor => factor.Key == "personality");
            Assert.Contains(inhabitant.DecisionFactors, factor => factor.Key == "household");
            Assert.NotEmpty(inhabitant.SpatialKnowledge.PerceivedTiles);
            Assert.Contains(
                inhabitant.Relationships,
                relationship => relationship.Type == "household_membership" &&
                    relationship.State == "accepted" &&
                    relationship.OtherPartyId == "household:camp-alpha");
        });
        Assert.NotNull(snapshot.Cognition);
        Assert.NotNull(snapshot.WorldSystems);
        Assert.Equal("spring", snapshot.WorldSystems!.Season);
        Assert.Equal(3, snapshot.WorldSystems.EcologyResourceCount);
        Assert.Equal(1, snapshot.WorldSystems.FactionCount);
        Assert.Equal(4, snapshot.Inhabitants.Select(inhabitant => inhabitant.Id).Distinct().Count());
    }

    [Fact]
    public async Task PrivateWorldObservationExposesPublicIntentionsWithoutModelTrace()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        _ = await runtime.AdvanceOneTickAsync();

        var snapshot = new OwnerWorldObservationStore(runtime).GetSnapshot();

        Assert.Contains(snapshot.Inhabitants, inhabitant =>
            inhabitant.PublicIntention is { WorldTick: 1, Summary: not null });
        Assert.All(snapshot.Inhabitants.Where(inhabitant => inhabitant.PublicIntention is not null), inhabitant =>
        {
            Assert.DoesNotContain("chain", inhabitant.PublicIntention!.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("prompt", inhabitant.PublicIntention.Summary, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task PrivateWorldObservationReplaysItsOwnEventCursor()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        _ = await runtime.AdvanceOneTickAsync();
        var store = new OwnerWorldObservationStore(runtime);

        var snapshot = store.GetSnapshot();
        var suffix = store.GetEventsAfter(1);

        Assert.Equal(1, snapshot.WorldTick);
        Assert.Equal(snapshot.WorldTick, suffix.SnapshotTick);
        Assert.All(suffix.Events, worldEvent => Assert.True(worldEvent.EventId > 1));
        Assert.Contains(suffix.Events, worldEvent => worldEvent.Kind == "tick_advanced");
    }

    [Fact]
    public async Task PrivateWorldObservationProjectsContentLifecycleAndGovernanceHistory()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var package = new ContentPackageManifest(
            "camp-recipes",
            ContentVersion.Parse("1.0.0"),
            "sha256:" + new string('e', 64),
            [],
            [new ContentDefinition(
                "recipe",
                "berry-stew",
                ContentVersion.Parse("1.0.0"),
                "Berry stew",
                "sha256:" + new string('e', 64))],
            []);
        var resolution = PrivateWorldRuntime.PreviewContent([package], [package.PackageId]);
        runtime.ProposeContent(package);
        runtime.ValidateContent(package.PackageId, resolution);
        runtime.ApproveContent(package.PackageId);
        runtime.StageContent(package.PackageId);
        _ = await runtime.AdvanceOneTickAsync();

        var snapshot = new OwnerWorldObservationStore(runtime).GetSnapshot();

        var content = Assert.Single(snapshot.ContentPackages);
        Assert.Equal(package.PackageId, content.PackageId);
        Assert.Equal("active", content.Lifecycle);
        Assert.Contains(snapshot.ContentEvents, item => item.Kind == "package_activated");
    }
}
