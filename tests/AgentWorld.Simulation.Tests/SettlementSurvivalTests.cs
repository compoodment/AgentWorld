using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Kernel;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.World;
using AgentWorld.Viewer.Observation;

namespace AgentWorld.Simulation.Tests;

public sealed class SettlementSurvivalTests
{
    [Fact]
    public async Task UrgentColdStillAllowsProtectiveConstruction()
    {
        using var seed = new PrivateWorldRuntime("cold-bootstrap", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = WithWeather(seed.ExportState(), WeatherKind.Snow);
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person with
            { Survival = person.Survival! with { WarmthBasisPoints = 1_000 } }).ToArray()
        };
        using var world = PrivateWorldRuntime.Restore(state);
        for (var tick = 0; tick < 80; tick++) await world.AdvanceOneTickAsync();
        Assert.Contains(world.WorldSimulation.Buildings, building => world.WorldContent.Buildings.Any(definition =>
            definition.CanonicalId == building.DefinitionId && definition.Tags.Any(tag => tag is "shelter" or "cooking" or "warmth")));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "fire_fuelled");
    }

    [Fact]
    public async Task SpoiledReservedIngredientCancelsProductionWithoutStoppingTicks()
    {
        using var seed = new PrivateWorldRuntime("spoiled-production", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var worker = state.Inhabitants[0];
        var position = state.Map.Tiles.First(tile => state.Map.IsPassable(tile.Position) &&
            !state.Map.CampObjects.Any(item => item.Position == tile.Position) && !state.Map.Resources.Any(item => item.Position == tile.Position) &&
            !state.Inhabitants.Any(person => person.InhabitantId != worker.InhabitantId && person.Position == tile.Position)).Position;
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == worker.InhabitantId
            ? person with { Position = position } : person).ToArray()
        };
        using var preparing = PrivateWorldRuntime.Restore(state, _ => new IdleProvider());
        var fire = preparing.WorldContent.Buildings.Single(building => building.LocalId == "fire");
        var placed = preparing.PlaceBuilding("spoilage-test-fire", fire.CanonicalId, position);
        Assert.True(placed.Applied, placed.Failure);
        var recipe = preparing.WorldContent.Recipes.Single(recipe => recipe.LocalId == "meal");
        var started = preparing.StartProduction(recipe.CanonicalId, placed.InstanceId, worker.InhabitantId);
        Assert.True(started.Applied, started.Failure);
        var pending = preparing.ExportState();
        pending = pending with
        {
            Society = pending.Society with
            {
                Society = pending.Society.Society with
                {
                    Inventory = pending.Society.Society.Inventory with
                    {
                        Lots = pending.Society.Society.Inventory.Lots.Select(lot =>
                    lot.ItemKind == "food" ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray()
                    }
                }
            }
        };
        using var world = PrivateWorldRuntime.Restore(pending, _ => new IdleProvider());
        for (var tick = 0; tick <= recipe.DurationTicks; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldProductionJobState.Cancelled, world.WorldSimulation.ProductionJobs.Single(job => job.JobId == started.JobId).State);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id.StartsWith(started.JobId + ":output:", StringComparison.Ordinal));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "production_input_unusable");
    }

    [Theory]
    [InlineData(WeatherKind.Clear, 6)]
    [InlineData(WeatherKind.Snow, 3)]
    [InlineData(WeatherKind.Storm, 4)]
    public async Task CropFoodYieldReflectsWeather(WeatherKind weather, int expected)
    {
        using var seed = new PrivateWorldRuntime("crop-weather", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = WithWeather(seed.ExportState(), weather);
        var site = state.Map.GetResource(SeededMapGenerator.FertileLandResourceId).Position;
        var worker = state.Inhabitants[0];
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == worker.InhabitantId
            ? person with { Position = site } : person.Position == site ? person with { Position = worker.Position } : person).ToArray()
        };
        using var world = PrivateWorldRuntime.Restore(state, _ => new IdleProvider());
        var recipe = world.WorldContent.Recipes.Single(recipe => recipe.LocalId == "vegetables");
        var started = world.StartProduction(recipe.CanonicalId, WorldBuildSiteRules.FertileLandSiteId(site), worker.InhabitantId);
        Assert.True(started.Applied, started.Failure);
        for (var tick = 0; tick < recipe.DurationTicks; tick++) await world.AdvanceOneTickAsync();
        Assert.Equal(expected, world.Society.Inventory.Lots.Single(lot => lot.Id == started.JobId + ":output:00").Quantity);
    }

    [Fact]
    public async Task DifferentFoodSourceImprovesDietAfterInventoryTransfer()
    {
        using var seed = new PrivateWorldRuntime("varied-food", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var worker = state.Inhabitants[0];
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "food:harvest:fixture", "food", "household:camp-alpha", 2);
        inventory = InventoryFixture.Transfer(inventory, "varied", "household:camp-alpha", worker.InhabitantId, "food:harvest:fixture", 1, "food_share");
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == worker.InhabitantId ? person with
            { HungerBasisPoints = 1_000, Survival = person.Survival! with { LastMealKind = "camp_rations" } } : person).ToArray(),
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
        };
        using var world = PrivateWorldRuntime.Restore(state);
        await world.AdvanceOneTickAsync();
        var eater = world.Inhabitants.Single(person => person.InhabitantId == worker.InhabitantId);
        Assert.Equal("foraged", eater.Survival!.LastMealKind);
        Assert.True(eater.Survival.NutritionBasisPoints > 5_000);
    }

    [Fact]
    public async Task ColdSettlementUsesFuelAndEquipmentAndCanRecoverAcrossRestart()
    {
        using var seed = new PrivateWorldRuntime("cold-settlement");
        var initial = WithWeather(seed.ExportState(), WeatherKind.Snow);
        using var world = PrivateWorldRuntime.Restore(initial);
        world.StageStarterContent();
        for (var tick = 0; tick < 600; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var state = world.ExportState();
        Assert.NotNull(state.Survival);
        Assert.Contains(state.Events, item => item.Kind == "fire_fuelled");
        Assert.Contains(state.Events, item => item.Kind == "equipment_collected" && item.Detail.EndsWith(":tool", StringComparison.Ordinal));
        Assert.Contains(state.Events, item => item.Kind == "equipment_collected" && item.Detail.EndsWith(":clothing", StringComparison.Ordinal));
        Assert.Contains(state.Events, item => item.Kind == "survival_condition_changed");
        Assert.Contains(state.Inhabitants, person => person.Survival!.WarmthBasisPoints > 6_000);
        Assert.All(new OwnerWorldObservationStore(world).GetSnapshot().Inhabitants, person => Assert.NotNull(person.Survival));
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state)));
        Assert.Equal(PrivateWorldRuntimeCodec.Encode(state), PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task ClothingReducesSnowExposureWithoutInventingHeat()
    {
        using var seed = new PrivateWorldRuntime("clothing-comparison", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++)
        {
            await seed.AdvanceOneTickAsync();
        }
        var state = WithWeather(seed.ExportState(), WeatherKind.Snow);
        var actor = state.Inhabitants[0].InhabitantId;
        var clothedInventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "test-clothing", "clothing", actor, 1);
        var clothed = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = clothedInventory } } };
        using var exposedWorld = PrivateWorldRuntime.Restore(state, _ => new IdleProvider());
        using var clothedWorld = PrivateWorldRuntime.Restore(clothed, _ => new IdleProvider());
        for (var tick = 0; tick < 80; tick++)
        {
            await exposedWorld.AdvanceOneTickAsync();
            await clothedWorld.AdvanceOneTickAsync();
        }
        var exposedWarmth = exposedWorld.Inhabitants.Single(person => person.InhabitantId == actor).Survival!.WarmthBasisPoints;
        var clothedWarmth = clothedWorld.Inhabitants.Single(person => person.InhabitantId == actor).Survival!.WarmthBasisPoints;
        Assert.True(clothedWarmth > exposedWarmth);
        Assert.True(clothedWarmth < 10_000);
    }

    [Fact]
    public async Task ExposureIllnessRecoversWithWarmthAndFood()
    {
        using var seed = new PrivateWorldRuntime("illness-recovery", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++)
        {
            await seed.AdvanceOneTickAsync();
        }
        var original = WithWeather(seed.ExportState(), WeatherKind.Clear);
        var sick = original with
        {
            Inhabitants = original.Inhabitants.Select(person => person with
            {
                Survival = new SurvivalCondition(7_000, 5_000),
            }).ToArray()
        };
        using var world = PrivateWorldRuntime.Restore(sick, _ => new IdleProvider());
        for (var tick = 0; tick < 100; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.All(world.Inhabitants, person =>
        {
            Assert.True(person.Survival!.IllnessBasisPoints < 5_000);
            Assert.True(person.Survival.WarmthBasisPoints > 7_000);
        });
        world.Pause();
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void FoodStorageSlowsDecayWithoutRottingTools()
    {
        using var world = new PrivateWorldRuntime("food-storage");
        var inventory = world.ExportState().Society.Society.Inventory;
        var kinds = new HashSet<string>(StringComparer.Ordinal) { "food" };
        var protectedOwners = new HashSet<string>(StringComparer.Ordinal) { "household:camp-alpha" };
        var exposed = InventoryFixture.ProcessSpoilage(inventory, 100, 4, kinds);
        var stored = InventoryFixture.ProcessSpoilage(inventory, 100, 4, kinds, protectedOwners);
        Assert.Equal(9_600, exposed.Lots.First(lot => lot.ItemKind == "food").FreshnessBasisPoints);
        Assert.Equal(9_800, stored.Lots.First(lot => lot.ItemKind == "food").FreshnessBasisPoints);
        Assert.All(stored.Lots.Where(lot => lot.ItemKind != "food"), lot => Assert.Equal(10_000, lot.FreshnessBasisPoints));
    }

    private static PrivateWorldRuntimeState WithWeather(PrivateWorldRuntimeState state, WeatherKind weather)
    {
        var systems = state.WorldSystems!;
        var profiles = Enum.GetValues<SeasonKind>().Select(season => new WeatherProfile(season,
            weather == WeatherKind.Clear ? 1 : 0, 0, weather == WeatherKind.Rain ? 1 : 0,
            weather == WeatherKind.Storm ? 1 : 0, weather == WeatherKind.Snow ? 1 : 0)).ToArray();
        return state with
        {
            WorldSystems = systems with
            {
                Config = systems.Config with { WeatherProfiles = profiles },
                Climate = systems.Climate with { Weather = weather },
            }
        };
    }

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == "safe_idle" ? 1d : 0d)));
    }
}
