using AgentWorld.GodotClient.Protocol;

namespace AgentWorld.Simulation.Tests;

public sealed class GodotWorldObservationProtocolTests
{
    [Fact]
    public void AcceptsCoherentReadOnlyReconnectBaselineAndAdvancesCursor()
    {
        var session = new WorldObservationSession();
        var observation = CreateValidObservation();

        var accepted = session.TryAccept(observation, out var failure);

        Assert.True(accepted, failure);
        Assert.Same(observation, session.Current);
        Assert.Equal(5, session.EventCursor);
    }

    [Fact]
    public void RejectsMalformedRefreshAndKeepsLastAcceptedObservation()
    {
        var session = new WorldObservationSession();
        var accepted = CreateValidObservation();
        Assert.True(session.TryAccept(accepted, out _));

        var malformed = accepted with
        {
            Baseline = accepted.Baseline with
            {
                Events = accepted.Baseline.Events with { SnapshotTick = 4 },
            },
        };

        var wasAccepted = session.TryAccept(malformed, out var failure);

        Assert.False(wasAccepted);
        Assert.Contains("coherent snapshot", failure, StringComparison.Ordinal);
        Assert.Same(accepted, session.Current);
        Assert.Equal(5, session.EventCursor);
    }

    [Fact]
    public void RejectsIncompatibleProtocolMajorWithoutDiscardingPriorWorld()
    {
        var session = new WorldObservationSession();
        var accepted = CreateValidObservation();
        Assert.True(session.TryAccept(accepted, out _));

        var incompatible = accepted with
        {
            Handshake = accepted.Handshake with { Protocol = new WorldProtocolVersion(2, 0) },
        };

        var wasAccepted = session.TryAccept(incompatible, out var failure);

        Assert.False(wasAccepted);
        Assert.Contains("not supported", failure, StringComparison.Ordinal);
        Assert.Same(accepted, session.Current);
    }

    private static WorldObservation CreateValidObservation()
    {
        var handshake = new WorldHandshake(
            Protocol: new WorldProtocolVersion(1, 0),
            ServerCapabilities:
            [
                "event-replay.read.v1",
                "reconnect-baseline.read.v1",
                "snapshot.read.v1",
            ],
            ClientCapabilities: []);
        var snapshot = new WorldSnapshot(
            WorldId: "fixture-world",
            WorldTick: 5,
            MapManifestDigest: "fixture-map",
            Tiles: [new WorldTile(0, 0, "meadow")],
            Objects: [],
            Resources: [],
            Actor: new WorldActor("camp-alpha", new WorldPosition(0, 0), 5000, 8000, 1, 0),
            LatestEventId: 5);
        var events = new WorldEventSlice(
            SnapshotTick: 5,
            AfterEventId: 3,
            Events:
            [
                new WorldEvent(4, 4, "move", "north"),
                new WorldEvent(5, 5, "sleep", "camp"),
            ]);

        return new WorldObservation(handshake, new WorldReconnectBaseline(snapshot, events));
    }
}
