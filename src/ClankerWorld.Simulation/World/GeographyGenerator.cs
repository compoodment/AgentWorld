using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

public enum WorldSizePreset
{
    Small,
    Medium,
    Large,
    Huge,
    Mega,
}

public enum WaterKind : byte
{
    Land,
    Ocean,
    Lake,
    River,
}

public sealed record GeographyOptions(
    string Seed,
    WorldSizePreset Size,
    bool WrapEastWest = true,
    int WaterPercent = 45);

public readonly record struct GeographyTile(byte Elevation, byte Rainfall, WaterKind Water);

/// <summary>
/// Compact generated geography. The six-by-five playable camp remains a
/// separate fixture until the save format and renderer can handle large maps.
/// </summary>
public sealed class GeneratedGeography
{
    private readonly byte[] elevation;
    private readonly byte[] rainfall;
    private readonly byte[] water;
    private readonly int[] drainage;

    internal GeneratedGeography(int width, int height, bool wrapsEastWest, byte[] elevation, byte[] rainfall, byte[] water, int[] drainage)
    {
        Width = width;
        Height = height;
        WrapsEastWest = wrapsEastWest;
        this.elevation = elevation;
        this.rainfall = rainfall;
        this.water = water;
        this.drainage = drainage;
    }

    public int Width { get; }
    public int Height { get; }
    public bool WrapsEastWest { get; }
    public GeographyTile At(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        if (x >= Width || y >= Height) throw new ArgumentOutOfRangeException(x >= Width ? nameof(x) : nameof(y));
        var index = (y * Width) + x;
        return new GeographyTile(elevation[index], rainfall[index], (WaterKind)water[index]);
    }

    public int Count(WaterKind kind) => water.Count(value => value == (byte)kind);

    public (int X, int Y)? DownstreamAt(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        if (x >= Width || y >= Height) throw new ArgumentOutOfRangeException(x >= Width ? nameof(x) : nameof(y));
        var index = drainage[(y * Width) + x];
        return index < 0 ? null : (index % Width, index / Width);
    }
}

/// <summary>
/// A deterministic 2D terrain foundation. FastNoiseLite supplies elevation;
/// drainage, lake/ocean classification, and rivers are separate map passes.
/// The preset dimensions are provisional benchmarking targets, not final art
/// or gameplay scale promises.
/// </summary>
public static class GeographyGenerator
{
    public const int ChunkSize = 64;

    public static (int Width, int Height) Dimensions(WorldSizePreset size) => size switch
    {
        WorldSizePreset.Small => (256, 128),
        WorldSizePreset.Medium => (512, 256),
        WorldSizePreset.Large => (1024, 512),
        WorldSizePreset.Huge => (2048, 1024),
        WorldSizePreset.Mega => (4096, 2048),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    public static GeneratedGeography Generate(GeographyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Seed);
        if (options.WaterPercent is < 10 or > 80)
            throw new ArgumentOutOfRangeException(nameof(options), "Water percentage must be between 10 and 80.");

        var (width, height) = Dimensions(options.Size);
        var length = checked(width * height);
        var elevation = new byte[length];
        var rainfall = new byte[length];
        var water = new byte[length];
        var elevationNoise = NewNoise(NoiseSeed(options.Seed, "elevation"), 0.012f);
        var rainNoise = NewNoise(NoiseSeed(options.Seed, "rainfall"), 0.018f);

        // A circle in noise-input space makes the *flat* map's east and west
        // edges neighbors. Its third coordinate never becomes a game axis.
        var radius = width / (2f * MathF.PI);
        var circleX = new float[width];
        var circleZ = new float[width];
        if (options.WrapEastWest)
            for (var x = 0; x < width; x++)
            {
                var angle = 2f * MathF.PI * x / width;
                circleX[x] = radius * MathF.Cos(angle);
                circleZ[x] = radius * MathF.Sin(angle);
            }
        var histogram = new int[256];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = options.WrapEastWest
                    ? elevationNoise.GetNoise(circleX[x], y, circleZ[x])
                    : elevationNoise.GetNoise(x, y);
                var wetness = options.WrapEastWest
                    ? rainNoise.GetNoise(circleX[x], y, circleZ[x])
                    : rainNoise.GetNoise(x, y);
                var scaled = (byte)Math.Clamp((int)MathF.Round((value + 1f) * 127.5f), 0, 255);
                elevation[(y * width) + x] = scaled;
                rainfall[(y * width) + x] = (byte)Math.Clamp((int)MathF.Round((wetness + 1f) * 127.5f), 0, 255);
                histogram[scaled]++;
            }
        }

        var targetWaterTiles = length * options.WaterPercent / 100;
        var waterLevel = 0;
        var covered = 0;
        while (waterLevel < histogram.Length - 1 && covered + histogram[waterLevel] < targetWaterTiles)
            covered += histogram[waterLevel++];
        for (var index = 0; index < length; index++)
            if (elevation[index] <= waterLevel) water[index] = (byte)WaterKind.Lake;

        ClassifyOceans(water, width, height, options.WrapEastWest);
        var drainage = RouteRivers(elevation, rainfall, water, width, height, options.WrapEastWest);
        return new GeneratedGeography(width, height, options.WrapEastWest, elevation, rainfall, water, drainage);
    }

    private static FastNoiseLite NewNoise(int seed, float frequency)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(4);
        noise.SetFrequency(frequency);
        return noise;
    }

    private static int NoiseSeed(string worldSeed, string layer)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(worldSeed + ":" + layer));
        return BinaryPrimitives.ReadInt32LittleEndian(digest);
    }

    private static void ClassifyOceans(byte[] water, int width, int height, bool wrap)
    {
        var visited = new bool[water.Length];
        var largest = new List<int>();
        var borderBodies = new List<int>();
        var queue = new Queue<int>();
        Span<int> neighbors = stackalloc int[4];
        for (var start = 0; start < water.Length; start++)
        {
            if (water[start] != (byte)WaterKind.Lake || visited[start]) continue;
            var component = new List<int>();
            var touchesBorder = false;
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var current))
            {
                component.Add(current);
                var x = current % width;
                var y = current / width;
                touchesBorder |= y == 0 || y == height - 1 || (!wrap && (x == 0 || x == width - 1));
                var neighborCount = WriteNeighbors(current, width, height, wrap, neighbors);
                for (var neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    var neighbor = neighbors[neighborIndex];
                    if (water[neighbor] != (byte)WaterKind.Lake || visited[neighbor]) continue;
                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            if (component.Count > largest.Count) largest = component;
            if (touchesBorder) borderBodies.AddRange(component);
        }

        // Open-map border water is sea; on a wrapped cylindrical map there is
        // no east/west border, so keep the largest sea even if it misses a pole.
        foreach (var index in wrap ? largest : borderBodies) water[index] = (byte)WaterKind.Ocean;
        if (water.All(value => value != (byte)WaterKind.Ocean))
            foreach (var index in largest) water[index] = (byte)WaterKind.Ocean;
    }

    private static int[] RouteRivers(byte[] elevation, byte[] rainfall, byte[] water, int width, int height, bool wrap)
    {
        var length = water.Length;
        var floodedHeight = new int[length];
        var downstream = new int[length];
        var flow = new int[length];
        var visited = new bool[length];
        var visitedOrder = new List<int>(length);
        var frontier = new PriorityQueue<int, (int Height, int Index)>();
        Span<int> neighbors = stackalloc int[4];
        Array.Fill(downstream, -1);

        for (var index = 0; index < length; index++)
        {
            if (water[index] != (byte)WaterKind.Ocean) continue;
            visited[index] = true;
            floodedHeight[index] = elevation[index] * 1024;
            frontier.Enqueue(index, (floodedHeight[index], index));
        }

        while (frontier.TryDequeue(out var current, out _))
        {
            visitedOrder.Add(current);
            var neighborCount = WriteNeighbors(current, width, height, wrap, neighbors);
            for (var neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
            {
                var neighbor = neighbors[neighborIndex];
                if (visited[neighbor]) continue;
                visited[neighbor] = true;
                // Raising a trapped cell just enough to reach an outlet
                // prevents cycles while keeping its river path deterministic.
                floodedHeight[neighbor] = Math.Max(elevation[neighbor] * 1024, floodedHeight[current] + 1);
                downstream[neighbor] = current;
                frontier.Enqueue(neighbor, (floodedHeight[neighbor], neighbor));
            }
        }

        for (var order = visitedOrder.Count - 1; order >= 0; order--)
        {
            var index = visitedOrder[order];
            if (water[index] == (byte)WaterKind.Ocean) continue;
            flow[index] += 1 + (rainfall[index] / 64);
            var outlet = downstream[index];
            if (outlet >= 0) flow[outlet] += flow[index];
        }

        // Larger catchments have wider/longer rivers. The threshold is a
        // provisional visual-tuning value, not a climate or hydrology law.
        for (var index = 0; index < length; index++)
            if (water[index] == (byte)WaterKind.Land && flow[index] >= 144)
                water[index] = (byte)WaterKind.River;
        return downstream;
    }

    private static int WriteNeighbors(int index, int width, int height, bool wrap, Span<int> result)
    {
        var x = index % width;
        var y = index / width;
        var count = 0;
        if (y > 0) result[count++] = index - width;
        if (x + 1 < width) result[count++] = index + 1;
        else if (wrap) result[count++] = index - x;
        if (y + 1 < height) result[count++] = index + width;
        if (x > 0) result[count++] = index - 1;
        else if (wrap) result[count++] = index + width - 1;
        return count;
    }
}
