using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Tests;

public sealed class GeographyGeneratorTests
{
    [Theory]
    [InlineData("river-world-a", true)]
    [InlineData("river-world-b", false)]
    public void GeneratedRiversFollowAnAcyclicRouteToWater(string seed, bool wrap)
    {
        var map = GeographyGenerator.Generate(new GeographyOptions(seed, WorldSizePreset.Small, wrap));

        Assert.Equal(0, map.Width % GeographyGenerator.ChunkSize);
        Assert.Equal(0, map.Height % GeographyGenerator.ChunkSize);
        Assert.True(map.Count(WaterKind.Ocean) > 0);
        Assert.True(map.Count(WaterKind.River) > 0);

        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
            {
                if (map.At(x, y).Water != WaterKind.River) continue;
                var visited = new HashSet<(int X, int Y)>();
                var cursor = (X: x, Y: y);
                while (map.At(cursor.X, cursor.Y).Water is WaterKind.Land or WaterKind.River)
                {
                    Assert.True(visited.Add(cursor), $"Drainage cycle from {x},{y}");
                    var next = map.DownstreamAt(cursor.X, cursor.Y);
                    Assert.NotNull(next);
                    Assert.Equal(1, Math.Abs(next.Value.Y - cursor.Y) +
                        Math.Min(Math.Abs(next.Value.X - cursor.X),
                            wrap ? map.Width - Math.Abs(next.Value.X - cursor.X) : map.Width));
                    cursor = next.Value;
                }

                Assert.True(map.At(cursor.X, cursor.Y).Water is WaterKind.Ocean or WaterKind.Lake);
            }
    }

    [Fact]
    public void SeedAndOptionsDetermineTheGeography()
    {
        var options = new GeographyOptions("world-one", WorldSizePreset.Small);
        var first = GeographyGenerator.Generate(options);
        var again = GeographyGenerator.Generate(options);
        var different = GeographyGenerator.Generate(options with { Seed = "world-two" });
        var changed = 0;
        for (var y = 0; y < first.Height; y++)
            for (var x = 0; x < first.Width; x++)
            {
                Assert.Equal(first.At(x, y), again.At(x, y));
                if (first.At(x, y) != different.At(x, y)) changed++;
            }

        Assert.True(changed > first.Width);
    }
}
