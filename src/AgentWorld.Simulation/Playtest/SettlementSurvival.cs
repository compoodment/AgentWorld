using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Kernel;
using AgentWorld.Simulation.World;

namespace AgentWorld.Simulation.Playtest;

public sealed record SurvivalCondition(int WarmthBasisPoints = 10_000, int IllnessBasisPoints = 0,
    int NutritionBasisPoints = 5_000, string? LastMealKind = null);
public sealed record CampFireState(string BuildingId, long FuelUntilTick);
public sealed record SettlementSurvivalState(long ActivatedTick, IReadOnlyList<CampFireState> Fires);

public sealed partial class PrivateWorldRuntime
{
    private SettlementSurvivalState? survivalState;
    private static readonly HashSet<string> PerishableKinds = new(StringComparer.Ordinal) { "food" };

    private static bool HasUrgentExposure(PlaytestInhabitantState person) =>
        person.Survival is { WarmthBasisPoints: < 3_500 } or { IllnessBasisPoints: >= 6_500 };

    private bool IsProtectiveProject(SettlementProject? project) => project is not null &&
        (worldContent.Buildings.Any(building => project.CandidateId == "build:building:" + building.CanonicalId &&
            building.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking")) ||
         worldContent.Recipes.Any(recipe => project.CandidateId == "build:recipe:" + recipe.CanonicalId &&
            recipe.Outputs.Any(output => output.ResourceId == "clothing")));

    private int WeatherExposure => worldSystems.Climate.Weather switch
    {
        WeatherKind.Snow => 60,
        WeatherKind.Storm => 55,
        WeatherKind.Rain => 25,
        _ => worldSystems.Climate.Season == SeasonKind.Winter ? 40 : 0,
    };

    private bool HasCarriedItem(string actor, string kind) => society.Checkpoint.Inventory.Lots.Any(lot =>
        lot.OwnerId == actor && lot.ItemKind == kind && AvailableLotQuantity(lot) > 0);

    private InventoryLot? SharedItem(string kind) => society.Checkpoint.Inventory.Lots.FirstOrDefault(lot =>
        lot.OwnerId == HouseholdId && lot.ItemKind == kind && AvailableLotQuantity(lot) > 0);

    private IEnumerable<PlacedBuilding> BuildingsWithTag(string tag) => worldSimulation.Buildings.Where(building =>
        worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(tag, StringComparer.Ordinal)));

    private bool NearShelter(GridPoint point) => BuildingsWithTag("shelter").Any(building =>
        IsWithinInteractionRange(point, building.Position, ResourceInteractionRange));

    private bool IsFireLit(PlacedBuilding building) => survivalState?.Fires.Any(fire =>
        fire.BuildingId == building.InstanceId && fire.FuelUntilTick > WorldTick) == true;

    private IEnumerable<PlacedBuilding> HeatingBuildings() => BuildingsWithTag("cooking").Concat(BuildingsWithTag("warmth"))
        .DistinctBy(building => building.InstanceId);

    private void AdvanceSettlementSurvival()
    {
        if (survivalState is null && !contentRegistry.ExportState().Packages.Any(package => package.Manifest.PackageId == SettlementContent.PackageId &&
            package.Lifecycle == ContentPackageLifecycle.Active))
        {
            return;
        }
        if (survivalState is null)
        {
            survivalState = new SettlementSurvivalState(WorldTick, []);
            // Do not retroactively rot years of legacy inventory on migration.
            ApplyInventoryTransition(inventory => InventoryFixture.ProcessSpoilage(inventory, WorldTick, 0));
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent("survival_activated", "weather_equipment_and_food");
        }
        var existingIds = worldSimulation.Buildings.Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
        foreach (var fire in survivalState.Fires.Where(fire => fire.FuelUntilTick <= WorldTick || !existingIds.Contains(fire.BuildingId)))
        {
            AppendEvent("fire_extinguished", fire.BuildingId);
        }
        survivalState = survivalState with
        {
            Fires = survivalState.Fires.Where(fire =>
            fire.FuelUntilTick > WorldTick && existingIds.Contains(fire.BuildingId)).ToArray()
        };
        var shelteredOwners = BuildingsWithTag("storage").Any() ? new HashSet<string>(StringComparer.Ordinal) { HouseholdId } : null;
        ApplyInventoryTransition(inventory => InventoryFixture.ProcessSpoilage(inventory, WorldTick, 4,
            PerishableKinds, shelteredOwners));
        foreach (var person in inhabitants.Values.ToArray())
        {
            var old = person.Survival ?? new SurvivalCondition();
            var protection = (HasCarriedItem(person.InhabitantId, "clothing") ? 35 : 0) + (NearShelter(person.Position) ? 45 : 0);
            var heat = HeatingBuildings().Any(building => IsFireLit(building) &&
                IsWithinInteractionRange(person.Position, building.Position, 2)) ? 90 : 0;
            var loss = Math.Max(0, WeatherExposure - protection);
            var warmth = Math.Clamp(old.WarmthBasisPoints - loss + heat + (loss == 0 ? 20 : 0), 0, 10_000);
            var illness = Math.Clamp(old.IllnessBasisPoints + (warmth < 2_500 || person.HungerBasisPoints < 500 ? 8 :
                warmth > 6_000 && person.HungerBasisPoints > 3_500 ? -12 : 0), 0, 10_000);
            inhabitants[person.InhabitantId] = person with { Survival = old with { WarmthBasisPoints = warmth, IllnessBasisPoints = illness } };
            if (old.WarmthBasisPoints / 2_500 != warmth / 2_500 || old.IllnessBasisPoints / 2_500 != illness / 2_500)
            {
                AppendEvent("survival_condition_changed", $"{person.InhabitantId}:warmth={warmth}:illness={illness}");
            }
        }
    }

    private void AddSurvivalCandidates(List<CognitionCandidate> candidates, string actor, PlaytestInhabitantState person)
    {
        if (person.Survival is not { } condition)
        {
            return;
        }
        if (WeatherExposure > 0 && !HasCarriedItem(actor, "clothing") && SharedItem("clothing") is not null)
        {
            candidates.Add(new CognitionCandidate("wear_clothing", "Collect woven clothing from camp to reduce exposure.", 3));
        }
        if (AdultResident(actor) && WeatherExposure > 0 && HeatingBuildings().Any(building => !IsFireLit(building)) &&
            (SharedItem("wood") is not null || HasCarriedItem(actor, "wood") || MaterialSource("wood") is not null))
        {
            candidates.Add(new CognitionCandidate("tend_fire", "Carry household wood to a hearth and keep the camp warm.", condition.WarmthBasisPoints < 6_000 ? 2 : 4));
        }
        if (condition.WarmthBasisPoints < 6_000 && (HeatingBuildings().Any(IsFireLit) || BuildingsWithTag("shelter").Any()))
        {
            candidates.Add(new CognitionCandidate("seek_warmth", "Seek a lit hearth or shelter until warm enough to work.", 3));
        }
        if (condition.IllnessBasisPoints >= 4_000 && !candidates.Any(candidate => candidate.Id == "sleep"))
        {
            candidates.Add(new CognitionCandidate("sleep", "Rest, stay warm and eat to recover from exposure illness.", 4));
        }
    }

    private void CollectEquipment(string actor, PlaytestInhabitantState person, string kind)
    {
        if (HasCarriedItem(actor, kind) || SharedItem(kind) is not { } item)
        {
            return;
        }
        var storage = map.GetObject("storage").Position;
        if (!IsWithinInteractionRange(person.Position, storage, ResourceInteractionRange))
        {
            MoveToward(actor, person, storage, "equipment", ResourceInteractionRange);
            return;
        }
        ApplyInventoryTransition(inventory => InventoryFixture.Transfer(inventory, $"equipment:{WorldTick}:{actor}:{kind}",
            HouseholdId, actor, item.Id, 1, "equipment_collected"));
        AppendEvent("equipment_collected", $"{actor}:{kind}");
    }

    private void TendFire(string actor, PlaytestInhabitantState person)
    {
        var building = HeatingBuildings().FirstOrDefault(building => !IsFireLit(building));
        if (building is null || survivalState is null)
        {
            return;
        }
        if (!HasCarriedItem(actor, "wood"))
        {
            if (SharedItem("wood") is not null)
            {
                CollectEquipment(actor, person, "wood");
            }
            else if (MaterialSource("wood") is { } source)
            {
                GatherProjectMaterial(actor, person, "wood", source);
            }
            return;
        }
        if (!IsWithinInteractionRange(person.Position, building.Position, ResourceInteractionRange))
        {
            MoveToward(actor, person, building.Position, "fuel_fire", ResourceInteractionRange);
            return;
        }
        var fuel = society.Checkpoint.Inventory.Lots.First(lot => lot.OwnerId == actor && lot.ItemKind == "wood" && AvailableLotQuantity(lot) > 0);
        society.Apply(checkpoint => AgentWorld.Simulation.Society.SocietyFixture.ConsumeInventory(checkpoint, actor, fuel.Id, 1, "heating_fuel"));
        survivalState = survivalState with { Fires = survivalState.Fires.Append(new CampFireState(building.InstanceId, WorldTick + 120)).ToArray() };
        AppendEvent("fire_fuelled", building.InstanceId);
    }

    private void SeekWarmth(string actor, PlaytestInhabitantState person)
    {
        var destination = HeatingBuildings().FirstOrDefault(IsFireLit) ?? BuildingsWithTag("shelter").FirstOrDefault();
        if (destination is not null && !IsWithinInteractionRange(person.Position, destination.Position, ResourceInteractionRange))
        {
            MoveToward(actor, person, destination.Position, "warmth", ResourceInteractionRange);
        }
    }

    private int RestRecovery(PlaytestInhabitantState person) => person.Survival is null ? 2_500 :
        1_500 + (NearShelter(person.Position) ? 800 : 0) +
        (NearShelter(person.Position) && SharedItem("bedding") is not null ? 700 : 0);

    private bool NeedsRecipeOutput(RecipeDefinition recipe) => survivalState is null || recipe.Outputs.Any(output =>
    {
        var available = society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == output.ResourceId)
            .Sum(AvailableLotQuantity);
        var target = output.ResourceId == "food" ? inhabitants.Count * 4 : Math.Max(1, inhabitants.Count);
        return available < target;
    });

    private int CropOutputQuantity(RecipeDefinition recipe, ContentQuantity output) =>
        !recipe.IsCrop || survivalState is null || output.ResourceId != "food" ? output.Amount :
        worldSystems.Climate.Weather switch
        {
            WeatherKind.Snow => Math.Max(1, output.Amount / 2),
            WeatherKind.Storm => Math.Max(1, checked((int)((long)output.Amount * 3 / 4))),
            _ => output.Amount,
        };

    private string FoodSource(InventoryLot lot)
    {
        var inventory = society.Checkpoint.Inventory;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var originId = lot.Id;
        while (lot.ProvenanceLotId is { } parent && visited.Add(lot.Id))
        {
            originId = parent;
            if (inventory.Lots.FirstOrDefault(item => item.Id == parent) is not { } source)
            {
                break;
            }
            lot = source;
        }
        var job = worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
            .FirstOrDefault(job => originId.StartsWith(job.JobId + ":output:", StringComparison.Ordinal));
        if (job is not null)
        {
            return worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == job.RecipeId)?.IsCrop == true ? "crops" : "cooked";
        }
        return originId.StartsWith("food:harvest:", StringComparison.Ordinal) ? "foraged" : "camp_rations";
    }

    private IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null)
    {
        var previous = actor is not null && inhabitants.TryGetValue(actor, out var person) ? person.Survival?.LastMealKind : null;
        return society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == owner && lot.ItemKind == "food" && AvailableLotQuantity(lot) > 0)
            .OrderBy(lot => previous is not null && FoodSource(lot) == previous ? 1 : 0)
            .ThenByDescending(lot => lot.FreshnessBasisPoints).ThenBy(lot => lot.Id, StringComparer.Ordinal);
    }

    private SurvivalCondition? AfterMeal(PlaytestInhabitantState person, InventoryLot food)
    {
        if (person.Survival is not { } condition)
        {
            return null;
        }
        var kind = FoodSource(food);
        var nutrition = Math.Clamp(condition.NutritionBasisPoints + (condition.LastMealKind is null ? 0 : condition.LastMealKind != kind ? 500 : -100), 0, 10_000);
        var illness = Math.Clamp(condition.IllnessBasisPoints + (food.FreshnessBasisPoints < 2_500 ? 300 : -100), 0, 10_000);
        AppendEvent("meal_eaten", $"{person.InhabitantId}:{kind}:nutrition={nutrition}");
        return condition with { NutritionBasisPoints = nutrition, LastMealKind = kind, IllnessBasisPoints = illness };
    }

    private static void ValidateSurvival(PrivateWorldRuntimeState state)
    {
        if (state.Survival is { } survival && (state.SchemaVersion < 6 || survival.ActivatedTick < 0 ||
            survival.ActivatedTick > state.Society.Society.WorldTick || survival.Fires.Count > (state.WorldSimulation?.Buildings.Count ?? 0) ||
            survival.Fires.Select(fire => fire.BuildingId).Distinct(StringComparer.Ordinal).Count() != survival.Fires.Count ||
            survival.Fires.Any(fire => fire.FuelUntilTick <= state.Society.Society.WorldTick || fire.FuelUntilTick - state.Society.Society.WorldTick > 120 ||
                state.WorldSimulation?.Buildings.Any(building => building.InstanceId == fire.BuildingId) != true)))
        {
            throw new InvalidDataException("The saved settlement survival state is invalid.");
        }
        foreach (var person in state.Inhabitants)
        {
            if (person.Survival is { } condition && (state.SchemaVersion < 6 || state.Survival is null ||
                condition.WarmthBasisPoints is < 0 or > 10_000 || condition.IllnessBasisPoints is < 0 or > 10_000 ||
                condition.NutritionBasisPoints is < 0 or > 10_000 || condition.LastMealKind is not (null or "crops" or "cooked" or "foraged" or "camp_rations")))
            {
                throw new InvalidDataException("The saved inhabitant survival condition is invalid.");
            }
        }
    }
}
