using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Harness;

namespace AgentWorld.Simulation.Playtest;

public sealed record PlacedBuilding(
    string InstanceId,
    string DefinitionId,
    GridPoint Position,
    long PlacedTick);

public enum WorldProductionJobState
{
    Running,
    Completed,
    Cancelled,
}

public sealed record WorldProductionJob(
    string JobId,
    string RecipeId,
    string BuildingInstanceId,
    string WorkerId,
    long StartedTick,
    long CompletionTick,
    WorldProductionJobState State,
    IReadOnlyList<string> InputReservationIds);

public sealed record WorldContentSimulationState(
    IReadOnlyList<PlacedBuilding> Buildings,
    IReadOnlyList<WorldProductionJob> ProductionJobs,
    long NextProductionJobSequence)
{
    public static WorldContentSimulationState Empty { get; } = new([], [], 1);
}

public sealed record BuildingPlacementResult(
    bool Applied,
    string InstanceId,
    string DefinitionId,
    GridPoint Position,
    string? Failure)
{
    public static BuildingPlacementResult Success(
        PlacedBuilding building) => new(
            true,
            building.InstanceId,
            building.DefinitionId,
            building.Position,
            null);

    public static BuildingPlacementResult Rejected(
        string instanceId,
        string definitionId,
        GridPoint position,
        string failure) => new(false, instanceId, definitionId, position, failure);
}

public sealed record ProductionStartResult(
    bool Applied,
    string? JobId,
    string RecipeId,
    string? Failure)
{
    public static ProductionStartResult Success(WorldProductionJob job) =>
        new(true, job.JobId, job.RecipeId, null);

    public static ProductionStartResult Rejected(string recipeId, string failure) =>
        new(false, null, recipeId, failure);
}

public static class WorldContentSimulationRules
{
    public static void Validate(
        WorldContentSimulationState state,
        DeclarativeWorldContentState definitions,
        SeededMap map,
        long worldTick)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        definitions.Validate();
        if (state.NextProductionJobSequence <= 0)
        {
            throw new InvalidDataException("The next production job sequence must be positive.");
        }

        var buildingDefinitions = definitions.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var recipeDefinitions = definitions.Recipes.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var buildingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var building in state.Buildings)
        {
            ArgumentNullException.ThrowIfNull(building);
            ContentPackageRules.ValidateLocalId(building.InstanceId);
            if (!buildingIds.Add(building.InstanceId) || !buildingDefinitions.TryGetValue(building.DefinitionId, out var definition))
            {
                throw new InvalidDataException("Placed buildings must have unique IDs and registered definitions.");
            }

            var existing = state.Buildings
                .Where(item => item.InstanceId != building.InstanceId)
                .Select(item => (Placement: item, Definition: buildingDefinitions[item.DefinitionId]));
            if (building.PlacedTick < 0 || building.PlacedTick > worldTick ||
                !Fits(map, existing, definition, building.Position))
            {
                throw new InvalidDataException($"Placed building '{building.InstanceId}' has an invalid footprint.");
            }
        }

        var jobIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var job in state.ProductionJobs)
        {
            ArgumentNullException.ThrowIfNull(job);
            ContentPackageRules.ValidateLocalId(job.JobId);
            if (!jobIds.Add(job.JobId) || !recipeDefinitions.ContainsKey(job.RecipeId) ||
                !buildingIds.Contains(job.BuildingInstanceId))
            {
                throw new InvalidDataException("Production jobs must have unique IDs and registered references.");
            }

            if (string.IsNullOrWhiteSpace(job.WorkerId) || job.StartedTick < 0 ||
                job.CompletionTick <= job.StartedTick || job.CompletionTick < worldTick &&
                job.State == WorldProductionJobState.Running ||
                job.InputReservationIds is null ||
                job.InputReservationIds.Count != job.InputReservationIds.Distinct(StringComparer.Ordinal).Count())
            {
                throw new InvalidDataException($"Production job '{job.JobId}' is malformed.");
            }
        }

        if (!state.Buildings.Select(item => item.InstanceId).SequenceEqual(
                state.Buildings.Select(item => item.InstanceId).Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            !state.ProductionJobs.Select(item => item.JobId).SequenceEqual(
                state.ProductionJobs.Select(item => item.JobId).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("World content simulation state is not in canonical order.");
        }
    }

    public static bool Fits(
        SeededMap map,
        IEnumerable<(PlacedBuilding Placement, BuildingDefinition Definition)> existingBuildings,
        BuildingDefinition definition,
        GridPoint position)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(existingBuildings);
        ArgumentNullException.ThrowIfNull(definition);
        var footprint = Footprint(definition, position).ToArray();
        if (footprint.Any(point => !map.IsPassable(point)))
        {
            return false;
        }

        var occupied = map.CampObjects
            .Select(item => item.Position)
            .Concat(map.Resources.Select(item => item.Position))
            .ToHashSet();
        foreach (var existing in existingBuildings)
        {
            foreach (var point in Footprint(existing.Definition, existing.Placement.Position))
            {
                occupied.Add(point);
            }
        }

        return footprint.All(point => !occupied.Contains(point));
    }

    public static IEnumerable<GridPoint> Footprint(BuildingDefinition definition, GridPoint position)
    {
        for (var y = 0; y < definition.Height; y++)
        {
            for (var x = 0; x < definition.Width; x++)
            {
                yield return new GridPoint(position.X + x, position.Y + y);
            }
        }
    }

    public static WorldContentSimulationState RemovePackage(
        WorldContentSimulationState state,
        string packageDigest)
    {
        ContentPackageRules.ValidateDigest(packageDigest, nameof(packageDigest));
        var removedBuildingIds = state.Buildings
            .Where(item => item.DefinitionId.StartsWith($"{packageDigest}/", StringComparison.Ordinal))
            .Select(item => item.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        return new WorldContentSimulationState(
            state.Buildings
                .Where(item => !item.DefinitionId.StartsWith($"{packageDigest}/", StringComparison.Ordinal))
                .ToArray(),
            state.ProductionJobs
                .Where(item => !removedBuildingIds.Contains(item.BuildingInstanceId) &&
                    !item.RecipeId.StartsWith($"{packageDigest}/", StringComparison.Ordinal))
                .Select(item => item with
                {
                    State = item.State == WorldProductionJobState.Running
                        ? WorldProductionJobState.Cancelled
                        : item.State,
                })
                .OrderBy(item => item.JobId, StringComparer.Ordinal)
                .ToArray(),
            state.NextProductionJobSequence);
    }
}
