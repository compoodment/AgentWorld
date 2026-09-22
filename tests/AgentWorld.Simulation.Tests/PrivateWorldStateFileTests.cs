using AgentWorld.Simulation.Playtest;
using AgentWorld.Viewer.Observation;

namespace AgentWorld.Simulation.Tests;

public sealed class PrivateWorldStateFileTests
{
    [Fact]
    public async Task PrivateWorldStateFileRestoresTheIntegratedRuntimeWithoutProviderSecrets()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"agentworld-private-state-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "private-world.json");
        try
        {
            var stateFile = new PrivateWorldStateFile(path);
            using (var runtime = stateFile.LoadOrCreate("playtest-alpha"))
            {
                _ = await runtime.AdvanceOneTickAsync();
                stateFile.Save(runtime);
            }

            using var restored = stateFile.LoadOrCreate("playtest-alpha");
            Assert.Equal(1, restored.WorldTick);
            Assert.Equal(4, restored.Society.Inhabitants.Count);
            Assert.Equal(
                PrivateWorldRuntimeCodec.Encode(restored.ExportState()),
                File.ReadAllBytes(path));
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
