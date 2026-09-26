using ClankerWorld.Simulation.Harness;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class OwnerWorldStateFileTests
{
    [Fact]
    public void SavedCompositeRuntimeSurvivesRestartWithEventsAndIdempotencyIntact()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-phase-two-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var stateFile = new OwnerWorldStateFile(
                System.IO.Path.Combine(directory, "runtime.json"));
            var first = stateFile.LoadOrCreate("camp-alpha");
            Assert.True(first.TryAdvanceOneAction());
            Assert.True(first.Pause("owner-device:restart-test"));
            var instruction = first.SubmitInstruction(new OwnerInstructionRequest(
                "restart-instruction",
                "owner-device:restart-test",
                "actor-scout",
                OwnerInstructionKind.MustDo,
                "Return to camp."));
            var water = first.Capture().Snapshot.CurrentMap.Tiles
                .Single(tile => tile.Terrain == TerrainKind.Water).Position;
            var authored = first.ApplyAuthoringBatch(new OwnerAuthoringBatch(
                "restart-authoring",
                [new SetTerrainOperation(water, TerrainKind.Mountain)],
                "owner-device:restart-test"));
            Assert.True(authored.Applied, authored.Failure);
            var expected = first.Capture();
            stateFile.Save(first);

            var restarted = stateFile.LoadOrCreate("camp-alpha");
            var restored = restarted.Capture();

            Assert.Equal(expected.Snapshot.World.Identity, restored.Snapshot.World.Identity);
            Assert.Equal(expected.Snapshot.World.Actor, restored.Snapshot.World.Actor);
            Assert.Equal(expected.Snapshot.CurrentMapManifestDigest, restored.Snapshot.CurrentMapManifestDigest);
            Assert.Equal(expected.Snapshot.InitialMapManifestDigest, restored.Snapshot.InitialMapManifestDigest);
            Assert.Equal(expected.Snapshot.TopologyRevision, restored.Snapshot.TopologyRevision);
            Assert.Equal(expected.Snapshot.IsPaused, restored.Snapshot.IsPaused);
            Assert.Equal(expected.Snapshot.RunEpoch, restored.Snapshot.RunEpoch);
            Assert.Equal(expected.Snapshot.Revision, restored.Snapshot.Revision);
            Assert.Equal(expected.Snapshot.Climate, restored.Snapshot.Climate);
            Assert.Equal(expected.Snapshot.Instructions, restored.Snapshot.Instructions);
            Assert.Equal(expected.Events, restored.Events);
            Assert.Equal(instruction, restarted.SubmitInstruction(new OwnerInstructionRequest(
                "restart-instruction",
                "owner-device:restart-test",
                "actor-scout",
                OwnerInstructionKind.MustDo,
                "Return to camp.")));
            Assert.Equal(authored, restarted.ApplyAuthoringBatch(new OwnerAuthoringBatch(
                "restart-authoring",
                [new SetTerrainOperation(water, TerrainKind.Mountain)],
                "owner-device:restart-test")));
            Assert.Equal(expected.Events, restarted.Capture().Events);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SavedRuntimeRefusesAConflictingConfiguredSeed()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-phase-two-runtime-seed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var stateFile = new OwnerWorldStateFile(
                System.IO.Path.Combine(directory, "runtime.json"));
            _ = stateFile.LoadOrCreate("camp-alpha");

            var exception = Assert.Throws<InvalidDataException>(() => stateFile.LoadOrCreate("other-world"));

            Assert.Contains("different configured world seed", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
