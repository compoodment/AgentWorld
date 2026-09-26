using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Viewer.Control;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class SocietyLifePaceTests
{
    [Fact]
    public void LifePaceTelemetryContainsOnlyBoundedTickAndRate()
    {
        var logger = new RecordingLogger<PrivateWorldRuntimeService>();
        OwnerLifePaceTelemetry.Changed(logger, 123, 1_460);
        Assert.Equal("world_life_pace tick=123 rate=1460", Assert.Single(logger.Messages));
    }

    [Fact]
    public void UnknownFutureLifecycleContractIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SocietyConfig(ContractVersion: 3).Validate());
    }

    [Fact]
    public void PaceChangesPreserveCurrentAgeBirthDatesAndTheWorldCalendar()
    {
        var original = SocietyFixture.AdvanceTo(SocietyFixture.CreateGenesis("life-clock"), 1_234).Checkpoint;
        var person = original.Inhabitants[0];
        Assert.Throws<InvalidOperationException>(() => SocietyFixture.SetLifePace(original, 1_460));
        var paused = SocietyFixture.Pause(original).Checkpoint;
        var fast = SocietyFixture.SetLifePace(paused, 1_460).Checkpoint;
        Assert.Equal(original.AgeAt(person, original.WorldTick), fast.AgeAt(person, fast.WorldTick));
        Assert.Equal(person.BirthTick, fast.Inhabitants[0].BirthTick);
        Assert.Equal(365, fast.Config.DaysPerWorldYear);
        Assert.Equal(paused.WorldTick, fast.WorldTick);
        var advanced = SocietyFixture.AdvanceTo(SocietyFixture.Resume(fast).Checkpoint, fast.WorldTick + 360).Checkpoint;
        Assert.Equal(original.AgeAt(person, original.WorldTick) + 1, advanced.AgeAt(advanced.Inhabitants[0], advanced.WorldTick));
        var normal = SocietyFixture.SetLifePace(SocietyFixture.Pause(advanced).Checkpoint, 1).Checkpoint;
        Assert.Equal(advanced.AgeAt(advanced.Inhabitants[0], advanced.WorldTick), normal.AgeAt(normal.Inhabitants[0], normal.WorldTick));
        Assert.Equal(advanced.LifeTickAt(advanced.WorldTick), normal.LifeTickAt(normal.WorldTick));
        Assert.Equal(normal, SocietyFixture.SetLifePace(normal, 1).Checkpoint);
    }

    [Fact]
    public void NewbornStartsAtZeroUnderAcceleratedTimeAndAgesAcrossRestart()
    {
        var founders = new[] { SocietyFixture.CreateFounder("alice", "Alice"), SocietyFixture.CreateFounder("bob", "Bob") };
        var society = SocietyFixture.CreateGenesis("life-family", founders, [new InventoryLot("food", "food", "alice", 10, 10_000, 10_000, 0)]);
        society = SocietyFixture.CreateHousehold(society, "home", "Home", ["alice", "bob"]).Checkpoint;
        society = SocietyFixture.ProposeRelationship(society, new("partners", 1, SocietyRelationshipType.Partnership, "alice", "bob", 0)).Checkpoint;
        society = SocietyFixture.AcceptRelationship(society, "partners", 1, "bob").Checkpoint;
        society = SocietyFixture.SetLifePace(SocietyFixture.Pause(society).Checkpoint, 1_460).Checkpoint;
        society = SocietyFixture.AdvanceTo(SocietyFixture.Resume(society).Checkpoint, 1_000).Checkpoint;
        var birth = SocietyFixture.CommitBirth(society, new("baby", 1, "alice", "bob", "home", ["alice", "bob"],
            ["alice", "bob"], "food", 1, society.WorldTick));
        society = birth.Checkpoint;
        var child = society.GetInhabitant(birth.CreatedId!);
        Assert.Equal(1_000, child.BirthTick);
        Assert.Equal(1_460_000, child.BirthLifeTick);
        Assert.Equal(0, society.AgeAt(child, society.WorldTick));
        var restored = SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(society));
        var before = SocietyFixture.AdvanceTo(restored, 1_719).Checkpoint;
        Assert.Equal(SocietyAgeBand.Infant, before.GetInhabitant(child.Id).AgeBand);
        var after = SocietyFixture.AdvanceTo(before, 1_720).Checkpoint;
        Assert.Equal(SocietyAgeBand.Child, after.GetInhabitant(child.Id).AgeBand);
        var adult = SocietyFixture.AdvanceTo(after, 7_480).Checkpoint;
        Assert.Equal(SocietyAgeBand.Adult, adult.GetInhabitant(child.Id).AgeBand);
        Assert.Equal(18, adult.AgeAt(adult.GetInhabitant(child.Id), adult.WorldTick));
    }

    [Fact]
    public void OldPausedPrivateSaveIsUnchangedUntilOwnerExplicitlySelectsPace()
    {
        using var seed = new PrivateWorldRuntime("life-migration");
        seed.Pause();
        var old = seed.ExportState() with { SchemaVersion = 9 };
        var bytes = PrivateWorldRuntimeCodec.Encode(old);
        using var world = PrivateWorldRuntime.Restore(old);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False(world.SetLifePace(1));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True(world.SetLifePace(365));
        Assert.Equal(PrivateWorldRuntime.StateSchemaVersion, world.ExportState().SchemaVersion);
        Assert.Equal(365, world.Society.LifeClock!.Rate);
        Assert.True(world.Society.IsPaused);
        using var reloaded = PrivateWorldRuntime.Restore(world.ExportState());
        Assert.Equal(PrivateWorldRuntimeCodec.Encode(world.ExportState()), PrivateWorldRuntimeCodec.Encode(reloaded.ExportState()));
        Assert.Throws<InvalidDataException>(() => PrivateWorldRuntime.Restore(world.ExportState() with { SchemaVersion = 9 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetLifePace(0));
    }

    [Fact]
    public void AcceleratedMortalityUsesTheSameBiologicalAgeRollsAsCalendarTime()
    {
        var founders = Enumerable.Range(0, 20).Select(index => SocietyFixture.CreateFounder($"person-{index:D2}", "Person")).ToArray();
        var seed = SocietyFixture.CreateGenesis("life-mortality", founders);
        var normal = SocietyFixture.AdvanceTo(seed, 72 * seed.Config.TicksPerWorldYear).Checkpoint;
        var fast = SocietyFixture.SetLifePace(SocietyFixture.Pause(seed).Checkpoint, 1_460).Checkpoint;
        fast = SocietyFixture.AdvanceTo(SocietyFixture.Resume(fast).Checkpoint, 72 * 360).Checkpoint;
        Assert.Contains(normal.Inhabitants, person => person.Status == SocietyInhabitantStatus.Dead);
        Assert.Equal(normal.Inhabitants.Select(person => (person.Id, person.Status, person.AgeBand, person.LastLifecycleYearChecked, person.DeathCause)),
            fast.Inhabitants.Select(person => (person.Id, person.Status, person.AgeBand, person.LastLifecycleYearChecked, person.DeathCause)));
    }

    [Fact]
    public async Task MinorsCannotStartAdultWorkEvenIfAnOldRoleRecordSaysBuilder()
    {
        using var seed = new PrivateWorldRuntime("minor-work");
        var state = seed.ExportState();
        var childId = state.Inhabitants[0].InhabitantId;
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inhabitants = state.Society.Society.Inhabitants.Select(person => person.Id == childId ? person with
                    {
                        BirthTick = -3 * state.Society.Society.Config.TicksPerWorldYear,
                        AgeBand = SocietyAgeBand.Child,
                        LastLifecycleYearChecked = 3,
                        CurrentRole = SocietyWorkRole.Builder,
                    } : person).ToArray(),
                }
            }
        };
        using var world = PrivateWorldRuntime.Restore(state);
        world.StageStarterContent();
        for (var tick = 0; tick < 50; tick++) await world.AdvanceOneTickAsync();
        Assert.Null(world.Inhabitants.Single(person => person.InhabitantId == childId).Project);
        Assert.Null(world.Inhabitants.Single(person => person.InhabitantId == childId).Lesson);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void UnsupportedClockRatesFailClosed(int rate)
    {
        var state = SocietyFixture.CreateGenesis("invalid-life-clock");
        state = state with { Config = state.Config with { ContractVersion = 2 }, LifeClock = new(rate, 0, 0) };
        Assert.Throws<InvalidDataException>(() => SocietyFixture.Validate(state));
    }
}
