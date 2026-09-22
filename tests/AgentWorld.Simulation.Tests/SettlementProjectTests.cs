using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Viewer.Observation;
using AgentWorld.Simulation.Harness;

namespace AgentWorld.Simulation.Tests;

public sealed class SettlementProjectTests
{
    [Fact]
    public async Task UnregisteredMapAdditionIsRejectedEvenWithARecomputedDigest()
    {
        using var world = new PrivateWorldRuntime("invalid-settlement-map");
        world.StageStarterContent();
        for (var tick = 0; tick < 10; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var state = world.ExportState();
        var forgedMap = state.Map with
        {
            Resources = state.Map.Resources.Select(resource => resource.Id == "settlement-stone"
                ? resource with { Id = "unregistered-stone" } : resource).ToArray(),
        };
        forgedMap = forgedMap with { ManifestDigest = MapManifestCodec.Digest(forgedMap) };
        Assert.Throws<InvalidDataException>(() => PrivateWorldRuntime.Restore(state with { Map = forgedMap }));
    }

    [Fact]
    public async Task LegacyCheckpointRemainsUntouchedUntilResumedAndThenMigrates()
    {
        using var seed = new PrivateWorldRuntime("legacy-settlement");
        seed.Pause();
        var legacy = seed.ExportState() with { SchemaVersion = 4 };
        var bytes = PrivateWorldRuntimeCodec.Encode(legacy);
        using var world = PrivateWorldRuntime.Restore(legacy);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        world.Resume();
        world.StageStarterContent();
        for (var tick = 0; tick < 10; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var migrated = world.ExportState();
        Assert.Equal(5, migrated.SchemaVersion);
        Assert.Contains(migrated.Map.Resources, resource => resource.Id == "settlement-seed");
        using var restored = PrivateWorldRuntime.Restore(migrated);
        Assert.Equal(PrivateWorldRuntimeCodec.Encode(migrated), PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Theory]
    [InlineData("build:", "working", 0)]
    [InlineData("build:recipe:example", "invented", 0)]
    [InlineData("build:recipe:example", "working", 11)]
    public void InvalidProjectCheckpointFailsClosed(string candidate, string stage, int work)
    {
        using var world = new PrivateWorldRuntime("invalid-project");
        var state = world.ExportState();
        var person = state.Inhabitants[0];
        var invalid = state with
        {
            Inhabitants = state.Inhabitants.Select(item => item == person
                ? item with { Project = new SettlementProject(candidate, "Project", 0, stage, work) }
                : item).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => PrivateWorldRuntime.Restore(invalid));
    }

    [Fact]
    public async Task DefaultSettlementGathersDifferentInputsSharesAndCompletesVisibleProjects()
    {
        var provider = new ObservingProvider();
        using var world = new PrivateWorldRuntime("living-settlement", _ => provider);
        world.StageStarterContent();
        for (var tick = 0; tick < 1000; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var state = world.ExportState();
        var gathered = state.Events.Where(item => item.Kind == "material_gathered")
            .Select(item => item.Detail.Split(':')[1]).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("wood", gathered);
        Assert.Contains("stone", gathered);
        Assert.Contains("fiber", gathered);
        Assert.Contains(state.Events, item => item.Kind == "project_request_fulfilled");
        Assert.Contains(state.Events, item => item.Kind == "project_progress" && item.Detail.Contains(":completed:", StringComparison.Ordinal));
        Assert.Contains(state.Events, item => item.Kind == "food_consumed");
        Assert.True(provider.MeaningfulProjectChoiceSeen);
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        Assert.NotEmpty(snapshot.Stockpiles);
        Assert.Contains(snapshot.Inhabitants, person => person.Project is not null);
        Assert.Contains(snapshot.Inhabitants, person => person.SocialNotes.Count > 0);
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state)));
        Assert.Equal(PrivateWorldRuntimeCodec.Encode(state), PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private sealed class ObservingProvider : IDecisionProvider
    {
        public bool MeaningfulProjectChoiceSeen { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            MeaningfulProjectChoiceSeen |= request.Observation.Candidates.Count(candidate => candidate.Id.StartsWith("build:", StringComparison.Ordinal)) > 1;
            return new DeterministicDecisionProvider().DecideAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task ProjectWorkSurvivesManualPauseAndRestartAndIsVisibleToOwner()
    {
        using var world = new PrivateWorldRuntime("settlement-project");
        world.StageStarterContent();
        for (var tick = 0; tick < 100 && !world.Inhabitants.Any(person => person.Project is { WorkDone: > 1 and < 9 }); tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var worker = world.Inhabitants.First(person => person.Project is { WorkDone: > 1 and < 9 });
        var chosen = worker.Project!;
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        Assert.Equal(chosen.Label, snapshot.Inhabitants.Single(person => person.Id == worker.InhabitantId).Project!.Label);
        world.Pause();
        var bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
        Assert.Equal(chosen, restored.Inhabitants.Single(person => person.InhabitantId == worker.InhabitantId).Project);
        restored.Resume();
        for (var tick = 0; tick < 100; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "project_progress" &&
            item.Detail == $"{worker.InhabitantId}:completed:{chosen.Label}");
    }

    [Fact]
    public async Task AChosenProjectAcquiresAndDeliversMissingWoodInsteadOfBeingHidden()
    {
        using var seed = new PrivateWorldRuntime("settlement-acquisition");
        var initial = seed.ExportState();
        using var world = PrivateWorldRuntime.Restore(initial with
        {
            Society = initial.Society with
            {
                Society = initial.Society.Society with
                {
                    Inventory = initial.Society.Society.Inventory with
                    {
                        Lots = initial.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind != "wood").ToArray(),
                    },
                },
            },
        });
        world.StageStarterContent();
        for (var tick = 0; tick < 200; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Contains(world.ExportState().Events, item => item.Kind == "project_chosen");
        Assert.Contains(world.ExportState().Events, item => item.Kind == "material_gathered" && item.Detail.Contains(":wood:", StringComparison.Ordinal));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "project_material_delivered");
        Assert.NotEmpty(world.WorldSimulation.Buildings);
    }
}
