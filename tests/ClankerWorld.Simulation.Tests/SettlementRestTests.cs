using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class SettlementRestTests
{
    [Fact]
    public async Task SurroundedInhabitantCanRestInPlaceWithoutTeleportingOrBeddingBenefits()
    {
        using var seed = new PrivateWorldRuntime("crowded-rest", _ => new RestProvider(false));
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var map = state.Map;
        var bed = map.GetObject("bedroll").Position;
        var position = map.Tiles.First(tile => map.IsPassable(tile.Position) && Distance(tile.Position, bed) > 2 &&
            map.Tiles.Count(other => map.IsPassable(other.Position) && Distance(other.Position, tile.Position) == 1) is > 0 and <= 3).Position;
        var blockers = map.Tiles.Where(tile => map.IsPassable(tile.Position) && Distance(tile.Position, position) == 1).Select(tile => tile.Position)
            .Concat(map.Tiles.Where(tile => map.IsPassable(tile.Position) && tile.Position != position).Select(tile => tile.Position))
            .Distinct().Take(3).ToArray();
        var sleeper = state.Inhabitants[0].InhabitantId;
        state = state with
        {
            Inhabitants = state.Inhabitants.Select((person, index) => person with
            {
                Position = index == 0 ? position : blockers[index - 1],
                EnergyBasisPoints = index == 0 ? 1_000 : 8_000,
            }).ToArray(),
        };
        using var world = PrivateWorldRuntime.Restore(state, actor => new RestProvider(actor == sleeper));
        var result = await world.AdvanceOneTickAsync();
        var rested = world.Inhabitants.Single(person => person.InhabitantId == sleeper);
        Assert.Equal(position, rested.Position);
        Assert.InRange(rested.EnergyBasisPoints, 1_001, 1_500);
        Assert.Contains(result.Events, item => item.Kind == "inhabitant_rested_outdoors" && item.Detail == sleeper);
        Assert.Equal(world.Inhabitants.Count, world.Inhabitants.Select(person => person.Position).Distinct().Count());
    }

    [Fact]
    public async Task OccupiedShelterDoesNotPreventRestAtReachableBedroll()
    {
        using var seed = new PrivateWorldRuntime("crowded-rest", _ => new RestProvider(false));
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var initial = seed.ExportState();
        var map = initial.Map;
        var bed = map.GetObject("bedroll").Position;
        var definition = seed.WorldContent.Buildings.Single(building => building.LocalId == "shelter");
        GridPoint[]? occupied = null;
        foreach (var tile in map.Tiles.Where(tile => map.IsPassable(tile.Position)))
        {
            var neighbors = map.Tiles.Where(other => map.IsPassable(other.Position) && Distance(other.Position, tile.Position) <= 1)
                .Select(other => other.Position).ToArray();
            if (neighbors.Length > 3 || Distance(bed, tile.Position) <= 2) continue;
            if (!seed.PlaceBuilding("crowded-shelter", definition.CanonicalId, tile.Position).Applied) continue;
            occupied = neighbors;
            break;
        }
        Assert.NotNull(occupied);
        var state = seed.ExportState();
        var sleeper = state.Inhabitants[0].InhabitantId;
        var otherPositions = occupied.Concat(map.Tiles.Where(tile => map.IsPassable(tile.Position) && tile.Position != bed)
            .Select(tile => tile.Position)).Distinct().Take(3).ToArray();
        state = state with
        {
            Inhabitants = state.Inhabitants.Select((person, index) => person with
            {
                Position = index == 0 ? bed : otherPositions[index - 1],
                EnergyBasisPoints = index == 0 ? 1_000 : 8_000,
            }).ToArray(),
        };
        using var world = PrivateWorldRuntime.Restore(state, actor => new RestProvider(actor == sleeper));
        await world.AdvanceOneTickAsync();
        Assert.True(world.Inhabitants.Single(person => person.InhabitantId == sleeper).EnergyBasisPoints > 1_000);
        Assert.Equal(world.Inhabitants.Count, world.Inhabitants.Select(person => person.Position).Distinct().Count());
    }

    private static int Distance(GridPoint first, GridPoint second) => Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private sealed class RestProvider(bool sleep) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                {
                    Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == (sleep ? "sleep" : "safe_idle")).ToArray(),
                },
            }, cancellationToken);
    }
}
