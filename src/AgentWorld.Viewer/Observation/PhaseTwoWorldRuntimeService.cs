using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Kernel;

namespace AgentWorld.Viewer.Observation;

/// <summary>
/// Drives the cognition-aware fixture only while the host explicitly opts in.
/// Pause state lives in <see cref="PhaseTwoWorldRuntime"/> so the scheduler
/// never creates hidden catch-up mutations.
/// </summary>
public sealed class PhaseTwoWorldRuntimeService(
    PhaseTwoWorldRuntime runtime,
    PhaseTwoWorldStateFile stateFile) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1d / KernelClock.ScheduledTicksPerSecond));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var result = await runtime.AdvanceOneActionAsync(stoppingToken);
            if (result.Advanced)
            {
                stateFile.Save(runtime);
            }
        }
    }
}
