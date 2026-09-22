using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.World;

namespace AgentWorld.Simulation.Tests;

public sealed class PrivateWorldRuntimeTests
{
    [Fact]
    public void PrivateWorldStartsWithAnActiveSettlementInsteadOfAuthoringDrafts()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");

        Assert.Equal(4, runtime.Society.Inhabitants.Count);
        Assert.All(runtime.Society.Inhabitants, inhabitant =>
            Assert.Equal(AgentWorld.Simulation.Society.SocietyInhabitantStatus.Active, inhabitant.Status));
        Assert.Single(runtime.Society.Households);
        Assert.Equal(4, runtime.Inhabitants.Count);
        Assert.Contains(runtime.Inhabitants, inhabitant => inhabitant.Personality == "curious");
        Assert.Contains(runtime.Inhabitants, inhabitant => inhabitant.Aspiration == "build something lasting");
    }

    [Fact]
    public async Task PrivateWorldCarriesRicherSystemsThroughTheAuthoritativeTickAndCheckpoint()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");

        Assert.Equal("playtest-alpha", runtime.WorldSystems.WorldSeed);
        Assert.Equal(SeasonKind.Spring, runtime.WorldSystems.Climate.Season);
        Assert.Single(runtime.WorldSystems.Factions.Factions);
        Assert.Single(runtime.WorldSystems.Currency.Currencies);
        Assert.Single(runtime.WorldSystems.Culture.Cultures);
        Assert.Single(runtime.WorldSystems.Chunks);

        _ = await runtime.AdvanceOneTickAsync();

        Assert.Equal(runtime.WorldTick, runtime.WorldSystems.WorldTick);
        Assert.Equal(2, runtime.WorldSystems.Ecology.Resources.Count);
        var restoredState = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(runtime.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(restoredState);

        Assert.Equal(
            WorldSystemsCodec.Encode(runtime.WorldSystems),
            WorldSystemsCodec.Encode(restored.WorldSystems));
    }

    [Fact]
    public async Task PrivateWorldAdvancesAllFoundersThroughBoundedCognition()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");

        var result = await runtime.AdvanceOneTickAsync();

        Assert.True(result.Advanced);
        Assert.Equal(1, result.WorldTick);
        Assert.Equal(4, result.Decisions.Count);
        Assert.All(result.Decisions, decision => Assert.True(decision.Admission.Accepted));
        Assert.Contains(result.Events, worldEvent => worldEvent.Kind == "inhabitant_moved");
        Assert.All(runtime.Inhabitants, inhabitant => Assert.True(inhabitant.HungerBasisPoints < 6_500));
    }

    [Fact]
    public async Task PrivateWorldCheckpointRoundTripsWithTheSamePopulationAndTick()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        _ = await runtime.AdvanceOneTickAsync();

        var encoded = PrivateWorldRuntimeCodec.Encode(runtime.ExportState());
        var restoredState = PrivateWorldRuntimeCodec.Decode(encoded);
        using var restored = PrivateWorldRuntime.Restore(restoredState);

        Assert.Equal(runtime.WorldTick, restored.WorldTick);
        Assert.Equal(
            runtime.Society.Inhabitants.Select(item => item.Id),
            restored.Society.Inhabitants.Select(item => item.Id));
        Assert.Equal(runtime.Inhabitants, restored.Inhabitants);
        Assert.Equal(
            encoded,
            PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task PrivateWorldPauseIsAnIdempotentBoundary()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        runtime.Pause();
        runtime.Pause();

        var paused = await runtime.AdvanceOneTickAsync();

        Assert.False(paused.Advanced);
        Assert.Equal("paused", paused.Outcome);
        Assert.Single(runtime.ExportState().Events, worldEvent => worldEvent.Kind == "paused");
    }

    [Fact]
    public async Task PrivateWorldInstructionsAreIdempotentAndReachCognition()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var request = new OwnerInstructionRequest(
            "instruction-key-1",
            "owner-device:test",
            "founder-rowan",
            OwnerInstructionKind.MustDo,
            "sleep at the bedroll");

        var first = runtime.SubmitInstruction(request);
        var replay = runtime.SubmitInstruction(request);
        _ = await runtime.AdvanceOneTickAsync();

        Assert.Equal(first, replay);
        Assert.Contains(runtime.ExportState().CompletedInstructionIds!, id => id == first.InstructionId);
        Assert.Contains(
            runtime.ExportState().Events,
            worldEvent => worldEvent.Kind == "instruction_applied" && worldEvent.Detail.Contains(first.InstructionId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrivateWorldActivatesStagedContentOnTheNextTickAndCanQuarantineIt()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var packageDigest = "sha256:" + new string('d', 64);
        var version = ContentVersion.Parse("1.0.0");
        var building = new BuildingDefinition(
            packageDigest,
            "camp-kitchen",
            version,
            "Camp kitchen",
            1,
            1,
            2,
            [new ContentQuantity("wood", 2)],
            ["camp"]);
        var recipe = new RecipeDefinition(
            packageDigest,
            "berry-stew",
            version,
            "Berry stew",
            [new ContentQuantity("wood", 1)],
            [new ContentQuantity("meal", 1)],
            10,
            building.CanonicalId,
            ["food"]);
        var package = new ContentPackageManifest(
            "camp-recipes",
            version,
            packageDigest,
            [],
            [
                new ContentDefinition(
                    BuildingDefinition.SchemaKind,
                    building.LocalId,
                    building.Version,
                    building.DisplayName,
                    building.PayloadDigest,
                    """{"schema":"building/v1","width":1,"height":1,"capacity":2,"buildCosts":[{"resourceId":"wood","amount":2}],"tags":["camp"]}"""),
                new ContentDefinition(
                    RecipeDefinition.SchemaKind,
                    recipe.LocalId,
                    recipe.Version,
                    recipe.DisplayName,
                    recipe.PayloadDigest,
                    $"{{\"schema\":\"recipe/v1\",\"inputs\":[{{\"resourceId\":\"wood\",\"amount\":1}}],\"outputs\":[{{\"resourceId\":\"meal\",\"amount\":1}}],\"durationTicks\":10,\"workstationBuildingId\":\"{building.CanonicalId}\",\"tags\":[\"food\"]}}")
            ],
            []);
        var resolution = PrivateWorldRuntime.PreviewContent([package], [package.PackageId]);

        runtime.ProposeContent(package);
        runtime.ValidateContent(package.PackageId, resolution);
        runtime.ApproveContent(package.PackageId);
        var staged = runtime.StageContent(package.PackageId);

        Assert.Equal(ContentPackageLifecycle.Staged, staged.Lifecycle);
        Assert.Equal(0, staged.StagedTick);
        Assert.DoesNotContain(runtime.Content.Packages, item => item.Lifecycle == ContentPackageLifecycle.Active);

        _ = await runtime.AdvanceOneTickAsync();

        var active = Assert.Single(runtime.Content.Packages);
        Assert.Equal(ContentPackageLifecycle.Active, active.Lifecycle);
        Assert.Equal(1, active.ActivationTick);
        Assert.Equal(building.CanonicalId, Assert.Single(runtime.WorldContent.Buildings).CanonicalId);
        Assert.Equal(recipe.CanonicalId, Assert.Single(runtime.WorldContent.Recipes).CanonicalId);
        Assert.Contains(runtime.ExportState().Events, item =>
            item.Kind == "content_definitions_activated" && item.Detail.Contains("buildings=1:recipes=1", StringComparison.Ordinal));

        var restoredState = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(runtime.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(restoredState);
        var rolledBack = restored.RollbackContent(package.PackageId, "preview mismatch");

        Assert.Equal(ContentPackageLifecycle.Quarantined, rolledBack.Lifecycle);
        Assert.Empty(restored.WorldContent.Buildings);
        Assert.Empty(restored.WorldContent.Recipes);
        Assert.Contains(restored.Content.Events, item => item.Kind == "package_rolled_back");
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "content_rolled_back");
    }

    [Fact]
    public async Task PrivateWorldPlacesAContentBuildingConsumesCostsAndProducesRecipeOutputs()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var (package, building, recipe) = MaterialPackage(durationTicks: 2);
        Activate(runtime, package);
        _ = await runtime.AdvanceOneTickAsync();

        var worker = runtime.Inhabitants.Single(item => item.InhabitantId == "founder-rowan");
        var rejected = runtime.PlaceBuilding("blocked-kitchen", building.CanonicalId, new GridPoint(0, 0));
        Assert.False(rejected.Applied);
        Assert.Equal(48, runtime.Society.Inventory.Lots.Single(item => item.Id == "wood:camp-alpha").Quantity);

        var placement = runtime.PlaceBuilding("camp-kitchen-one", building.CanonicalId, worker.Position);
        Assert.True(placement.Applied, placement.Failure);
        Assert.Single(runtime.WorldSimulation.Buildings);
        Assert.Equal(46, runtime.Society.Inventory.Lots.Single(item => item.Id == "wood:camp-alpha").Quantity);

        var started = runtime.StartProduction(recipe.CanonicalId, placement.InstanceId, worker.InhabitantId);
        Assert.True(started.Applied, started.Failure);
        Assert.Equal(1, runtime.WorldSimulation.ProductionJobs.Count(item => item.State == WorldProductionJobState.Running));

        _ = await runtime.AdvanceOneTickAsync();
        Assert.DoesNotContain(runtime.Society.Inventory.Lots, item => item.ItemKind == "meal");
        _ = await runtime.AdvanceOneTickAsync();

        var completed = Assert.Single(runtime.WorldSimulation.ProductionJobs);
        Assert.Equal(WorldProductionJobState.Completed, completed.State);
        Assert.Equal(1, runtime.Society.Inventory.Lots.Single(item => item.ItemKind == "meal").Quantity);
        Assert.Contains(runtime.ExportState().Events, item => item.Kind == "recipe_completed");

        var restoredState = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(runtime.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(restoredState);
        Assert.Equal(
            runtime.WorldSimulation.Buildings,
            restored.WorldSimulation.Buildings);
        var expectedJob = Assert.Single(runtime.WorldSimulation.ProductionJobs);
        var actualJob = Assert.Single(restored.WorldSimulation.ProductionJobs);
        Assert.Equal(expectedJob.JobId, actualJob.JobId);
        Assert.Equal(expectedJob.RecipeId, actualJob.RecipeId);
        Assert.Equal(expectedJob.BuildingInstanceId, actualJob.BuildingInstanceId);
        Assert.Equal(expectedJob.WorkerId, actualJob.WorkerId);
        Assert.Equal(expectedJob.StartedTick, actualJob.StartedTick);
        Assert.Equal(expectedJob.CompletionTick, actualJob.CompletionTick);
        Assert.Equal(expectedJob.State, actualJob.State);
        Assert.Equal(expectedJob.InputReservationIds, actualJob.InputReservationIds);
        Assert.Equal(
            runtime.WorldSimulation.NextProductionJobSequence,
            restored.WorldSimulation.NextProductionJobSequence);
        Assert.Equal(runtime.Society.Inventory.Lots, restored.Society.Inventory.Lots);
    }

    [Fact]
    public void PrivateWorldCodecReadsLegacyCheckpointWithoutContentRegistry()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var legacyState = runtime.ExportState() with { SchemaVersion = 1, Content = null };

        var decoded = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(legacyState));
        using var restored = PrivateWorldRuntime.Restore(decoded);

        Assert.Equal(1, decoded.SchemaVersion);
        Assert.Null(decoded.Content);
        Assert.Empty(restored.Content.Packages);
        Assert.Throws<InvalidDataException>(() => PrivateWorldRuntimeCodec.Encode(
            runtime.ExportState() with { Content = null }));
    }

    private static ContentPackageManifest Package(string id, string version, char digestCharacter) => new(
        id,
        ContentVersion.Parse(version),
        "sha256:" + new string(digestCharacter, 64),
        [],
        [new ContentDefinition(
            "recipe",
            $"{id}-starter",
            ContentVersion.Parse("1.0.0"),
            "Starter recipe",
            "sha256:" + new string(digestCharacter, 64))],
        []);

    private static (ContentPackageManifest Package, BuildingDefinition Building, RecipeDefinition Recipe) MaterialPackage(
        int durationTicks)
    {
        var packageDigest = "sha256:" + new string('e', 64);
        var version = ContentVersion.Parse("1.0.0");
        var building = new BuildingDefinition(
            packageDigest,
            "camp-kitchen",
            version,
            "Camp kitchen",
            1,
            1,
            2,
            [new ContentQuantity("wood", 2)],
            ["camp"]);
        var recipe = new RecipeDefinition(
            packageDigest,
            "berry-meal",
            version,
            "Berry meal",
            [new ContentQuantity("wood", 1)],
            [new ContentQuantity("meal", 1)],
            durationTicks,
            building.CanonicalId,
            ["food"]);
        return (new ContentPackageManifest(
            "material-production",
            version,
            packageDigest,
            [],
            [
                new ContentDefinition(
                    BuildingDefinition.SchemaKind,
                    building.LocalId,
                    building.Version,
                    building.DisplayName,
                    building.PayloadDigest,
                    """{"schema":"building/v1","width":1,"height":1,"capacity":2,"buildCosts":[{"resourceId":"wood","amount":2}],"tags":["camp"]}"""),
                new ContentDefinition(
                    RecipeDefinition.SchemaKind,
                    recipe.LocalId,
                    recipe.Version,
                    recipe.DisplayName,
                    recipe.PayloadDigest,
                    $"{{\"schema\":\"recipe/v1\",\"inputs\":[{{\"resourceId\":\"wood\",\"amount\":1}}],\"outputs\":[{{\"resourceId\":\"meal\",\"amount\":1}}],\"durationTicks\":{durationTicks},\"workstationBuildingId\":\"{building.CanonicalId}\",\"tags\":[\"food\"]}}")
            ],
            []), building, recipe);
    }

    private static void Activate(PrivateWorldRuntime runtime, ContentPackageManifest package)
    {
        var resolution = PrivateWorldRuntime.PreviewContent([package], [package.PackageId]);
        runtime.ProposeContent(package);
        runtime.ValidateContent(package.PackageId, resolution);
        runtime.ApproveContent(package.PackageId);
        runtime.StageContent(package.PackageId);
    }
}
