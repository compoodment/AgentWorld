using AgentWorld.Simulation.Playtest;

namespace AgentWorld.Viewer.Observation;

/// <summary>
/// Advances the integrated private-world alpha at a deliberately readable
/// cadence. Hosted providers are not called once per render frame.
/// </summary>
public sealed partial class PrivateWorldRuntimeService(
    PrivateWorldRuntime runtime,
    PrivateWorldStateFile stateFile,
    OwnerClientPresenceLease clientPresence,
    ILogger<PrivateWorldRuntimeService>? logger = null) : BackgroundService
{
    private string? lastGateState;

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
            LogGateTransition("waiting_for_client", runtime.WorldTick);
            return false;
        }

        _ = runtime.StageStarterContent();
        using var monitorLifetime = new CancellationTokenSource();
        using var tickCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitor = MonitorTickGateAsync(tickCancellation, monitorLifetime.Token);
        PrivateWorldStepResult result;
        try
        {
            result = await runtime.AdvanceOneTickAsync(() => clientPresence.HasActiveClient, tickCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && tickCancellation.IsCancellationRequested)
        {
            LogGateTransition(runtime.Society.IsPaused ? "paused" : "waiting_for_client", runtime.WorldTick);
            return false;
        }
        finally
        {
            await monitorLifetime.CancelAsync();
            await monitor;
        }
        LogGateTransition(result.Advanced ? "advancing" : result.Outcome, result.WorldTick);
        if (result.Advanced)
        {
            if (stateFile.Save(runtime) && logger is not null)
            {
                var checkpoint = runtime.ExportState();
                LogHistoryCompacted(logger, result.WorldTick, checkpoint.EventHistoryFloor, checkpoint.Events.Count);
            }
            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                var projects = runtime.Inhabitants.Where(person => person.Project is not null)
                    .ToDictionary(person => person.InhabitantId, person => person.Project!, StringComparer.Ordinal);
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "project_chosen" or "project_progress" or "project_request_fulfilled"))
                {
                    var actor = worldEvent.Detail.Split(':', 2)[0];
                    var project = projects.GetValueOrDefault(actor);
                    LogSettlementActivity(logger, result.WorldTick, worldEvent.Kind, actor,
                        project?.Stage ?? "helping", project?.WorkDone ?? 0,
                        project?.Blocker is not null);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "survival_condition_changed"))
                {
                    var actor = worldEvent.Detail.Split(':', 2)[0];
                    if (runtime.Inhabitants.FirstOrDefault(person => person.InhabitantId == actor)?.Survival is { } condition)
                    {
                        LogSurvivalCondition(logger, result.WorldTick, actor, condition.WarmthBasisPoints, condition.IllnessBasisPoints);
                    }
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "fire_fuelled" or "fire_extinguished"))
                {
                    LogSurvivalEnvironment(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "settlement_trade_offered" or
                             "settlement_trade_declined" or "settlement_trade_completed" or "settlement_trade_cancelled"))
                {
                    LogSettlementTrade(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "council_steward_changed" or
                             "council_policy_proposed" or "council_vote_recorded" or "council_policy_adopted" or "council_policy_rejected"))
                {
                    LogSettlementCouncil(logger, result.WorldTick, worldEvent.Kind);
                }
            }
        }

        foreach (var decision in result.Decisions.OrderBy(item => item.InhabitantId, StringComparer.Ordinal))
        {
            var intention = decision.Admission.Intention;
            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                var provider = intention?.Provider.ToString().ToLowerInvariant() ?? "none";
                LogCognitionDecision(
                    logger,
                    result.WorldTick,
                    decision.InhabitantId,
                    decision.Admission.Accepted,
                    decision.Admission.FellBack,
                    decision.Admission.Outcome,
                    provider,
                    intention?.CandidateId ?? "none",
                    intention?.Confidence ?? 0,
                    intention?.Usage?.ModelId ?? "none",
                    intention?.Usage?.InputTokens ?? 0,
                    intention?.Usage?.OutputTokens ?? 0);
            }
        }

        return result.Advanced;
    }

    private async Task MonitorTickGateAsync(CancellationTokenSource tickCancellation, CancellationToken monitorToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(monitorToken))
            {
                if (!clientPresence.HasActiveClient || runtime.Society.IsPaused)
                {
                    await tickCancellation.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (monitorToken.IsCancellationRequested)
        {
            // The proposed tick completed; no lifecycle watcher survives it.
        }
    }

    private void LogGateTransition(string state, long worldTick)
    {
        if (string.Equals(lastGateState, state, StringComparison.Ordinal))
        {
            return;
        }

        lastGateState = state;
        if (logger is not null)
        {
            LogWorldTickGate(logger, state, worldTick, clientPresence.ActiveClientCount);
        }
    }

    [LoggerMessage(EventId = 2208, Level = LogLevel.Information,
        Message = "settlement_council tick={WorldTick} event={EventKind}")]
    private static partial void LogSettlementCouncil(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2207, Level = LogLevel.Information,
        Message = "settlement_trade tick={WorldTick} event={EventKind}")]
    private static partial void LogSettlementTrade(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Information,
        Message = "world_history_compacted tick={WorldTick} event_floor={EventFloor} recent_events={RecentEvents}")]
    private static partial void LogHistoryCompacted(ILogger logger, long worldTick, long eventFloor, int recentEvents);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information,
        Message = "settlement_activity tick={WorldTick} event={EventKind} inhabitant={InhabitantId} stage={Stage} work={WorkDone} blocked={Blocked}")]
    private static partial void LogSettlementActivity(ILogger logger, long worldTick, string eventKind,
        string inhabitantId, string stage, int workDone, bool blocked);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Information,
        Message = "survival_condition tick={WorldTick} inhabitant={InhabitantId} warmth={Warmth} illness={Illness}")]
    private static partial void LogSurvivalCondition(ILogger logger, long worldTick, string inhabitantId, int warmth, int illness);

    [LoggerMessage(EventId = 2206, Level = LogLevel.Information,
        Message = "survival_environment tick={WorldTick} event={EventKind}")]
    private static partial void LogSurvivalEnvironment(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "cognition_decision tick={WorldTick} inhabitant={InhabitantId} accepted={Accepted} fallback={FellBack} outcome={Outcome} provider={Provider} candidate={CandidateId} confidence={Confidence} model={Model} input_tokens={InputTokens} output_tokens={OutputTokens}")]
    private static partial void LogCognitionDecision(
        ILogger logger,
        long worldTick,
        string inhabitantId,
        bool accepted,
        bool fellBack,
        string outcome,
        string provider,
        string candidateId,
        double confidence,
        string model,
        int inputTokens,
        int outputTokens);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Information,
        Message = "world_tick_gate state={State} tick={WorldTick} active_clients={ActiveClientCount}")]
    private static partial void LogWorldTickGate(
        ILogger logger,
        string state,
        long worldTick,
        int activeClientCount);
}
