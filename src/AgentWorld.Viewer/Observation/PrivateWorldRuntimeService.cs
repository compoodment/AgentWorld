using AgentWorld.Simulation.Playtest;

namespace AgentWorld.Viewer.Observation;

/// <summary>
/// Advances the integrated private-world alpha at a deliberately readable
/// cadence. Hosted providers are not called once per render frame.
/// </summary>
public sealed class PrivateWorldRuntimeService(
    PrivateWorldRuntime runtime,
    PrivateWorldStateFile stateFile,
    OwnerClientPresenceLease clientPresence) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _ = await TryAdvanceOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Advances one private-world tick only while at least one authenticated
    /// game client has a current presence lease. Manual world pause remains a
    /// separate persistent simulation state and is never cleared here.
    /// </summary>
    public async ValueTask<bool> TryAdvanceOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!clientPresence.HasActiveClient)
        {
            return false;
        }

        var result = await runtime.AdvanceOneTickAsync(cancellationToken);
        if (result.Advanced)
        {
            stateFile.Save(runtime);
        }

        return result.Advanced;
    }
}
