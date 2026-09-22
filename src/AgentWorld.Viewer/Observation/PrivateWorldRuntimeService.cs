using AgentWorld.Simulation.Playtest;

namespace AgentWorld.Viewer.Observation;

/// <summary>
/// Advances the integrated private-world alpha at a deliberately readable
/// cadence. Hosted providers are not called once per render frame.
/// </summary>
public sealed class PrivateWorldRuntimeService(
    PrivateWorldRuntime runtime,
    PrivateWorldStateFile stateFile) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var result = await runtime.AdvanceOneTickAsync(stoppingToken);
            if (result.Advanced)
            {
                stateFile.Save(runtime);
            }
        }
    }
}
