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
        Assert.True(map.Resources.Count > 20, "Generated worlds need usable sites beyond the starter camp.");
        Assert.All(map.Resources, site => Assert.True(map.IsPassable(site.Position)));
        Assert.DoesNotContain(map.Resources, site => map.CampObjects.Any(item => item.Position == site.Position));
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
        Assert.Equal(WeatherRules.RegionSize, projection.WeatherRegionSize);
        Assert.Equal((256 / WeatherRules.RegionSize) * (128 / WeatherRules.RegionSize),
            projection.WeatherRegions.Count);
        Assert.True(projection.WeatherRegions.Select(region => region.Weather).Distinct().Count() > 1,
            "A generated world must not have one planet-wide weather condition.");
        Assert.All(projection.WeatherRegions,
            region => Assert.InRange(region.SoilMoisture ?? -1, 0, 100));
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
        Assert.True(restored.WorldSystems.Chunks.Count > 1);
        Assert.All(restored.ExportState().Map.Resources, site =>
        {
            var chunk = Assert.Single(restored.WorldSystems.Chunks, item =>
                item.Coordinate == ChunkRules.ToChunkCoordinate(site.Position, item.ChunkSize));
            Assert.Contains(chunk.Resources, item => item.ResourceId == site.Id &&
                item.LocalPosition == ChunkRules.ToLocalPoint(site.Position, chunk.ChunkSize));
        });
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

    [Fact]
    public void ClimateSelectionChangesPlayableGroundAndKeepsPolarCapsOptional()
    {
        var dryOptions = new GeographyOptions("climate-choice", WorldSizePreset.Small,
            ClimateMode: ClimateMode.Uniform, SelectedClimate: ClimateZone.Dry, LatitudeCooling: false);
        var dry = GeographyGenerator.Generate(dryOptions);
        var tropical = GeographyGenerator.Generate(dryOptions with { SelectedClimate = ClimateZone.Tropical });
        Assert.Equal(ClimateZone.Dry, dry.At(50, 50).Climate);
        Assert.Equal(ClimateZone.Tropical, tropical.At(50, 50).Climate);
        Assert.Equal(dry.At(50, 50).Temperature, tropical.At(50, 50).Temperature);
        var dryMap = GeneratedCampMapGenerator.Generate(dryOptions);
        var tropicalMap = GeneratedCampMapGenerator.Generate(dryOptions with { SelectedClimate = ClimateZone.Tropical });
        Assert.Contains(dryMap.Tiles, tile => tile.Terrain == TerrainKind.Sand);
        Assert.Contains(tropicalMap.Tiles, tile => tile.Terrain == TerrainKind.Forest);
        Assert.All(dryMap.Resources.Where(site => site.Id.StartsWith("wild-", StringComparison.Ordinal)),
            site => Assert.True(site.Kind is "stone" or "fiber"));
        Assert.Contains(tropicalMap.Resources, site => site.Id.StartsWith("wild-", StringComparison.Ordinal) &&
            site.Kind == "construction" && site.IsRenewable);
        Assert.NotEqual(dryMap.ManifestDigest, tropicalMap.ManifestDigest);
        Assert.True(MapAcceptance.Validate(dryMap, allowEmptyCamp: true).IsValid);

        var capped = GeographyGenerator.Generate(dryOptions with { LatitudeCooling = true });
        Assert.Equal(ClimateZone.Polar, capped.At(50, 0).Climate);
        Assert.Equal(ClimateZone.Dry, capped.At(50, capped.Height / 2).Climate);

        var dominant = GeographyGenerator.Generate(dryOptions with { ClimateMode = ClimateMode.Dominant });
        var dryLand = 0;
        var otherLand = 0;
        for (var y = 0; y < dominant.Height; y++)
            for (var x = 0; x < dominant.Width; x++)
            {
                var tile = dominant.At(x, y);
                if (tile.Water != WaterKind.Land) continue;
                if (tile.Climate == ClimateZone.Dry) dryLand++;
                else otherLand++;
            }
        Assert.True(dryLand > otherLand, "The selected climate should dominate land.");
        Assert.True(otherLand > 0, "Dominant mode should still allow natural climate regions.");
    }

    [Fact]
    public void RegionalWeatherUsesClimateWithoutChangingTheGlobalCalendar()
    {
        var config = WorldSystemsConfig.Default;
        var dryWetDays = 0;
        var tropicalWetDays = 0;
        for (var day = 0; day < 100; day++)
        {
            var season = WorldCalendarRules.GetSeason(day % config.DaysPerYear, config);
            var dry = WeatherRules.WeatherForRegion("climate-choice", day, season, config,
                1, 1, 4, ClimateZone.Dry);
            var tropical = WeatherRules.WeatherForRegion("climate-choice", day, season, config,
                1, 1, 4, ClimateZone.Tropical);
            if (dry is WeatherKind.Rain or WeatherKind.Storm) dryWetDays++;
            if (tropical is WeatherKind.Rain or WeatherKind.Storm) tropicalWetDays++;
            Assert.NotEqual(WeatherKind.Snow, tropical);
        }
        Assert.True(tropicalWetDays > dryWetDays);
    }

    [Fact]
    public void ResourceAbundanceChangesRealSitesWithoutOverloadingChunks()
    {
        var options = new GeographyOptions("abundance-choice", WorldSizePreset.Small);
        var sparse = GeneratedCampMapGenerator.Generate(options with { ResourceAbundance = ResourceAbundance.Sparse });
        var normal = GeneratedCampMapGenerator.Generate(options);
        var abundant = GeneratedCampMapGenerator.Generate(options with { ResourceAbundance = ResourceAbundance.Abundant });
        Assert.True(sparse.Resources.Count < normal.Resources.Count);
        Assert.True(normal.Resources.Count < abundant.Resources.Count);
        Assert.NotEqual(sparse.ManifestDigest, abundant.ManifestDigest);
        Assert.True(MapAcceptance.Validate(abundant, allowEmptyCamp: true).IsValid);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup,
            geographyOptions: options with { ResourceAbundance = ResourceAbundance.Abundant });
        Assert.All(world.WorldSystems.Chunks,
            chunk => Assert.True(chunk.Resources.Count <= world.WorldSystems.Config.MaxResourcesPerChunk));
    }
}
