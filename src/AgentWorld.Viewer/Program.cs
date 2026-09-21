using System.Net;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Harness;
using AgentWorld.Viewer.Control;
using AgentWorld.Viewer.Observation;

var builder = WebApplication.CreateBuilder(args);
var runtimeSeed = builder.Configuration["AgentWorld:Runtime:Seed"] ?? SeededWorldObservationStore.SampleSeed;
var advanceFixture = builder.Configuration.GetValue<bool>("AgentWorld:Runtime:AdvanceScript");
var configuredDecisionProvider = builder.Configuration["AgentWorld:Runtime:DecisionProvider"] ?? "deterministic";
var configuredJevModel = builder.Configuration["AgentWorld:Runtime:JevModel"] ?? "jev-1.13.0";
var publicPort = builder.Configuration.GetValue("AgentWorld:Http:Port", 5188);
var localApprovalPort = builder.Configuration.GetValue<int?>("AgentWorld:Pairing:LocalApprovalPort") ?? 0;
if (publicPort is <= 0 or > 65535 || localApprovalPort is < 0 or > 65535 || localApprovalPort == publicPort)
{
    throw new InvalidOperationException("AgentWorld HTTP ports must be distinct values between 1 and 65535.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, publicPort);
    if (localApprovalPort > 0)
    {
        options.Listen(IPAddress.Loopback, localApprovalPort);
    }
});

var authorityStatePath = builder.Configuration["AgentWorld:Pairing:StatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "owner-authority.json");
var runtimeStatePath = builder.Configuration["AgentWorld:Runtime:StatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "phase-two-runtime.json");
var approvedAssetCatalogPath = builder.Configuration["AgentWorld:Assets:CatalogPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "approved-assets.json");
var configuredAuthorityId = builder.Configuration["AgentWorld:Pairing:ServerAuthorityId"] ??
    $"agentworld-host:{Environment.MachineName}";

// The catalog is loaded only from a host-owned path at startup. Its absence
// intentionally yields an empty, deny-all allow-list; malformed existing
// catalog files stop startup rather than becoming a partial approval set.
var approvedAssetCatalog = ApprovedAssetCatalog.LoadOrDeny(approvedAssetCatalogPath);
builder.Services.AddSingleton<IPhaseTwoApprovedAssetReferencePolicy>(approvedAssetCatalog);
builder.Services.AddSingleton(approvedAssetCatalog);
builder.Services.AddHttpClient("typesafe");
builder.Services.AddSingleton<IDecisionProvider>(services =>
{
    if (!string.Equals(configuredDecisionProvider, "jev", StringComparison.OrdinalIgnoreCase))
    {
        return new DeterministicDecisionProvider();
    }

    return new JevDecisionProvider(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("typesafe"),
        () => Environment.GetEnvironmentVariable("TYPESAFE_API_KEY"),
        model: configuredJevModel);
});
builder.Services.AddSingleton<PhaseTwoWorldStateFile>(services => new PhaseTwoWorldStateFile(
    runtimeStatePath,
    approvedAssetCatalog,
    services.GetRequiredService<IDecisionProvider>()));
builder.Services.AddSingleton<PhaseTwoWorldRuntime>(services => services
    .GetRequiredService<PhaseTwoWorldStateFile>()
    .LoadOrCreate(runtimeSeed));
builder.Services.AddSingleton<PhaseTwoWorldObservationStore>();
builder.Services.AddSingleton(new OwnerAuthorityStateFile(authorityStatePath));
var pairingHostOptions = new OwnerPairingHostOptions(localApprovalPort);
builder.Services.AddSingleton(pairingHostOptions);
builder.Services.AddSingleton<OwnerAuthorityStore>(services =>
{
    var runtime = services.GetRequiredService<PhaseTwoWorldRuntime>();
    var worldId = runtime.Capture().Snapshot.World.Identity.WorldId;
    return services.GetRequiredService<OwnerAuthorityStateFile>().LoadOrCreate(
        new OwnerAuthorityIdentity(configuredAuthorityId, worldId));
});
builder.Services.AddSingleton<OwnerRequestAuthorizer>();
if (advanceFixture)
{
    builder.Services.AddHostedService<PhaseTwoWorldRuntimeService>();
}

var app = builder.Build();
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
    PhaseTwoWorldObservationStore observations) =>
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

    return Results.Ok(new ViewerOwnerReconnect(
        observations.GetOwnerHandshake(),
        observations.GetReconnectBaseline(request.Action.AfterEventId)));
});

app.MapPost("/api/v1/owner/control/pause", (
    OwnerSignedHttpRequest<OwnerControlAction> request,
    OwnerRequestAuthorizer authorizer,
    PhaseTwoWorldRuntime runtime,
    PhaseTwoWorldStateFile stateFile) =>
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

    var changed = runtime.Pause($"owner-device:{authorization.Value!.DeviceId}");
    if (changed)
    {
        stateFile.Save(runtime);
    }

    return Results.Ok(OwnerControlReceipt.From("pause", changed, runtime.Capture().Snapshot));
});

app.MapPost("/api/v1/owner/control/resume", (
    OwnerSignedHttpRequest<OwnerControlAction> request,
    OwnerRequestAuthorizer authorizer,
    PhaseTwoWorldRuntime runtime,
    PhaseTwoWorldStateFile stateFile) =>
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

    var changed = runtime.Resume($"owner-device:{authorization.Value!.DeviceId}");
    if (changed)
    {
        stateFile.Save(runtime);
    }

    return Results.Ok(OwnerControlReceipt.From("resume", changed, runtime.Capture().Snapshot));
});

app.MapPost("/api/v1/owner/instructions", (
    OwnerSignedHttpRequest<OwnerInstructionAction> request,
    OwnerRequestAuthorizer authorizer,
    PhaseTwoWorldRuntime runtime,
    PhaseTwoWorldStateFile stateFile) =>
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

    try
    {
        // The transport has no issuer field. This value is minted from the
        // authenticated server-side device identity rather than accepted from
        // a client payload.
        var receipt = runtime.SubmitInstruction(new PhaseTwoInstructionRequest(
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

app.MapPost("/api/v1/owner/authoring", (
    OwnerSignedHttpRequest<OwnerAuthoringBatchAction> request,
    OwnerRequestAuthorizer authorizer,
    PhaseTwoWorldRuntime runtime,
    PhaseTwoWorldStateFile stateFile) =>
{
    if (request?.Action is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["action"] = ["An authoring batch is required."],
        });
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

// Legacy diagnostic routes deliberately remain present only to make their
// read denial explicit (and to retain a 405 response for accidental POSTs).
app.MapGet("/api/v1/world", OwnerFailures.UnpairedObservation);
app.MapGet("/api/v1/events", OwnerFailures.UnpairedObservation);
app.MapGet("/api/v1/reconnect", OwnerFailures.UnpairedObservation);

app.Run();

static bool IsControl(OwnerSignedHttpRequest<OwnerControlAction>? request, string expectedOperation) =>
    request?.Action is not null &&
    string.Equals(request.Action.Operation, expectedOperation, StringComparison.Ordinal);

static bool TryParseInstructionKind(string? value, out PhaseTwoInstructionKind kind)
{
    kind = value?.Trim().ToLowerInvariant() switch
    {
        "suggestive" => PhaseTwoInstructionKind.Suggestive,
        "must_do" => PhaseTwoInstructionKind.MustDo,
        _ => default,
    };
    return value is not null && (value.Trim().Equals("suggestive", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("must_do", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
