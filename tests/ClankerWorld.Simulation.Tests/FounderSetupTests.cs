using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class FounderSetupTests
{
    [Fact]
    public async Task EmptyBaseCampPersistsFounderProgressAndOnlyStartsOnExplicitCommand()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-founders-");
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"),
                newWorldPace: WorldStartPace.FounderSetup);
            using (var created = file.LoadOrCreate("new-camp"))
            {
                Assert.Empty(created.Inhabitants);
                Assert.Empty(created.Society.Inhabitants);
                Assert.Equal(2, created.Society.Households.Count);
                Assert.False(created.FounderSetup!.Started);
                Assert.True(created.Society.IsPaused);
                Assert.Equal(2, created.ExportState().Map.CampObjects.Count(item => item.Kind == "shelter"));
                Assert.DoesNotContain(created.ExportState().Map.CampObjects, item => item.Kind == "founder");
                Assert.Throws<InvalidOperationException>(created.Resume);
                Assert.Throws<InvalidOperationException>(created.StartWorld);
                created.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(0, 0));
                created.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(1, 2));
                file.Save(created);
            }

            using var resumedSetup = file.LoadOrCreate("new-camp");
            Assert.Equal(2, resumedSetup.FounderSetup!.FounderIds.Count);
            Assert.True(resumedSetup.Society.IsPaused);
            Assert.Equal(2, resumedSetup.Society.Households.Single(item => item.Id == "household:camp-alpha").MemberIds.Count);
            resumedSetup.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(2, 2));
            resumedSetup.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(3, 2));
            Assert.Equal(2, resumedSetup.Society.Households.Single(item => item.Id == "household:camp-beta").MemberIds.Count);
            Assert.True(resumedSetup.Society.IsPaused);
            resumedSetup.StartWorld();
            Assert.False(resumedSetup.Society.IsPaused);
            Assert.True(resumedSetup.FounderSetup.Started);
            var tick = await resumedSetup.AdvanceOneTickAsync();
            Assert.True(tick.Advanced);
            file.Save(resumedSetup);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
