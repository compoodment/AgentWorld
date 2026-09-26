using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Viewer.Observation;

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

    [LoggerMessage(EventId = 2215, Level = LogLevel.Information,
        Message = "work_practice tick={WorldTick} inhabitant={InhabitantId} building={Building} farming={Farming} crafting={Crafting}")]
    private static partial void LogWorkPractice(ILogger logger, long worldTick, string inhabitantId, int building, int farming, int crafting);

    [LoggerMessage(EventId = 2216, Level = LogLevel.Information,
        Message = "social_standing tick={WorldTick} inhabitant={InhabitantId} subject={SubjectId} trust={Trust} reason={Reason}")]
    private static partial void LogSocialStanding(ILogger logger, long worldTick, string inhabitantId, string subjectId, int trust, string reason);

    [LoggerMessage(EventId = 2217, Level = LogLevel.Information,
        Message = "inhabitant_content_proposal tick={WorldTick} inhabitant={InhabitantId} package={PackageId} kind=building lifecycle=proposed")]
    private static partial void LogInhabitantContentProposal(ILogger logger, long worldTick, string inhabitantId, string packageId);

    [LoggerMessage(EventId = 2218, Level = LogLevel.Information,
        Message = "hosted_decision tick={WorldTick} inhabitant={InhabitantId} outcome={Outcome}")]
    private static partial void LogHostedDecision(ILogger logger, long worldTick, string inhabitantId, string outcome);

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
            runtime.CancelPendingHostedDecisions();
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
            result = await runtime.AdvanceOneTickNonBlockingAsync(() => clientPresence.HasActiveClient, tickCancellation.Token);
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
                foreach (var worldEvent in result.Events.Where(item => item.Kind.StartsWith("hosted_decision_", StringComparison.Ordinal)))
                {
                    var split = worldEvent.Detail.Split(':', 2);
                    LogHostedDecision(logger, result.WorldTick, split[0],
                        worldEvent.Kind["hosted_decision_".Length..] + (split.Length > 1 ? ":" + split[1] : ""));
                }
                var actors = runtime.Inhabitants.Select(person => person.InhabitantId).OrderByDescending(id => id.Length).ToArray();
                string? EventActor(string detail) => actors.FirstOrDefault(id => detail == id || detail.StartsWith(id + ":", StringComparison.Ordinal));
                var projects = runtime.Inhabitants.Where(person => person.Project is not null)
                    .ToDictionary(person => person.InhabitantId, person => person.Project!, StringComparer.Ordinal);
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "work_practice_earned"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is not null && runtime.Inhabitants.FirstOrDefault(person => person.InhabitantId == actor)?.Proficiency is { } practice)
                        LogWorkPractice(logger, result.WorldTick, actor, practice.Building, practice.Farming, practice.Crafting);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "social_standing_changed"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null || worldEvent.Detail.Length <= actor.Length + 1) continue;
                    var remainder = worldEvent.Detail[(actor.Length + 1)..];
                    var subject = actors.FirstOrDefault(id => remainder == id || remainder.StartsWith(id + ":", StringComparison.Ordinal));
                    var standing = runtime.Inhabitants.FirstOrDefault(person => person.InhabitantId == actor)?.SocialStanding?
                        .FirstOrDefault(item => item.SubjectId == subject);
                    if (subject is null || standing is null) continue;
                    var reason = remainder.Length > subject.Length ? remainder[(subject.Length + 1)..] : "cooperation";
                    LogSocialStanding(logger, result.WorldTick, actor, subject, standing.Trust, reason);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "inhabitant_building_proposed"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null || worldEvent.Detail.Length <= actor.Length + 1) continue;
                    LogInhabitantContentProposal(logger, result.WorldTick, actor, worldEvent.Detail[(actor.Length + 1)..]);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "project_chosen" or "project_progress" or "project_request_fulfilled"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null) continue;
                    var project = projects.GetValueOrDefault(actor);
                    LogSettlementActivity(logger, result.WorldTick, worldEvent.Kind, actor,
                        project?.Stage ?? "helping", project?.WorkDone ?? 0,
                        project?.Blocker is not null);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "survival_condition_changed"))
                {
                    var actor = EventActor(worldEvent.Detail);
                    if (actor is null) continue;
                    if (runtime.Inhabitants.FirstOrDefault(person => person.InhabitantId == actor)?.Survival is { } condition)
                    {
                        LogSurvivalCondition(logger, result.WorldTick, actor, condition.WarmthBasisPoints, condition.IllnessBasisPoints);
                    }
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "fire_fuelled" or "fire_extinguished"))
                {
                    LogSurvivalEnvironment(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind == "production_worker_unavailable"))
                {
                    var job = runtime.WorldSimulation.ProductionJobs.Concat(runtime.WorldSimulation.CropBuilds ?? [])
                        .FirstOrDefault(item => item.JobId == worldEvent.Detail);
                    if (job is not null) LogProductionCancelled(logger, result.WorldTick, job.JobId, job.WorkerId);
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
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "lesson_requested" or "lesson_accepted" or
                             "lesson_training" or "lesson_completed" or "lesson_declined" or "lesson_cancelled"))
                {
                    LogSettlementLesson(logger, result.WorldTick, worldEvent.Kind);
                }
                foreach (var worldEvent in result.Events.Where(item => item.Kind is "partnership_proposed" or "partnership_accepted" or
                             "partnership_refused" or "partnership_ended" or "partnership_expired" or
                             "parenthood_requested" or "parenthood_preparing" or "parenthood_cancelled" or "parenthood_completed" or
                             "child_born" or "child_cared_for" or "caregiver_proposed" or "caregiver_assigned" or
                             "caregiver_accepted" or "caregiver_refused" or "caregiver_proposal_expired" or "caregiver_ended"))
                {
                    LogSettlementFamily(logger, result.WorldTick, worldEvent.Kind);
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

    [LoggerMessage(EventId = 2212, Level = LogLevel.Information,
        Message = "production_cancelled tick={WorldTick} job={JobId} inhabitant={InhabitantId} reason=worker_unavailable")]
    private static partial void LogProductionCancelled(ILogger logger, long worldTick, string jobId, string inhabitantId);

    [LoggerMessage(EventId = 2209, Level = LogLevel.Information,
        Message = "settlement_lesson tick={WorldTick} event={EventKind}")]
    private static partial void LogSettlementLesson(ILogger logger, long worldTick, string eventKind);

    [LoggerMessage(EventId = 2210, Level = LogLevel.Information,
        Message = "settlement_family tick={WorldTick} event={EventKind}")]
    private static partial void LogSettlementFamily(ILogger logger, long worldTick, string eventKind);

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
