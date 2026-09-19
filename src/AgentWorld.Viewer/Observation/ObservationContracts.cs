using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Persistence;

namespace AgentWorld.Viewer.Observation;

public sealed record ProtocolVersion(int Major, int Minor);

public sealed record ViewerHandshake(
    ProtocolVersion Protocol,
    IReadOnlyList<string> ServerCapabilities,
    IReadOnlyList<string> ClientCapabilities);

public sealed record ViewerPosition(int X, int Y);

public sealed record ViewerTile(int X, int Y, string Terrain);

public sealed record ViewerMapObject(string Id, string Kind, ViewerPosition Position);

public sealed record ViewerResource(string Id, string Kind, ViewerPosition Position, bool IsRenewable, string State);

public sealed record ViewerActor(
    string Id,
    ViewerPosition Position,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    int FoodItems,
    int WoodItems);

public sealed record ViewerEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record ViewerWorldSnapshot(
    string WorldId,
    long WorldTick,
    string MapManifestDigest,
    IReadOnlyList<ViewerTile> Tiles,
    IReadOnlyList<ViewerMapObject> Objects,
    IReadOnlyList<ViewerResource> Resources,
    ViewerActor Actor,
    long LatestEventId);

public sealed record ViewerEventSlice(long SnapshotTick, long AfterEventId, IReadOnlyList<ViewerEvent> Events);

/// <summary>
/// Owns the static deterministic sample exposed by the first browser slice.
/// It creates protocol DTOs from the core's immutable harness state rather than
/// exposing simulation records to clients.
/// </summary>
public sealed class SeededWorldObservationStore
{
    public const string SampleSeed = "camp-alpha";

    private static readonly string[] ServerCapabilities =
    [
        "snapshot.read.v1",
        "event-replay.read.v1",
        "seeded-map.read.v1",
    ];

    private static readonly string[] ClientCapabilities =
    [
        "snapshot.read.v1",
        "event-replay.read.v1",
    ];

    private readonly ViewerWorldSnapshot snapshot;
    private readonly ViewerEvent[] events;

    public SeededWorldObservationStore()
        : this(ScriptedHarness.RunEntireSequence(SampleSeed))
    {
    }

    public SeededWorldObservationStore(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        snapshot = ToSnapshot(world);
        events = world.Events
            .OrderBy(worldEvent => worldEvent.EventId)
            .Select(ToEvent)
            .ToArray();
    }

    public ViewerHandshake GetHandshake() => new(
        new ProtocolVersion(Major: 1, Minor: 0),
        ServerCapabilities.ToArray(),
        ClientCapabilities.ToArray());

    public ViewerWorldSnapshot GetSnapshot() => snapshot with
    {
        Tiles = snapshot.Tiles.ToArray(),
        Objects = snapshot.Objects.ToArray(),
        Resources = snapshot.Resources.ToArray(),
    };

    public ViewerEventSlice GetEventsAfter(long afterEventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterEventId);
        return new ViewerEventSlice(
            snapshot.WorldTick,
            afterEventId,
            events.Where(worldEvent => worldEvent.EventId > afterEventId).ToArray());
    }

    private static ViewerWorldSnapshot ToSnapshot(HarnessWorld world) => new(
        world.Identity.WorldId,
        world.Identity.WorldTick,
        world.Map.ManifestDigest,
        world.Map.Tiles
            .OrderBy(tile => tile.Position.Y)
            .ThenBy(tile => tile.Position.X)
            .Select(tile => new ViewerTile(tile.Position.X, tile.Position.Y, ToWireValue(tile.Terrain)))
            .ToArray(),
        world.Map.CampObjects
            .OrderBy(mapObject => mapObject.Id, StringComparer.Ordinal)
            .Select(mapObject => new ViewerMapObject(mapObject.Id, mapObject.Kind, ToPosition(mapObject.Position)))
            .ToArray(),
        world.Map.Resources
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .Select(resource => new ViewerResource(
                resource.Id,
                resource.Kind,
                ToPosition(resource.Position),
                resource.IsRenewable,
                ToWireValue(world.GetResource(resource.Id).State)))
            .ToArray(),
        new ViewerActor(
            world.Actor.Id,
            ToPosition(world.Actor.Position),
            world.Actor.HungerBasisPoints,
            world.Actor.EnergyBasisPoints,
            world.Actor.FoodItems,
            world.Actor.WoodItems),
        world.Events.Count == 0 ? 0 : world.Events.Max(worldEvent => worldEvent.EventId));

    private static ViewerEvent ToEvent(PersistenceEvent worldEvent) => new(
        worldEvent.EventId,
        worldEvent.WorldTick,
        ToWireValue(worldEvent.Kind),
        worldEvent.Detail ?? string.Empty);

    private static ViewerPosition ToPosition(GridPoint point) => new(point.X, point.Y);

    private static string ToWireValue(TerrainKind terrain) => terrain switch
    {
        TerrainKind.Meadow => "meadow",
        TerrainKind.Water => "water",
        TerrainKind.Mountain => "mountain",
        _ => throw new ArgumentOutOfRangeException(nameof(terrain)),
    };

    private static string ToWireValue(ResourceState state) => state switch
    {
        ResourceState.Available => "available",
        ResourceState.Depleted => "depleted",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string ToWireValue(PersistenceEventKind kind) => kind switch
    {
        PersistenceEventKind.CounterAdjusted => "counter_adjusted",
        PersistenceEventKind.MigrationApplied => "migration_applied",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
