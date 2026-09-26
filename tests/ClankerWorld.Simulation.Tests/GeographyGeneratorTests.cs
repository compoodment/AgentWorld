using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class GeographyGeneratorTests
{
    [Theory]
    [InlineData(WorldSizePreset.Small)]
    [InlineData(WorldSizePreset.Medium)]
    public void GeneratedGeographySupportsAnEmptyPlayableCampAndNoBuildHighGround(WorldSizePreset size)
    {
        var map = GeneratedCampMapGenerator.Generate(new GeographyOptions(
            "river-world-a", size, WrapEastWest: true));

        Assert.Equal(GeographyGenerator.Dimensions(size), (map.Width, map.Height));
        Assert.True(MapAcceptance.Validate(map, allowEmptyCamp: true).IsValid);
        Assert.DoesNotContain(map.CampObjects, item => item.Kind == "founder");
        Assert.Contains(map.Tiles, item => item.Terrain == TerrainKind.River);
        Assert.Contains(map.Tiles, item => item.Terrain == TerrainKind.Mountain);
        Assert.All(map.Tiles.Where(item => item.Terrain is TerrainKind.Mountain or TerrainKind.Peak),
            item => Assert.False(map.IsBuildable(item.Position)));
        Assert.All(map.CampObjects, item => Assert.True(map.IsPassable(item.Position)));
    }

    [Fact]
    public async Task GeneratedCampCanStartAdvanceAndRestoreWithItsOwnGeography()
    {
        var options = new GeographyOptions("generated-life", WorldSizePreset.Small, WrapEastWest: true);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var initial = world.ExportState();
        Assert.Equal(options, initial.Geography);
        Assert.Empty(world.Inhabitants);
        Assert.True(world.Society.IsPaused);
        var projection = new OwnerWorldObservationStore(world).GetSnapshot();
        Assert.Empty(projection.Tiles);
        Assert.Equal((256, 128), (projection.PackedTerrain?.Width, projection.PackedTerrain?.Height));
        Assert.Equal("terrain-kind-v1", projection.PackedTerrain!.Encoding);
        var terrainBytes = Convert.FromBase64String(projection.PackedTerrain.Data);
        Assert.Equal(initial.Map.Tiles.Count, terrainBytes.Length);
        Assert.Equal((byte)initial.Map.Tiles[0].Terrain, terrainBytes[0]);
        var bedroll = initial.Map.GetObject("bedroll").Position;
        var positions = initial.Map.Tiles.Where(tile =>
                tile.Position.X >= bedroll.X - 1 && tile.Position.X < bedroll.X + 5 &&
                tile.Position.Y >= bedroll.Y && tile.Position.Y < bedroll.Y + 5 &&
                initial.Map.IsPassable(tile.Position) &&
                !initial.Map.CampObjects.Any(item => item.Position == tile.Position) &&
                !initial.Map.Resources.Any(item => item.Position == tile.Position))
            .Take(4).Select(tile => tile.Position).ToArray();
        Assert.Equal(4, positions.Length);
        for (var index = 0; index < positions.Length; index++)
            world.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), positions[index]);
        world.StartWorld();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(saved);
        Assert.Equal(1, restored.WorldTick);
        Assert.Equal(initial.Map.ManifestDigest, restored.ExportState().Map.ManifestDigest);
        Assert.Equal(options, restored.ExportState().Geography);
        Assert.Equal(4, restored.Inhabitants.Count);
    }

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
