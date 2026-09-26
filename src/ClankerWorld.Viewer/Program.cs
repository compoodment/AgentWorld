using System.Net;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Control;
using ClankerWorld.Viewer.Observation;

var builder = WebApplication.CreateBuilder(args);
var runtimeSeed = builder.Configuration["ClankerWorld:Runtime:Seed"] ?? SeededWorldObservationStore.SampleSeed;
var configuredWorldMode = builder.Configuration["ClankerWorld:Runtime:WorldMode"] ?? "private";
if (!string.Equals(configuredWorldMode, "private", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(configuredWorldMode, "fixture", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Unsupported ClankerWorld:Runtime:WorldMode '{configuredWorldMode}'. Expected private or fixture.");
}

var isPrivateWorld = string.Equals(configuredWorldMode, "private", StringComparison.OrdinalIgnoreCase);
var advanceFixture = builder.Configuration.GetValue<bool>("ClankerWorld:Runtime:AdvanceScript");
var advanceRuntime = isPrivateWorld
    ? builder.Configuration.GetValue("ClankerWorld:Runtime:AdvanceScript", true)
    : advanceFixture;
var clientPresenceTimeoutSeconds = builder.Configuration.GetValue(
    "ClankerWorld:Runtime:ClientPresenceTimeoutSeconds",
    5);
if (clientPresenceTimeoutSeconds is < 2 or > 60)
{
    throw new InvalidOperationException(
        "ClankerWorld:Runtime:ClientPresenceTimeoutSeconds must be between 2 and 60 seconds.");
}
var configuredDecisionProvider = builder.Configuration["ClankerWorld:Runtime:DecisionProvider"] ?? "deterministic";
var configuredJevModel = builder.Configuration["ClankerWorld:Runtime:JevModel"] ?? "jev-1.13.0";
var configuredModel = builder.Configuration["ClankerWorld:Runtime:Model"];
var configuredOpenAiModel = builder.Configuration["ClankerWorld:Runtime:OpenAiModel"] ??
    (configuredDecisionProvider.StartsWith("openai", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var configuredOllamaCloudModel = builder.Configuration["ClankerWorld:Runtime:OllamaCloudModel"] ??
    (configuredDecisionProvider.StartsWith("ollama", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var publicPort = builder.Configuration.GetValue("ClankerWorld:Http:Port", 5188);
var localApprovalPort = builder.Configuration.GetValue<int?>("ClankerWorld:Pairing:LocalApprovalPort") ?? 0;
if (publicPort is <= 0 or > 65535 || localApprovalPort is < 0 or > 65535 || localApprovalPort == publicPort)
{
    throw new InvalidOperationException("ClankerWorld HTTP ports must be distinct values between 1 and 65535.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, publicPort);
    if (localApprovalPort > 0)
    {
        options.Listen(IPAddress.Loopback, localApprovalPort);
    }
});

var authorityStatePath = builder.Configuration["ClankerWorld:Pairing:StatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "owner-authority.json");
var configuredRuntimeStatePath = builder.Configuration["ClankerWorld:Runtime:StatePath"];
var runtimeStatePath = configuredRuntimeStatePath ??
    Path.Combine(
        builder.Environment.ContentRootPath,
        "saves",
        isPrivateWorld ? "private-world.json" : "fixture-runtime.json");
var legacyRuntimeStatePath = builder.Configuration["ClankerWorld:Runtime:LegacyStatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "fixture-runtime.json");
var privateRuntimeStatePath = isPrivateWorld
    ? runtimeStatePath
    : builder.Configuration["ClankerWorld:Runtime:PrivateStatePath"] ??
        Path.Combine(builder.Environment.ContentRootPath, "saves", "private-world.json");
var providerConfigurationPath = builder.Configuration["ClankerWorld:Runtime:ProviderStatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "provider-configuration.json");
var approvedAssetCatalogPath = builder.Configuration["ClankerWorld:Assets:CatalogPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "approved-assets.json");
var configuredAuthorityId = builder.Configuration["ClankerWorld:Pairing:ServerAuthorityId"] ??
    $"clankerworld-host:{Environment.MachineName}";

// The catalog is loaded only from a host-owned path at startup. Its absence
// intentionally yields an empty, deny-all allow-list; malformed existing
// catalog files stop startup rather than becoming a partial approval set.
var approvedAssetCatalog = ApprovedAssetCatalog.LoadOrDeny(approvedAssetCatalogPath);
builder.Services.AddSingleton<IOwnerApprovedAssetReferencePolicy>(approvedAssetCatalog);
builder.Services.AddSingleton(approvedAssetCatalog);
builder.Services.AddHttpClient("typesafe");
builder.Services.AddHttpClient("model");
builder.Services.AddSingleton<WorldJevPolicy>();
builder.Services.AddSingleton(new ProviderConfigurationStore(
    providerConfigurationPath,
    new ProviderConfigurationSeed(
        configuredDecisionProvider,
        configuredJevModel,
        Environment.GetEnvironmentVariable("TYPESAFE_API_KEY"),
        configuredOpenAiModel,
        Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        configuredOllamaCloudModel,
        Environment.GetEnvironmentVariable("OLLAMA_API_KEY"))));
builder.Services.AddSingleton<ConfigurableDecisionProvider>();
builder.Services.AddSingleton<IDecisionProvider>(services =>
    services.GetRequiredService<ConfigurableDecisionProvider>());
builder.Services.AddSingleton<OwnerWorldStateFile>(services => new OwnerWorldStateFile(
    isPrivateWorld ? legacyRuntimeStatePath : runtimeStatePath,
    approvedAssetCatalog,
    services.GetRequiredService<IDecisionProvider>()));
builder.Services.AddSingleton<OwnerWorldRuntime>(services => services
    .GetRequiredService<OwnerWorldStateFile>()
    .LoadOrCreate(runtimeSeed));
builder.Services.AddSingleton<PrivateWorldStateFile>(services => new PrivateWorldStateFile(
    privateRuntimeStatePath,
    _ => services.GetRequiredService<IDecisionProvider>(),
    WorldStartPace.FounderSetup));
builder.Services.AddSingleton<PrivateWorldRuntime>(services =>
{
    var runtime = services.GetRequiredService<PrivateWorldStateFile>().LoadOrCreate(runtimeSeed);
    services.GetRequiredService<WorldJevPolicy>().Initialize(runtime.JevEnabled, runtime.JevPolicyRevision);
    return runtime;
});
builder.Services.AddSingleton<OwnerWorldObservationStore>(services => isPrivateWorld
    ? new OwnerWorldObservationStore(services.GetRequiredService<PrivateWorldRuntime>())
    : new OwnerWorldObservationStore(services.GetRequiredService<OwnerWorldRuntime>()));
builder.Services.AddSingleton(new OwnerAuthorityStateFile(authorityStatePath));
var pairingHostOptions = new OwnerPairingHostOptions(localApprovalPort);
builder.Services.AddSingleton(pairingHostOptions);
builder.Services.AddSingleton<OwnerAuthorityStore>(services =>
{
    var worldId = isPrivateWorld
        ? services.GetRequiredService<PrivateWorldRuntime>().Society.WorldId
        : services.GetRequiredService<OwnerWorldRuntime>().Capture().Snapshot.World.Identity.WorldId;
    return services.GetRequiredService<OwnerAuthorityStateFile>().LoadOrCreate(
        new OwnerAuthorityIdentity(configuredAuthorityId, worldId));
});
builder.Services.AddSingleton<OwnerRequestAuthorizer>();
builder.Services.AddSingleton(new OwnerClientPresenceLease(
    TimeSpan.FromSeconds(clientPresenceTimeoutSeconds)));
if (advanceRuntime)
{
    if (isPrivateWorld)
    {
        builder.Services.AddHostedService<PrivateWorldRuntimeService>();
    }
    else
    {
        builder.Services.AddHostedService<OwnerWorldRuntimeService>();
    }
}

var app = builder.Build();
var founderSetupGate = new object();
app.UseDefaultFiles();
app.UseStaticFiles();

// This is deliberately discovery-only. Full owner capabilities are returned
// only inside the authenticated reconnect baseline below.
app.MapGet("/api/v1/handshake", () => Results.Ok(new ViewerHandshake(
    new ProtocolVersion(Major: 1, Minor: 1),
    ["owner-device-pairing.v1"],
    ["owner-device-pairing.v1"])));

app.MapPost("/api/v1/pairings", (
    StartOwnerPairingHttpRequest request,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    var result = authority.StartPairing(new OwnerPairingRequest(request?.PublicKeySpkiBase64 ?? string.Empty));
    // Start/expiry trimming changes operational authority state even when a
    // new pairing is refused for capacity. Persist that bounded state before
    // returning either outcome.
    stateFile.Save(authority);
    if (!result.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(result.Failure);
    }

    return Results.Ok(result.Value);
});

app.MapGet("/api/v1/pairings/{pairingId}", (
    string pairingId,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    var result = authority.GetPairingStatus(pairingId);
    // Polling can cause a pending record to become expired, so retain that
    // state before replying.
    stateFile.Save(authority);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : OwnerFailures.ToHttpResult(result.Failure);
});

app.MapPost("/api/v1/pairings/activate", (
    ActivateOwnerPairingHttpRequest request,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    var result = authority.ActivatePairing(new OwnerPairingActivationRequest(
        request?.PairingId ?? string.Empty,
        request?.CanonicalProof ?? string.Empty,
        request?.SignatureBase64 ?? string.Empty));
    // Expiry is applied while evaluating activation, including rejected
    // activation requests, so persist before returning the result.
    stateFile.Save(authority);
    if (!result.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(result.Failure);
    }

    return Results.Ok(result.Value);
});

// This route is served only by the second loopback-only listener configured by
// the deployment. Tailscale Serve forwards the normal viewer port, never this
// approval port, so a remote client cannot bootstrap itself.
app.MapPost("/api/v1/local/pairings/{pairingId}/approve", (
    string pairingId,
    LocalPairingApprovalHttpRequest request,
    HttpContext context,
    OwnerPairingHostOptions options,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    if (!options.IsLocalApprovalRequest(context))
    {
        return Results.NotFound();
    }

    var result = authority.ApprovePendingPairingLocally(pairingId, request?.PairingCode ?? string.Empty);
    // A mismatch increments the durable bounded-attempt counter, so a restart
    // must not erase unsuccessful approval attempts.
    stateFile.Save(authority);
    if (!result.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(result.Failure);
    }

    return Results.Ok(result.Value);
});

// Recovery deliberately shares the separate host-local listener with first
// pairing approval. If every paired Windows device is lost or revoked, the
// host can still revoke a stale public key before pairing a replacement; this
// path is not forwarded by Tailscale Serve.
app.MapPost("/api/v1/local/devices/{deviceId}/revoke", (
    string deviceId,
    LocalDeviceRevokeHttpRequest _,
    HttpContext context,
    OwnerPairingHostOptions options,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    if (!options.IsLocalApprovalRequest(context))
    {
        return Results.NotFound();
    }

    var result = authority.RevokeDeviceLocally(deviceId);
    stateFile.Save(authority);
    if (!result.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(result.Failure);
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/v1/owner/challenges", (
    IssueOwnerChallengeHttpRequest request,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    var result = authority.IssueChallenge(new OwnerChallengeIssueRequest(
        request?.DeviceId ?? string.Empty,
        request?.RequestId ?? string.Empty,
        request?.CanonicalProof ?? string.Empty,
        request?.SignatureBase64 ?? string.Empty));
    // Challenge expiry/retention trimming also happens on rejected requests.
    stateFile.Save(authority);
    if (!result.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(result.Failure);
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/v1/owner/reconnect", (
    OwnerSignedHttpRequest<OwnerReconnectAction> request,
    OwnerRequestAuthorizer authorizer,
    OwnerWorldObservationStore observations,
    OwnerClientPresenceLease clientPresence) =>
{
    if (request?.Action is null || request.Action.AfterEventId < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["afterEventId"] = ["The event cursor cannot be negative."],
        });
    }

    var authorization = authorizer.Authorize(
        request,
        "POST",
        "/api/v1/owner/reconnect",
        OwnerHttpBinding.ReconnectPayload(request.Action));
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    clientPresence.RecordAuthenticatedReconnect(authorization.Value!.DeviceId);

    return Results.Ok(new ViewerOwnerReconnect(
        observations.GetOwnerHandshake(),
        observations.GetReconnectBaseline(request.Action.AfterEventId)));
});

app.MapPost("/api/v1/owner/control/life-pace", (
    OwnerSignedHttpRequest<OwnerLifePaceAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services,
    OwnerWorldObservationStore observations,
    ILogger<PrivateWorldRuntimeService> logger) =>
{
    if (request.Action is null || request.Action.Rate is not (1 or 365 or 1_460))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action.rate"] = ["Life pace must be 1, 365 or 1460."] });
    }
    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/control/life-pace", OwnerHttpBinding.LifePacePayload(request.Action));
    if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
    if (!isPrivateWorld) return Results.Conflict(new { error = "Life pacing requires a private world." });
    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    bool changed;
    try
    {
        changed = runtime.SetLifePace(request.Action.Rate);
    }
    catch (InvalidOperationException)
    {
        return Results.Conflict(new { error = "Pause the world before changing life pace." });
    }
    // A retry must also persist a prior in-memory change whose first save failed.
    services.GetRequiredService<PrivateWorldStateFile>().Save(runtime);
    if (changed) OwnerLifePaceTelemetry.Changed(logger, runtime.WorldTick, request.Action.Rate);
    return Results.Ok(OwnerControlReceipt.From("life_pace", changed, observations.GetSnapshot()));
});

app.MapPost("/api/v1/owner/control/jev-assistance", (
    OwnerSignedHttpRequest<OwnerJevAssistanceAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services,
    OwnerWorldObservationStore observations,
    WorldJevPolicy jevPolicy,
    ILogger<PrivateWorldRuntimeService> logger) =>
{
    if (request?.Action is null)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A Jev setting is required."] });
    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/control/jev-assistance",
        OwnerHttpBinding.JevAssistancePayload(request.Action));
    if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
    if (!isPrivateWorld) return Results.Conflict(new { error = "Jev assistance requires a private world." });
    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    bool changed;
    try
    {
        changed = runtime.SetJevEnabled(request.Action.Enabled);
    }
    catch (InvalidOperationException)
    {
        return Results.Conflict(new { error = "Pause the world before changing Jev assistance." });
    }
    jevPolicy.Set(runtime.JevEnabled, runtime.JevPolicyRevision);
    services.GetRequiredService<PrivateWorldStateFile>().Save(runtime);
    if (changed) OwnerJevAssistanceTelemetry.Changed(logger, runtime.WorldTick, runtime.JevEnabled);
    return Results.Ok(OwnerControlReceipt.From("jev_assistance", changed, observations.GetSnapshot()));
});

app.MapPost("/api/v1/owner/control/pause", (
    OwnerSignedHttpRequest<OwnerControlAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services,
    OwnerWorldObservationStore observations) =>
{
    if (!IsControl(request, "pause"))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.operation"] = ["This endpoint only accepts the pause operation."],
        });
    }

    var authorization = authorizer.Authorize(
        request,
        "POST",
        "/api/v1/owner/control/pause",
        OwnerHttpBinding.EmptyPayload("pause"));
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    if (isPrivateWorld)
    {
        var privateRuntime = services.GetRequiredService<PrivateWorldRuntime>();
        var privateStateFile = services.GetRequiredService<PrivateWorldStateFile>();
        var wasPaused = privateRuntime.Society.IsPaused;
        privateRuntime.Pause();
        var changed = !wasPaused && privateRuntime.Society.IsPaused;
        if (changed)
        {
            privateStateFile.Save(privateRuntime);
        }

        return Results.Ok(OwnerControlReceipt.From("pause", changed, observations.GetSnapshot()));
    }

    var runtime = services.GetRequiredService<OwnerWorldRuntime>();
    var stateFile = services.GetRequiredService<OwnerWorldStateFile>();
    var legacyChanged = runtime.Pause($"owner-device:{authorization.Value!.DeviceId}");
    if (legacyChanged)
    {
        stateFile.Save(runtime);
    }

    return Results.Ok(OwnerControlReceipt.From("pause", legacyChanged, runtime.Capture().Snapshot));
});

app.MapPost("/api/v1/owner/control/resume", (
    OwnerSignedHttpRequest<OwnerControlAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services,
    OwnerWorldObservationStore observations) =>
{
    if (!IsControl(request, "resume"))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.operation"] = ["This endpoint only accepts the resume operation."],
        });
    }

    var authorization = authorizer.Authorize(
        request,
        "POST",
        "/api/v1/owner/control/resume",
        OwnerHttpBinding.EmptyPayload("resume"));
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    if (isPrivateWorld)
    {
        var privateRuntime = services.GetRequiredService<PrivateWorldRuntime>();
        if (privateRuntime.FounderSetup is { Started: false })
            return Results.Conflict(new { message = "Place four configured founders, then select Start World." });
        var privateStateFile = services.GetRequiredService<PrivateWorldStateFile>();
        var wasPaused = privateRuntime.Society.IsPaused;
        privateRuntime.Resume();
        var changed = wasPaused && !privateRuntime.Society.IsPaused;
        if (changed)
        {
            privateStateFile.Save(privateRuntime);
        }

        return Results.Ok(OwnerControlReceipt.From("resume", changed, observations.GetSnapshot()));
    }

    var runtime = services.GetRequiredService<OwnerWorldRuntime>();
    var stateFile = services.GetRequiredService<OwnerWorldStateFile>();
    var legacyChanged = runtime.Resume($"owner-device:{authorization.Value!.DeviceId}");
    if (legacyChanged)
    {
        stateFile.Save(runtime);
    }

    return Results.Ok(OwnerControlReceipt.From("resume", legacyChanged, runtime.Capture().Snapshot));
});

app.MapPost("/api/v1/owner/founders/place", (
    OwnerSignedHttpRequest<OwnerFounderPlacementAction> request,
    OwnerRequestAuthorizer authorizer,
    ProviderConfigurationStore providers,
    IServiceProvider services) =>
{
    if (!isPrivateWorld || request?.Action is not { Cognition: { } cognition } action)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A founder placement is required in a private world."] });
    string payload;
    try { payload = OwnerHttpBinding.FounderPlacementPayload(action); }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
    }
    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/founders/place", payload);
    if (!authorization.IsSuccess)
        return OwnerFailures.ToHttpResult(authorization.Failure);
    if (cognition.InhabitantId != action.FounderId || cognition.Role != PlayerDecisionProviders.PersonalRole ||
        cognition.ForgetCredential || cognition.Provider is not (PlayerDecisionProviders.OpenAi or PlayerDecisionProviders.OllamaCloud))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["cognition"] = ["Choose one personal hosted model for this founder."] });

    lock (founderSetupGate)
    {
        try
        {
            var runtime = services.GetRequiredService<PrivateWorldRuntime>();
            var position = new GridPoint(action.X, action.Y);
            runtime.ValidateFounderPlacement(action.FounderId, position);
            providers.Configure(cognition);
            var household = runtime.PlaceFounder(action.FounderId, position);
            services.GetRequiredService<PrivateWorldStateFile>().Save(runtime);
            var placed = runtime.FounderSetup!.FounderIds.Count;
            return Results.Ok(new OwnerFounderPlacementReceipt(action.FounderId, household, placed, PrivateWorldRuntime.RequiredFounders));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
        }
    }
});

app.MapPost("/api/v1/owner/agents/place", (
    OwnerSignedHttpRequest<OwnerAgentPlacementAction> request,
    OwnerRequestAuthorizer authorizer,
    ProviderConfigurationStore providers,
    IServiceProvider services,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("ClankerWorld.AgentPlacement");
    if (!isPrivateWorld || request?.Action is not { Cognition: { } cognition } action)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["An agent placement is required in a private world."] });
    string payload;
    try { payload = OwnerHttpBinding.AgentPlacementPayload(action); }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
    }
    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/agents/place", payload);
    if (!authorization.IsSuccess)
        return OwnerFailures.ToHttpResult(authorization.Failure);
    if (cognition.InhabitantId != action.AgentId || cognition.Role != PlayerDecisionProviders.PersonalRole ||
        cognition.ForgetCredential || cognition.Provider is not (PlayerDecisionProviders.OpenAi or PlayerDecisionProviders.OllamaCloud))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["cognition"] = ["Choose one personal hosted model for this agent."] });

    lock (founderSetupGate)
    {
        try
        {
            var runtime = services.GetRequiredService<PrivateWorldRuntime>();
            var position = new GridPoint(action.X, action.Y);
            runtime.ValidateAgentPlacement(action.AgentId, position);
            providers.Configure(cognition);
            var household = runtime.AddAgent(action.AgentId, position);
            services.GetRequiredService<PrivateWorldStateFile>().Save(runtime);
            AgentPlacementLog.Placed(logger, action.AgentId, household, runtime.WorldTick, action.X, action.Y);
            return Results.Ok(new OwnerAgentPlacementReceipt(action.AgentId, household));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            var loggedId = action.AgentId is { Length: 38 } id &&
                id.StartsWith("agent:", StringComparison.Ordinal) &&
                Guid.TryParseExact(id["agent:".Length..], "N", out _)
                    ? id : "<invalid-id>";
            AgentPlacementLog.Rejected(logger, loggedId, exception.GetType().Name);
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
        }
    }
});

app.MapPost("/api/v1/owner/control/start-world", (
    OwnerSignedHttpRequest<OwnerControlAction> request,
    OwnerRequestAuthorizer authorizer,
    ProviderConfigurationStore providers,
    IServiceProvider services,
    OwnerWorldObservationStore observations) =>
{
    if (!isPrivateWorld || !IsControl(request, "start-world"))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action.operation"] = ["This endpoint starts a configured private world."] });
    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/control/start-world",
        OwnerHttpBinding.EmptyPayload("start-world"));
    if (!authorization.IsSuccess)
        return OwnerFailures.ToHttpResult(authorization.Failure);

    lock (founderSetupGate)
    {
        var runtime = services.GetRequiredService<PrivateWorldRuntime>();
        var setup = runtime.FounderSetup;
        if (setup is null || setup.Started || setup.FounderIds.Count != PrivateWorldRuntime.RequiredFounders)
            return Results.Conflict(new { message = "Place all four founders before starting the world." });
        var providerStatus = providers.CaptureStatus();
        var assignments = providerStatus.Assignments ?? [];
        foreach (var founderId in setup.FounderIds)
        {
            var personal = assignments.Where(item => item.InhabitantId == founderId).ToArray();
            if (personal.Length != 2 || !personal.Any(item => item.Role == PlayerDecisionProviders.RoutineRole) ||
                !personal.Any(item => item.Role == PlayerDecisionProviders.PlanningRole) ||
                personal.Any(item => item.Provider is not (PlayerDecisionProviders.OpenAi or PlayerDecisionProviders.OllamaCloud)) ||
                personal.Select(item => (item.Provider, item.Model, item.CredentialSlotId)).Distinct().Count() != 1 ||
                personal.Any(item => item.CredentialSlotId is { } slot
                    ? !(providerStatus.CredentialSlots ?? []).Any(saved => saved.Id == slot && saved.Provider == item.Provider)
                    : !providerStatus.Providers.Any(option => option.Provider == item.Provider && option.HasCredential)))
                return Results.Conflict(new { message = "Every founder needs one configured personal model and credential." });
        }
        runtime.StartWorld();
        services.GetRequiredService<PrivateWorldStateFile>().Save(runtime);
        return Results.Ok(OwnerControlReceipt.From("start-world", true, observations.GetSnapshot()));
    }
});

app.MapPost("/api/v1/owner/instructions", (
    OwnerSignedHttpRequest<OwnerInstructionAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (request?.Action is null || !TryParseInstructionKind(request.Action.Kind, out var kind))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.kind"] = ["Instruction kind must be suggestive or must_do."],
        });
    }

    string payload;
    try
    {
        payload = OwnerHttpBinding.InstructionPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/instructions", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    if (isPrivateWorld)
    {
        var privateRuntime = services.GetRequiredService<PrivateWorldRuntime>();
        var privateStateFile = services.GetRequiredService<PrivateWorldStateFile>();
        try
        {
            var receipt = privateRuntime.SubmitInstruction(new OwnerInstructionRequest(
                request.Action.IdempotencyKey,
                $"owner-device:{authorization.Value!.DeviceId}",
                request.Action.TargetInhabitantId,
                kind,
                request.Action.Text));
            privateStateFile.Save(privateRuntime);
            return Results.Ok(receipt);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["action"] = [exception.Message],
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new OwnerControlFailure("idempotency_conflict", exception.Message));
        }
    }

    try
    {
        var runtime = services.GetRequiredService<OwnerWorldRuntime>();
        var stateFile = services.GetRequiredService<OwnerWorldStateFile>();
        // The transport has no issuer field. This value is minted from the
        // authenticated server-side device identity rather than accepted from
        // a client payload.
        var receipt = runtime.SubmitInstruction(new OwnerInstructionRequest(
            request.Action.IdempotencyKey,
            $"owner-device:{authorization.Value!.DeviceId}",
            request.Action.TargetInhabitantId,
            kind,
            request.Action.Text));
        stateFile.Save(runtime);
        return Results.Ok(receipt);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new OwnerControlFailure("idempotency_conflict", exception.Message));
    }
});

app.MapBuildingDesign(isPrivateWorld);

app.MapPost("/api/v1/owner/content/propose", (
    OwnerSignedHttpRequest<OwnerContentPackageAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_content_required",
            "Data-only content governance is available only in the integrated private world."));
    }

    if (!OwnerContentBinding.TryMapManifest(request?.Action, out var manifest, out var failure))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [failure],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.ProposePayload(request!.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/propose", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    try
    {
        var record = runtime.ProposeContent(manifest!);
        stateFile.Save(runtime);
        return Results.Ok(OwnerContentPackageReceipt.From("propose", record));
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
    }
});

app.MapPost("/api/v1/owner/content/validate", (
    OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_content_required",
            "Data-only content governance is available only in the integrated private world."));
    }

    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.packageId"] = ["A package ID is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.PackageIdPayload("validate", request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/validate", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    try
    {
        var resolution = runtime.ResolveContent(request.Action.PackageId);
        var record = runtime.ValidateContent(request.Action.PackageId, resolution);
        stateFile.Save(runtime);
        return Results.Ok(OwnerContentPackageReceipt.From("validate", record));
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
    }
});

app.MapPost("/api/v1/owner/content/approve", (
    OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_content_required",
            "Data-only content governance is available only in the integrated private world."));
    }

    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.packageId"] = ["A package ID is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.PackageIdPayload("approve", request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/approve", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    try
    {
        var record = runtime.ApproveContent(request.Action.PackageId);
        stateFile.Save(runtime);
        return Results.Ok(OwnerContentPackageReceipt.From("approve", record));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
    }
});

app.MapPost("/api/v1/owner/content/stage", (
    OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_content_required",
            "Data-only content governance is available only in the integrated private world."));
    }

    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.packageId"] = ["A package ID is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.PackageIdPayload("stage", request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/stage", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    try
    {
        var record = runtime.StageContent(request.Action.PackageId);
        stateFile.Save(runtime);
        return Results.Ok(OwnerContentPackageReceipt.From("stage", record));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
    }
});

app.MapPost("/api/v1/owner/content/rollback", (
    OwnerSignedHttpRequest<OwnerContentRollbackAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services,
    ILogger<PrivateWorldRuntimeService> logger) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_content_required",
            "Data-only content governance is available only in the integrated private world."));
    }

    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A package ID and rollback reason are required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.RollbackPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/rollback", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    try
    {
        var record = runtime.RollbackContent(request.Action.PackageId, request.Action.Reason);
        stateFile.Save(runtime);
        OwnerContentTelemetry.Rollback(logger, runtime.WorldTick, record.Manifest.PackageId, "quarantined");
        return Results.Ok(OwnerContentPackageReceipt.From("rollback", record));
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }
    catch (InvalidOperationException exception)
    {
        OwnerContentTelemetry.Rollback(logger, runtime.WorldTick, request.Action.PackageId, "rejected");
        return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
    }
});

app.MapPost("/api/v1/owner/buildings/place", (
    OwnerSignedHttpRequest<OwnerBuildingPlacementAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_required",
            "Building placement is available only in the integrated private world."));
    }

    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A building placement action is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.BuildingPlacementPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/buildings/place", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    var result = runtime.PlaceBuilding(
        request.Action.InstanceId,
        request.Action.DefinitionId,
        new ClankerWorld.Simulation.Harness.GridPoint(request.Action.X, request.Action.Y));
    if (!result.Applied)
    {
        return Results.Conflict(new OwnerControlFailure("building_rejected", result.Failure ?? "Building placement was rejected."));
    }

    stateFile.Save(runtime);
    return Results.Ok(result);
});

app.MapPost("/api/v1/owner/production/start", (
    OwnerSignedHttpRequest<OwnerProductionStartAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (!isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_required",
            "Recipe production is available only in the integrated private world."));
    }

    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A production-start action is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerContentBinding.ProductionStartPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/production/start", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var runtime = services.GetRequiredService<PrivateWorldRuntime>();
    var stateFile = services.GetRequiredService<PrivateWorldStateFile>();
    var result = runtime.StartProduction(
        request.Action.RecipeId,
        request.Action.BuildingInstanceId,
        request.Action.WorkerId);
    if (!result.Applied)
    {
        return Results.Conflict(new OwnerControlFailure("production_rejected", result.Failure ?? "Recipe production was rejected."));
    }

    stateFile.Save(runtime);
    return Results.Ok(result);
});

app.MapPost("/api/v1/owner/authoring", (
    OwnerSignedHttpRequest<OwnerAuthoringBatchAction> request,
    OwnerRequestAuthorizer authorizer,
    IServiceProvider services) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["An authoring batch is required."],
        });
    }

    if (isPrivateWorld)
    {
        return Results.Conflict(new OwnerControlFailure(
            "private_world_authoring_pending",
            "Private-world authoring is not migrated yet; content activation remains disabled in the alpha runtime."));
    }

    string payload;
    try
    {
        payload = OwnerHttpBinding.AuthoringPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/authoring", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    if (!OwnerAuthoringMapper.TryMap(request.Action, out var batch, out var failure))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [failure],
        });
    }

    var runtime = services.GetRequiredService<OwnerWorldRuntime>();
    var stateFile = services.GetRequiredService<OwnerWorldStateFile>();
    var receipt = runtime.ApplyAuthoringBatch(batch! with
    {
        IssuerId = $"owner-device:{authorization.Value!.DeviceId}",
    });
    if (receipt.Applied)
    {
        stateFile.Save(runtime);
    }

    return Results.Ok(receipt);
});

app.MapPost("/api/v1/owner/pairings/approve", (
    OwnerSignedHttpRequest<OwnerPairingApprovalAction> request,
    OwnerRequestAuthorizer authorizer,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A pairing ID and comparison code are required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerHttpBinding.PairingApprovalPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/pairings/approve", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var approved = authority.ApprovePendingPairing(request.Action.PairingId, request.Action.PairingCode);
    // Treat paired-device approval exactly like local bootstrap approval: a
    // failed comparison changes the bounded durable attempt count.
    stateFile.Save(authority);
    if (!approved.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(approved.Failure);
    }

    return Results.Ok(approved.Value);
});

app.MapPost("/api/v1/owner/devices/revoke", (
    OwnerSignedHttpRequest<OwnerDeviceManagementAction> request,
    OwnerRequestAuthorizer authorizer,
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action.deviceId"] = ["A device ID is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerHttpBinding.DeviceManagementPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/devices/revoke", payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    var revoked = authority.RevokeDevice(request.Action.DeviceId);
    stateFile.Save(authority);
    if (!revoked.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(revoked.Failure);
    }

    return Results.Ok(revoked.Value);
});

// Paired devices may inspect the public-key registry, but this is still a
// signed, one-use request rather than a bearer-style read endpoint. The
// result contains only OwnerDevice records: public SPKIs/fingerprints and
// lifecycle timestamps, never pairing codes, challenge nonces, or secrets.
app.MapPost("/api/v1/owner/devices/list", (
    OwnerSignedHttpRequest<OwnerDeviceListAction> request,
    OwnerRequestAuthorizer authorizer,
    OwnerAuthorityStore authority) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A device-list action is required."],
        });
    }

    var authorization = authorizer.Authorize(
        request,
        "POST",
        "/api/v1/owner/devices/list",
        OwnerHttpBinding.DeviceListPayload());
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    return Results.Ok(authority.GetDevices());
});

// Provider status is owner-only even though it contains no key material. It
// reveals which hosted account integration is active and therefore uses the
// same one-use signed request boundary as every other private-world control.
app.MapPost("/api/v1/owner/providers/status", (
    OwnerSignedHttpRequest<OwnerProviderStatusAction> request,
    OwnerRequestAuthorizer authorizer,
    ProviderConfigurationStore providers) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A provider-status action is required."],
        });
    }

    var authorization = authorizer.Authorize(
        request,
        "POST",
        "/api/v1/owner/providers/status",
        OwnerHttpBinding.ProviderStatusPayload());
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    return Results.Ok(providers.CaptureStatus());
});

app.MapPost("/api/v1/owner/providers/configure", (
    OwnerSignedHttpRequest<OwnerProviderConfigurationAction> request,
    OwnerRequestAuthorizer authorizer,
    ProviderConfigurationStore providers,
    OwnerWorldObservationStore observations) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["A provider-configuration action is required."],
        });
    }

    string payload;
    try
    {
        payload = OwnerHttpBinding.ProviderConfigurationPayload(request.Action);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }

    var authorization = authorizer.Authorize(
        request,
        "POST",
        "/api/v1/owner/providers/configure",
        payload);
    if (!authorization.IsSuccess)
    {
        return OwnerFailures.ToHttpResult(authorization.Failure);
    }

    try
    {
        if (request.Action.InhabitantId is { } target &&
            !observations.GetSnapshot().Inhabitants.Any(item => item.Id == target))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["inhabitantId"] = ["Choose an inhabitant in this world."],
            });
        }
        return Results.Ok(providers.Configure(request.Action));
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = [exception.Message],
        });
    }
});

// Legacy diagnostic routes deliberately remain present only to make their
// read denial explicit (and to retain a 405 response for accidental POSTs).
app.MapGet("/api/v1/world", OwnerFailures.UnpairedObservation);
app.MapGet("/api/v1/events", OwnerFailures.UnpairedObservation);
app.MapGet("/api/v1/reconnect", OwnerFailures.UnpairedObservation);

app.Run();

static bool IsControl(OwnerSignedHttpRequest<OwnerControlAction>? request, string expectedOperation) =>
    request?.Action is not null &&
    string.Equals(request.Action.Operation, expectedOperation, StringComparison.Ordinal);

static bool TryParseInstructionKind(string? value, out OwnerInstructionKind kind)
{
    kind = value?.Trim().ToLowerInvariant() switch
    {
        "suggestive" => OwnerInstructionKind.Suggestive,
        "must_do" => OwnerInstructionKind.MustDo,
        _ => default,
    };
    return value is not null && (value.Trim().Equals("suggestive", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("must_do", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
