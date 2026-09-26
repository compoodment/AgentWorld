using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Persistence;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Harness;

/// <summary>
/// A coordinate in the first world's bounded, logical ground grid.
/// </summary>
public readonly record struct GridPoint(int X, int Y);

public enum TerrainKind
{
    Meadow,
    Water,
    Mountain,
    River,
    Lake,
    Ocean,
    Peak,
}

public enum ResourceState
{
    Available,
    Depleted,
}

public sealed record TerrainTile(GridPoint Position, TerrainKind Terrain);

public sealed record CampObject(string Id, string Kind, GridPoint Position);

public sealed record MapResource(
    string Id,
    string Kind,
    GridPoint Position,
    bool IsRenewable);

/// <summary>
/// The selected deterministic map attempt and its canonical manifest lock.
/// </summary>
public sealed record SeededMap(
    int Width,
    int Height,
    int GenerationAttempt,
    IReadOnlyList<TerrainTile> Tiles,
    IReadOnlyList<CampObject> CampObjects,
    IReadOnlyList<MapResource> Resources,
    string ManifestDigest)
{
    // Keep the index outside the record: a cache field would silently change
    // record equality and could be copied into a `with` map with new tiles.
    private static readonly ConditionalWeakTable<SeededMap, byte[]> TerrainIndexes = new();

    public bool Contains(GridPoint point) =>
        point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

    public bool IsPassable(GridPoint point) =>
        Contains(point) && TerrainAt(point) == (byte)TerrainKind.Meadow;

    // Construction eligibility is separate from travel: future mountain
    // paths must not silently become build sites when traversal is expanded.
    public bool IsBuildable(GridPoint point) =>
        Contains(point) && TerrainAt(point) == (byte)TerrainKind.Meadow;

    private byte TerrainAt(GridPoint point) =>
        TerrainIndexes.GetValue(this, static map =>
        {
            var indexed = new byte[checked(map.Width * map.Height)];
            Array.Fill(indexed, byte.MaxValue);
            foreach (var tile in map.Tiles)
                if (map.Contains(tile.Position))
                    indexed[tile.Position.Y * map.Width + tile.Position.X] = checked((byte)tile.Terrain);
            return indexed;
        })[point.Y * Width + point.X];

    public CampObject GetObject(string id) =>
        CampObjects.Single(mapObject => string.Equals(mapObject.Id, id, StringComparison.Ordinal));

    public MapResource GetResource(string id) =>
        Resources.Single(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));
}

/// <summary>
/// Generates the tiny fixed-corpus temperate camp fixture. It intentionally has
/// only enough terrain variation to exercise the validation rules.
/// </summary>
public static class SeededMapGenerator
{
    public const int MaximumAttempts = 32;
    public const string GeneratorId = "temperate-fixture";
    public const string GeneratorVersion = "v1";
    public const string GeneratorConfigDigest = "sha256:temperate-fixture-config-v2";
    public const string FertileLandResourceId = "fertile-land";

    public static SeededMap Generate(string worldSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);

        var diagnostics = new List<string>();
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var candidate = CreateCandidate(worldSeed, attempt);
            var validation = MapAcceptance.Validate(candidate);
            if (validation.IsValid)
            {
                return candidate;
            }

            diagnostics.Add($"attempt {attempt}: {validation.Failure}");
        }

        throw new InvalidOperationException(
            $"No valid temperate fixture map for seed '{worldSeed}' after {MaximumAttempts} attempts: " +
            string.Join("; ", diagnostics));
    }

    private static SeededMap CreateCandidate(string worldSeed, int attempt)
    {
        const int width = 6;
        const int height = 5;
        var terrain = new TerrainKind[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                terrain[y, x] = TerrainKind.Meadow;
            }
        }

        var safeObstacles = new[]
        {
            new GridPoint(5, 0),
            new GridPoint(5, 4),
            new GridPoint(0, 4),
            new GridPoint(4, 4),
        };
        var random = Pcg32XshRrV1.Create(worldSeed, $"worldgen/attempt:{attempt}");
        var waterIndex = (int)(random.NextUInt() % (uint)safeObstacles.Length);
        var mountainIndex = (waterIndex + 1 + (int)(random.NextUInt() % (uint)(safeObstacles.Length - 1))) %
            safeObstacles.Length;
        terrain[safeObstacles[waterIndex].Y, safeObstacles[waterIndex].X] = TerrainKind.Water;
        terrain[safeObstacles[mountainIndex].Y, safeObstacles[mountainIndex].X] = TerrainKind.Mountain;

        var tiles = new List<TerrainTile>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                tiles.Add(new TerrainTile(new GridPoint(x, y), terrain[y, x]));
            }
        }

        var campObjects = new[]
        {
            new CampObject("bedroll", "bedroll", new GridPoint(1, 0)),
            new CampObject("campfire", "cooking", new GridPoint(2, 0)),
            new CampObject("founder-scout", "founder", new GridPoint(0, 0)),
            new CampObject("shelter", "shelter", new GridPoint(0, 1)),
            new CampObject("storage", "storage", new GridPoint(1, 1)),
        };
        var resources = new[]
        {
            new MapResource("berry-patch", "food", new GridPoint(4, 1), true),
            new MapResource("timber-tree", "construction", new GridPoint(4, 3), false),
            new MapResource(FertileLandResourceId, "fertile_land", new GridPoint(2, 3), false),
        };
        var withoutDigest = new SeededMap(width, height, attempt, tiles, campObjects, resources, string.Empty);
        return withoutDigest with { ManifestDigest = MapManifestCodec.Digest(withoutDigest) };
    }
}

/// <summary>A starter camp with facilities but no pre-created person.</summary>
public static class BaseCampMapGenerator
{
    public static SeededMap Generate(string worldSeed)
    {
        var fixture = SeededMapGenerator.Generate(worldSeed);
        var objects = fixture.CampObjects
            .Where(item => item.Kind != "founder")
            .Concat([
                new CampObject("second-shelter", "shelter", new GridPoint(3, 0)),
                new CampObject("workshop", "workshop", new GridPoint(0, 2)),
                new CampObject("camp-path", "path", new GridPoint(2, 1)),
            ]).ToArray();
        var candidate = fixture with { CampObjects = objects, ManifestDigest = string.Empty };
        var map = candidate with { ManifestDigest = MapManifestCodec.Digest(candidate) };
        var validation = MapAcceptance.Validate(map, allowEmptyCamp: true);
        if (!validation.IsValid)
            throw new InvalidOperationException($"The generated base camp is invalid: {validation.Failure}");
        return map;
    }
}

/// <summary>
/// Projects generated 2D geography into the current physical-map contract and
/// places the ordinary empty starter camp on a connected clear patch. Climate,
/// vegetation and surface layers remain in the geography source and are not
/// yet projected by the playable map contract.
/// </summary>
public static class GeneratedCampMapGenerator
{
    private const int CampWidth = 6;
    private const int CampHeight = 5;

    public static SeededMap Generate(GeographyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        // This bridge still allocates one object per tile for the old
        // physical-map contract. The compact geography source supports all
        // presets; larger playable maps need a compact save/projection first.
        if (options.Size > WorldSizePreset.Medium)
            throw new NotSupportedException("Large generated worlds require the compact playable-map contract.");
        var geography = GeographyGenerator.Generate(options);
        var width = geography.Width;
        var height = geography.Height;
        var tiles = new TerrainTile[checked(width * height)];
        var kinds = new TerrainKind[tiles.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var tile = geography.At(x, y);
                var kind = tile.Water switch
                {
                    WaterKind.Ocean => TerrainKind.Ocean,
                    WaterKind.Lake => TerrainKind.Lake,
                    WaterKind.River => TerrainKind.River,
                    _ when tile.Elevation >= 245 => TerrainKind.Peak,
                    _ when tile.Elevation >= 215 => TerrainKind.Mountain,
                    _ => TerrainKind.Meadow,
                };
                var index = y * width + x;
                kinds[index] = kind;
                tiles[index] = new TerrainTile(new GridPoint(x, y), kind);
            }

        var origin = FindCampOrigin(kinds, width, height);
        var template = BaseCampMapGenerator.Generate(options.Seed);
        var objects = template.CampObjects.Select(item => item with
        {
            Position = new GridPoint(item.Position.X + origin.X, item.Position.Y + origin.Y),
        }).ToArray();
        var resources = template.Resources.Select(item => item with
        {
            Position = new GridPoint(item.Position.X + origin.X, item.Position.Y + origin.Y),
        }).ToArray();
        var withoutDigest = new SeededMap(width, height, 0, tiles, objects, resources, string.Empty);
        var map = withoutDigest with { ManifestDigest = MapManifestCodec.Digest(withoutDigest) };
        var validation = MapAcceptance.Validate(map, allowEmptyCamp: true);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Generated base camp is invalid: {validation.Failure}");
        return map;
    }

    private static GridPoint FindCampOrigin(TerrainKind[] kinds, int width, int height)
    {
        var centerX = (width - CampWidth) / 2;
        var centerY = (height - CampHeight) / 2;
        var maxRadius = width + height;
        for (var radius = 0; radius <= maxRadius; radius++)
            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                var offsetY = radius - Math.Abs(offsetX);
                if (TrySite(centerX + offsetX, centerY + offsetY, kinds, width, height))
                    return new GridPoint(centerX + offsetX, centerY + offsetY);
                if (offsetY > 0 && TrySite(centerX + offsetX, centerY - offsetY, kinds, width, height))
                    return new GridPoint(centerX + offsetX, centerY - offsetY);
            }
        throw new InvalidOperationException("The generated geography has no suitable base-camp clearing.");
    }

    private static bool TrySite(int left, int top, TerrainKind[] kinds, int width, int height)
    {
        if (left < 0 || top < 0 || left + CampWidth > width || top + CampHeight > height)
            return false;
        if (left / GeographyGenerator.ChunkSize != (left + CampWidth - 1) / GeographyGenerator.ChunkSize ||
            top / GeographyGenerator.ChunkSize != (top + CampHeight - 1) / GeographyGenerator.ChunkSize)
            return false;
        for (var y = top; y < top + CampHeight; y++)
            for (var x = left; x < left + CampWidth; x++)
                if (kinds[y * width + x] != TerrainKind.Meadow) return false;
        return true;
    }
}

/// <summary>
/// Explicit canonical bytes for the genesis map manifest. The digest is not
/// included in its own input, avoiding self-referential serialization.
/// </summary>
public static class MapManifestCodec
{
    private const string Header = "clankerworld.seeded-map/v1";

    public static byte[] Encode(SeededMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("dimensions=").Append(map.Width).Append('x').Append(map.Height).Append('\n');
        builder.Append("generation_attempt=").Append(map.GenerationAttempt).Append('\n');
        foreach (var tile in map.Tiles.OrderBy(tile => tile.Position.Y).ThenBy(tile => tile.Position.X))
        {
            builder.Append("tile=")
                .Append(tile.Position.X).Append(',').Append(tile.Position.Y).Append('|')
                .Append(ToWireValue(tile.Terrain)).Append('\n');
        }

        foreach (var mapObject in map.CampObjects.OrderBy(mapObject => mapObject.Id, StringComparer.Ordinal))
        {
            builder.Append("object=")
                .Append(mapObject.Id).Append('|').Append(mapObject.Kind).Append('|')
                .Append(mapObject.Position.X).Append(',').Append(mapObject.Position.Y).Append('\n');
        }

        foreach (var resource in map.Resources.OrderBy(resource => resource.Id, StringComparer.Ordinal))
        {
            builder.Append("resource=")
                .Append(resource.Id).Append('|').Append(resource.Kind).Append('|')
                .Append(resource.Position.X).Append(',').Append(resource.Position.Y).Append('|')
                .Append(resource.IsRenewable ? "renewable" : "finite").Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static string Digest(SeededMap map) =>
        Convert.ToHexStringLower(SHA256.HashData(Encode(map)));

    private static string ToWireValue(TerrainKind terrain) => terrain switch
    {
        TerrainKind.Meadow => "meadow",
        TerrainKind.Water => "water",
        TerrainKind.Mountain => "mountain",
        TerrainKind.River => "river",
        TerrainKind.Lake => "lake",
        TerrainKind.Ocean => "ocean",
        TerrainKind.Peak => "peak",
        _ => throw new ArgumentOutOfRangeException(nameof(terrain)),
    };
}

public sealed record MapValidationResult(bool IsValid, string? Failure)
{
    public static MapValidationResult Valid { get; } = new(true, null);

    public static MapValidationResult Invalid(string failure) => new(false, failure);
}

/// <summary>
/// The first-world generated-map acceptance checks used by the seed corpus.
/// </summary>
public static class MapAcceptance
{
    public static MapValidationResult Validate(SeededMap map, bool allowEmptyCamp = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Width <= 0 || map.Height <= 0 || map.GenerationAttempt < 0 ||
            map.GenerationAttempt >= SeededMapGenerator.MaximumAttempts)
        {
            return MapValidationResult.Invalid("The map dimensions or generation attempt are invalid.");
        }

        if (map.Tiles.Count != map.Width * map.Height ||
            map.Tiles.Select(tile => tile.Position).Distinct().Count() != map.Tiles.Count ||
            map.Tiles.Any(tile => !map.Contains(tile.Position)))
        {
            return MapValidationResult.Invalid("The logical grid is not a complete bounded rectangle.");
        }

        var founder = map.CampObjects.SingleOrDefault(mapObject =>
            string.Equals(mapObject.Kind, "founder", StringComparison.Ordinal));
        if (!allowEmptyCamp && (founder is null || !map.IsPassable(founder.Position)))
        {
            return MapValidationResult.Invalid("The founder must occupy passable ground.");
        }
        if (allowEmptyCamp && founder is not null)
            return MapValidationResult.Invalid("An empty base camp cannot contain a founder marker.");

        var requiredKinds = new[] { "shelter", "bedroll", "storage", "cooking" };
        if (requiredKinds.Any(kind => !map.CampObjects.Any(mapObject =>
                string.Equals(mapObject.Kind, kind, StringComparison.Ordinal))))
        {
            return MapValidationResult.Invalid("A required camp-start placement is missing.");
        }

        if (map.CampObjects.Select(mapObject => mapObject.Id).Distinct(StringComparer.Ordinal).Count() != map.CampObjects.Count ||
            map.CampObjects.Select(mapObject => mapObject.Position).Distinct().Count() != map.CampObjects.Count ||
            map.CampObjects.Any(mapObject => !map.IsPassable(mapObject.Position)))
        {
            return MapValidationResult.Invalid("Camp-start objects are duplicated or overlap impassable terrain.");
        }

        if (map.Resources.Select(resource => resource.Id).Distinct(StringComparer.Ordinal).Count() != map.Resources.Count ||
            map.Resources.Any(resource => !map.IsPassable(resource.Position)))
        {
            return MapValidationResult.Invalid("Resource placements are invalid.");
        }

        if (!map.Resources.Any(resource => resource.IsRenewable &&
                string.Equals(resource.Kind, "food", StringComparison.Ordinal)) ||
            !map.Resources.Any(resource => string.Equals(resource.Kind, "construction", StringComparison.Ordinal)) ||
            !map.Resources.Any(resource => string.Equals(resource.Id, SeededMapGenerator.FertileLandResourceId, StringComparison.Ordinal) &&
                string.Equals(resource.Kind, "fertile_land", StringComparison.Ordinal)))
        {
            return MapValidationResult.Invalid("Reachable food, construction, or fertile-land resources are missing.");
        }

        var startingPoint = founder?.Position ?? map.GetObject("bedroll").Position;
        var reachable = ReachableFrom(map, startingPoint);
        if (map.CampObjects.Any(mapObject => !reachable.Contains(mapObject.Position)) ||
            map.Resources.Any(resource => !reachable.Contains(resource.Position)))
        {
            return MapValidationResult.Invalid("A required camp-start route crosses an impassable boundary.");
        }

        if (!string.Equals(MapManifestCodec.Digest(map), map.ManifestDigest, StringComparison.Ordinal))
        {
            return MapValidationResult.Invalid("The manifest digest does not match the selected map.");
        }

        return MapValidationResult.Valid;
    }

    private static HashSet<GridPoint> ReachableFrom(SeededMap map, GridPoint origin)
    {
        var visited = new HashSet<GridPoint> { origin };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(origin);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in CardinalNeighbors(current))
            {
                if (map.IsPassable(next) && visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return visited;
    }

    internal static IReadOnlyList<GridPoint> CardinalNeighbors(GridPoint point) =>
    [
        new GridPoint(point.X, point.Y - 1),
        new GridPoint(point.X + 1, point.Y),
        new GridPoint(point.X, point.Y + 1),
        new GridPoint(point.X - 1, point.Y),
    ];
}

/// <summary>
/// Four-direction A* with the contract's deterministic queue-key ordering.
/// </summary>
public static class DeterministicRouteFinder
{
    public static IReadOnlyList<GridPoint> Find(SeededMap map, GridPoint origin, GridPoint destination)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsPassable(origin) || !map.IsPassable(destination))
        {
            throw new InvalidOperationException("Routing endpoints must be passable.");
        }

        var open = new PriorityQueue<RouteNode, RoutePriority>();
        var predecessor = new Dictionary<GridPoint, GridPoint>();
        var best = new Dictionary<GridPoint, RouteRecord>();
        var nodeId = 0;
        var start = new RouteNode(origin, origin, 0, nodeId++);
        open.Enqueue(start, ToPriority(start, destination));
        best.Add(origin, new RouteRecord(0, origin));

        while (open.TryDequeue(out var node, out _))
        {
            var record = best[node.Position];
            if (node.G != record.Cost || node.Predecessor != record.Predecessor)
            {
                continue;
            }

            if (node.Position == destination)
            {
                return Reconstruct(predecessor, origin, destination);
            }

            foreach (var next in MapAcceptance.CardinalNeighbors(node.Position))
            {
                if (!map.IsPassable(next))
                {
                    continue;
                }

                var candidate = new RouteRecord(checked(node.G + 100), node.Position);
                if (best.TryGetValue(next, out var old) && Compare(candidate, old) >= 0)
                {
                    continue;
                }

                best[next] = candidate;
                predecessor[next] = node.Position;
                var nextNode = new RouteNode(next, node.Position, candidate.Cost, nodeId++);
                open.Enqueue(nextNode, ToPriority(nextNode, destination));
            }
        }

        throw new InvalidOperationException("No passable route exists between the requested points.");
    }

    private static int Compare(RouteRecord left, RouteRecord right)
    {
        var result = left.Cost.CompareTo(right.Cost);
        if (result != 0)
        {
            return result;
        }

        result = left.Predecessor.Y.CompareTo(right.Predecessor.Y);
        return result != 0 ? result : left.Predecessor.X.CompareTo(right.Predecessor.X);
    }

    private static List<GridPoint> Reconstruct(
        Dictionary<GridPoint, GridPoint> predecessor,
        GridPoint origin,
        GridPoint destination)
    {
        var route = new List<GridPoint> { destination };
        var current = destination;
        while (current != origin)
        {
            current = predecessor[current];
            route.Add(current);
        }

        route.Reverse();
        return route;
    }

    private static RoutePriority ToPriority(RouteNode node, GridPoint destination)
    {
        var heuristic = checked((Math.Abs(node.Position.X - destination.X) +
            Math.Abs(node.Position.Y - destination.Y)) * 100);
        return new RoutePriority(
            checked(node.G + heuristic),
            heuristic,
            node.G,
            node.Position.Y,
            node.Position.X,
            node.Predecessor.Y,
            node.Predecessor.X,
            node.NodeId);
    }

    private sealed record RouteNode(GridPoint Position, GridPoint Predecessor, int G, int NodeId);

    private readonly record struct RouteRecord(int Cost, GridPoint Predecessor);

    private readonly record struct RoutePriority(
        int F,
        int H,
        int G,
        int Y,
        int X,
        int PredecessorY,
        int PredecessorX,
        int NodeId) : IComparable<RoutePriority>
    {
        public int CompareTo(RoutePriority other)
        {
            var result = F.CompareTo(other.F);
            result = result != 0 ? result : H.CompareTo(other.H);
            result = result != 0 ? result : G.CompareTo(other.G);
            result = result != 0 ? result : Y.CompareTo(other.Y);
            result = result != 0 ? result : X.CompareTo(other.X);
            result = result != 0 ? result : PredecessorY.CompareTo(other.PredecessorY);
            result = result != 0 ? result : PredecessorX.CompareTo(other.PredecessorX);
            return result != 0 ? result : NodeId.CompareTo(other.NodeId);
        }
    }
}

public sealed record HarnessActor(
    string Id,
    GridPoint Position,
    int HungerBasisPoints,
    int EnergyBasisPoints,
    int FoodItems,
    int WoodItems);

public sealed record RuntimeResource(string Id, ResourceState State);

/// <summary>
/// The complete small state for the #88 fixture. The map manifest stays
/// immutable; resource availability and the actor live in the tick state.
/// </summary>
public sealed record HarnessWorld(
    WorldIdentity Identity,
    SeededMap Map,
    HarnessActor Actor,
    IReadOnlyList<RuntimeResource> Resources,
    IReadOnlyList<PersistenceEvent> Events)
{
    public RuntimeResource GetResource(string id) =>
        Resources.Single(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));
}

/// <summary>
/// One deliberately small ordered kernel path: needs first, then exactly one
/// movement/work action, then a durable event for the completed tick.
/// </summary>
public static class ScriptedHarness
{
    private const int NeedDrainPerTick = 100;
    private const int FoodRecovery = 2_000;
    private const int SleepRecovery = 2_500;
    private const string ActorId = "actor-scout";

    public static HarnessWorld CreateGenesis(string worldSeed)
    {
        var map = SeededMapGenerator.Generate(worldSeed);
        var identity = CreateIdentity(worldSeed, map);
        return CreateGenesis(identity);
    }

    public static HarnessWorld CreateGenesis(WorldIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var map = SeededMapGenerator.Generate(identity.WorldSeed);
        if (map.GenerationAttempt != identity.GenerationAttempt ||
            !string.Equals(map.ManifestDigest, identity.InitialMapManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The saved map identity does not reproduce the selected manifest.");
        }

        var founder = map.GetObject("founder-scout");
        var actor = new HarnessActor(ActorId, founder.Position, 5_000, 4_000, 0, 0);
        var resources = map.Resources
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .Select(resource => new RuntimeResource(resource.Id, ResourceState.Available))
            .ToArray();
        return new HarnessWorld(identity with { WorldTick = 0 }, map, actor, resources, []);
    }

    public static HarnessWorld RunToFoodConsumed(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var berry = world.Map.GetResource("berry-patch");
        var current = MoveUntilAt(world, berry.Position);
        current = Harvest(current, berry.Id);
        return Consume(current);
    }

    public static HarnessWorld FinishAfterFood(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var bedroll = world.Map.GetObject("bedroll");
        return Sleep(MoveUntilAt(world, bedroll.Position));
    }

    public static HarnessWorld RunEntireSequence(string worldSeed) =>
        FinishAfterFood(RunToFoodConsumed(CreateGenesis(worldSeed)));

    /// <summary>
    /// Advances exactly one committed action from the small scripted fixture.
    /// This lets a host own the world clock without teaching a client how to
    /// mutate or replay simulation state.
    /// </summary>
    public static bool TryAdvanceOneAction(HarnessWorld world, out HarnessWorld advanced)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.Actor.FoodItems > 0)
        {
            advanced = Consume(world);
            return true;
        }

        var berry = world.Map.GetResource("berry-patch");
        if (world.GetResource(berry.Id).State == ResourceState.Available)
        {
            advanced = world.Actor.Position == berry.Position
                ? Harvest(world, berry.Id)
                : MoveOneStep(world, berry.Position);
            return true;
        }

        var bedroll = world.Map.GetObject("bedroll");
        if (world.Actor.Position != bedroll.Position)
        {
            advanced = MoveOneStep(world, bedroll.Position);
            return true;
        }

        if (!world.Events.Any(worldEvent => string.Equals(worldEvent.Detail, $"sleep:{ActorId}", StringComparison.Ordinal)))
        {
            advanced = Sleep(world);
            return true;
        }

        advanced = world;
        return false;
    }

    /// <summary>
    /// Phase 3 executor entry points. They reuse the fixture's needs/resource
    /// commit path while allowing cognition to select the action instead of a
    /// hard-coded script selecting it.
    /// </summary>
    public static HarnessWorld ApplyMovement(HarnessWorld world, GridPoint destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Actor.Position == destination)
        {
            throw new InvalidOperationException("A Phase 3 movement action must advance to a different tile.");
        }

        return Commit(
            world,
            actor => actor with { Position = destination },
            resources => resources,
            1,
            $"move:{ActorId}:{destination.X},{destination.Y}");
    }

    public static HarnessWorld ApplyHarvest(HarnessWorld world, string resourceId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Harvest(world, resourceId);
    }

    public static HarnessWorld ApplyConsume(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Consume(world);
    }

    public static HarnessWorld ApplySleep(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Sleep(world);
    }

    public static HarnessWorld ApplyIdle(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Commit(
            world,
            actor => actor,
            resources => resources,
            0,
            $"idle:{ActorId}");
    }

    public static HarnessWorld ReplayFromGenesis(WorldIdentity identity, IReadOnlyList<PersistenceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        var replay = CreateGenesis(identity with { WorldTick = 0 });
        foreach (var expected in events.OrderBy(worldEvent => worldEvent.EventId))
        {
            replay = ReplayOne(replay, expected);
        }

        return replay;
    }

    private static HarnessWorld ReplayOne(HarnessWorld world, PersistenceEvent expected)
    {
        if (expected.Detail is null)
        {
            throw new InvalidDataException("Harness events must include an action detail.");
        }

        HarnessWorld replayed;
        if (expected.Detail.StartsWith("move:", StringComparison.Ordinal))
        {
            var components = expected.Detail.Split(':', StringSplitOptions.None);
            if (components.Length != 3 || !string.Equals(components[1], ActorId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The move event detail is invalid.");
            }

            replayed = MoveOneStep(world, ParsePoint(components[2]));
        }
        else if (expected.Detail.StartsWith("harvest:", StringComparison.Ordinal))
        {
            replayed = Harvest(world, expected.Detail["harvest:".Length..]);
        }
        else if (string.Equals(expected.Detail, $"consume:{ActorId}", StringComparison.Ordinal))
        {
            replayed = Consume(world);
        }
        else if (string.Equals(expected.Detail, $"sleep:{ActorId}", StringComparison.Ordinal))
        {
            replayed = Sleep(world);
        }
        else
        {
            throw new InvalidDataException("The harness event action is not recognized.");
        }

        var actual = replayed.Events[^1];
        if (actual != expected)
        {
            throw new InvalidDataException("Replaying the action log produced a different committed event.");
        }

        return replayed;
    }

    private static HarnessWorld MoveUntilAt(HarnessWorld world, GridPoint destination)
    {
        var current = world;
        while (current.Actor.Position != destination)
        {
            current = MoveOneStep(current, destination);
        }

        return current;
    }

    private static HarnessWorld MoveOneStep(HarnessWorld world, GridPoint destination)
    {
        var route = DeterministicRouteFinder.Find(world.Map, world.Actor.Position, destination);
        if (route.Count < 2)
        {
            throw new InvalidOperationException("A movement event must advance to a different tile.");
        }

        var next = route[1];
        return Commit(
            world,
            actor => actor with { Position = next },
            resources => resources,
            1,
            $"move:{ActorId}:{next.X},{next.Y}");
    }

    private static HarnessWorld Harvest(HarnessWorld world, string resourceId)
    {
        var source = world.Map.GetResource(resourceId);
        if (world.Actor.Position != source.Position || world.GetResource(resourceId).State != ResourceState.Available)
        {
            throw new InvalidOperationException("Harvesting requires an available source at the actor's position.");
        }

        return Commit(
            world,
            actor => source.Kind switch
            {
                "food" => actor with { FoodItems = checked(actor.FoodItems + 1) },
                "construction" => actor with { WoodItems = checked(actor.WoodItems + 1) },
                _ => throw new InvalidOperationException("The resource kind is not harvestable by this fixture."),
            },
            resources => resources
                .Select(resource => string.Equals(resource.Id, resourceId, StringComparison.Ordinal)
                    ? resource with { State = ResourceState.Depleted }
                    : resource)
                .ToArray(),
            2,
            $"harvest:{resourceId}");
    }

    private static HarnessWorld Consume(HarnessWorld world)
    {
        if (world.Actor.FoodItems <= 0)
        {
            throw new InvalidOperationException("Consuming requires an available food item.");
        }

        return Commit(
            world,
            actor => actor with
            {
                FoodItems = actor.FoodItems - 1,
                HungerBasisPoints = ClampBasisPoints(actor.HungerBasisPoints + FoodRecovery),
            },
            resources => resources,
            3,
            $"consume:{ActorId}");
    }

    private static HarnessWorld Sleep(HarnessWorld world)
    {
        if (world.Actor.Position != world.Map.GetObject("bedroll").Position)
        {
            throw new InvalidOperationException("Sleeping requires the actor to be at the bedroll.");
        }

        return Commit(
            world,
            actor => actor with { EnergyBasisPoints = ClampBasisPoints(actor.EnergyBasisPoints + SleepRecovery) },
            resources => resources,
            4,
            $"sleep:{ActorId}");
    }

    private static HarnessWorld Commit(
        HarnessWorld world,
        Func<HarnessActor, HarnessActor> action,
        Func<IReadOnlyList<RuntimeResource>, IReadOnlyList<RuntimeResource>> resourceAction,
        int counterDelta,
        string detail)
    {
        var nextTick = checked(world.Identity.WorldTick + 1);
        var needsApplied = world.Actor with
        {
            HungerBasisPoints = ClampBasisPoints(world.Actor.HungerBasisPoints - NeedDrainPerTick),
            EnergyBasisPoints = ClampBasisPoints(world.Actor.EnergyBasisPoints - NeedDrainPerTick),
        };
        var actor = action(needsApplied);
        var worldEvent = new PersistenceEvent(
            checked(world.Events.Count + 1L),
            nextTick,
            PersistenceEventKind.CounterAdjusted,
            counterDelta,
            null,
            detail);
        return world with
        {
            Identity = world.Identity with { WorldTick = nextTick },
            Actor = actor,
            Resources = resourceAction(world.Resources),
            Events = world.Events.Append(worldEvent).ToArray(),
        };
    }

    private static WorldIdentity CreateIdentity(string worldSeed, SeededMap map) => new(
        $"harness-{worldSeed}",
        "deterministic-kernel-contract/phase-1",
        "phase-1-harness/v1",
        "1",
        "one-tick-per-minute/v1",
        0,
        worldSeed,
        SeededMapGenerator.GeneratorId,
        SeededMapGenerator.GeneratorVersion,
        SeededMapGenerator.GeneratorConfigDigest,
        map.GenerationAttempt,
        map.ManifestDigest,
        "content-lock/empty-v1",
        "asset-lock/empty-v1");

    private static int ClampBasisPoints(int value) => Math.Clamp(value, 0, 10_000);

    private static GridPoint ParsePoint(string value)
    {
        var parts = value.Split(',', StringSplitOptions.None);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y))
        {
            throw new InvalidDataException("A grid coordinate must use invariant x,y integers.");
        }

        return new GridPoint(x, y);
    }
}

/// <summary>
/// Persists #88 through the #87 envelope. The payload is canonical state inside
/// the existing snapshot; the outer event log remains the append-only authority.
/// </summary>
public static class HarnessPersistence
{
    public static PersistedWorld Save(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new PersistedWorld(
            CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(ToMiniatureState(world))),
            CanonicalPersistenceCodec.EncodeEventLog(world.Events));
    }

    public static HarnessWorld Load(PersistedWorld save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var snapshot = CanonicalPersistenceCodec.DecodeSnapshot(save.SnapshotBytes);
        var events = CanonicalPersistenceCodec.DecodeEventLog(save.EventLogBytes);
        var genericReplay = WorldReplay.ReplayGenesis(
            snapshot.State.Identity with { WorldTick = 0 },
            events);
        if (genericReplay.Counter != snapshot.State.Counter ||
            genericReplay.LastEventId != snapshot.State.LastEventId ||
            genericReplay.Identity.WorldTick != snapshot.State.Identity.WorldTick)
        {
            throw new InvalidDataException("The snapshot does not agree with its ordered event suffix.");
        }

        var decoded = HarnessStateCodec.Decode(snapshot.State.Identity, snapshot.State.CanonicalStatePayload, events);
        var physicalReplay = ScriptedHarness.ReplayFromGenesis(snapshot.State.Identity, events);
        if (!string.Equals(StateDigest(decoded), StateDigest(physicalReplay), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The canonical state payload does not match replayed actions.");
        }

        return decoded;
    }

    public static string StateDigest(HarnessWorld world) =>
        CanonicalPersistenceCodec.StateDigest(ToMiniatureState(world));

    public static string EventDigest(HarnessWorld world) =>
        CanonicalPersistenceCodec.EventDigest(world.Events);

    private static MiniatureWorldState ToMiniatureState(HarnessWorld world) => new(
        world.Identity,
        checked(world.Events.Sum(worldEvent => worldEvent.CounterDelta)),
        world.Events.Count == 0 ? 0 : world.Events[^1].EventId,
        HarnessStateCodec.Encode(world));
}

public static class HarnessStateCodec
{
    private const string Header = "clankerworld.seeded-harness-state/v1";

    public static string Encode(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("map_manifest_base64=")
            .Append(Convert.ToBase64String(MapManifestCodec.Encode(world.Map))).Append('\n');
        builder.Append("actor=")
            .Append(world.Actor.Id).Append('|')
            .Append(world.Actor.Position.X).Append('|').Append(world.Actor.Position.Y).Append('|')
            .Append(world.Actor.HungerBasisPoints).Append('|').Append(world.Actor.EnergyBasisPoints).Append('|')
            .Append(world.Actor.FoodItems).Append('|').Append(world.Actor.WoodItems).Append('\n');
        foreach (var resource in world.Resources.OrderBy(resource => resource.Id, StringComparer.Ordinal))
        {
            builder.Append("resource=").Append(resource.Id).Append('|')
                .Append(resource.State == ResourceState.Available ? "available" : "depleted").Append('\n');
        }

        return builder.ToString();
    }

    public static HarnessWorld Decode(
        WorldIdentity identity,
        string? payload,
        IReadOnlyList<PersistenceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        if (string.IsNullOrEmpty(payload))
        {
            throw new InvalidDataException("The harness snapshot does not contain canonical state payload.");
        }

        var lines = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The harness state format is not supported.");
        }

        var encodedManifest = ValueAfterPrefix(lines[1], "map_manifest_base64=");
        byte[] manifest;
        try
        {
            manifest = Convert.FromBase64String(encodedManifest);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The harness map manifest is not base64.", exception);
        }

        var genesis = ScriptedHarness.CreateGenesis(identity);
        var expectedManifest = MapManifestCodec.Encode(genesis.Map);
        if (!manifest.SequenceEqual(expectedManifest) ||
            !string.Equals(MapManifestCodec.Digest(genesis.Map), identity.InitialMapManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The saved map manifest does not match the world identity.");
        }

        var actorParts = ValueAfterPrefix(lines[2], "actor=").Split('|', StringSplitOptions.None);
        if (actorParts.Length != 7 || !string.Equals(actorParts[0], genesis.Actor.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The saved actor state is invalid.");
        }

        var actor = new HarnessActor(
            actorParts[0],
            new GridPoint(ParseInteger(actorParts[1]), ParseInteger(actorParts[2])),
            ParseBasisPoints(actorParts[3]),
            ParseBasisPoints(actorParts[4]),
            ParseNonNegativeInteger(actorParts[5]),
            ParseNonNegativeInteger(actorParts[6]));
        if (!genesis.Map.IsPassable(actor.Position))
        {
            throw new InvalidDataException("The saved actor is not on passable ground.");
        }

        var savedResources = lines.Skip(3)
            .Select(ParseResource)
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .ToArray();
        if (savedResources.Length != genesis.Resources.Count ||
            !savedResources.Select(resource => resource.Id).SequenceEqual(
                genesis.Resources.Select(resource => resource.Id),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException("The saved resource state does not match the manifest.");
        }

        return new HarnessWorld(identity, genesis.Map, actor, savedResources, events.ToArray());
    }

    private static RuntimeResource ParseResource(string line)
    {
        var parts = ValueAfterPrefix(line, "resource=").Split('|', StringSplitOptions.None);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidDataException("The saved resource line is invalid.");
        }

        return parts[1] switch
        {
            "available" => new RuntimeResource(parts[0], ResourceState.Available),
            "depleted" => new RuntimeResource(parts[0], ResourceState.Depleted),
            _ => throw new InvalidDataException("The saved resource state is invalid."),
        };
    }

    private static string ValueAfterPrefix(string line, string prefix) =>
        line.StartsWith(prefix, StringComparison.Ordinal)
            ? line[prefix.Length..]
            : throw new InvalidDataException($"Expected canonical payload line '{prefix}'.");

    private static int ParseInteger(string value)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException("The saved integer value is invalid.");
        }

        return result;
    }

    private static int ParseNonNegativeInteger(string value)
    {
        var result = ParseInteger(value);
        return result >= 0 ? result : throw new InvalidDataException("The saved quantity must not be negative.");
    }

    private static int ParseBasisPoints(string value)
    {
        var result = ParseNonNegativeInteger(value);
        return result <= 10_000 ? result : throw new InvalidDataException("The saved need must be basis points.");
    }
}
