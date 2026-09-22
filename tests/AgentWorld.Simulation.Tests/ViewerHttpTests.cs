using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Playtest;
using AgentWorld.Viewer.Control;
using AgentWorld.Viewer.Observation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AgentWorld.Simulation.Tests;

public sealed class ViewerHttpTests(ViewerWebApplicationFactory factory) : IClassFixture<ViewerWebApplicationFactory>
{
    private const string ApprovedAssetDigest =
        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task UnpairedClientsCanDiscoverPairingButCannotReadTheWorld()
    {
        using var client = factory.CreateClient();

        var handshake = await client.GetFromJsonAsync<ViewerHandshake>("/api/v1/handshake");
        using var world = await client.GetAsync("/api/v1/world");
        using var events = await client.GetAsync("/api/v1/events?afterEventId=0");
        using var reconnect = await client.GetAsync("/api/v1/reconnect?afterEventId=0");
        using var attemptedWrite = await client.PostAsync("/api/v1/world", content: null);
        using var remotePairingApproval = await client.PostAsJsonAsync(
            "/api/v1/local/pairings/pairing_not_real/approve",
            new LocalPairingApprovalHttpRequest("000000"));
        using var remoteRecoveryRevoke = await client.PostAsJsonAsync(
            "/api/v1/local/devices/device_not_real/revoke",
            new LocalDeviceRevokeHttpRequest(null));
        using var unsignedDeviceList = await client.PostAsJsonAsync(
            "/api/v1/owner/devices/list",
            new { });
        var page = await client.GetStringAsync("/");

        Assert.NotNull(handshake);
        Assert.Equal(new ProtocolVersion(1, 1), handshake.Protocol);
        Assert.Equal(["owner-device-pairing.v1"], handshake.ServerCapabilities);
        Assert.Equal(HttpStatusCode.Unauthorized, world.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, events.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reconnect.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, attemptedWrite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remotePairingApproval.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remoteRecoveryRevoke.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsignedDeviceList.StatusCode);
        Assert.Contains("read-only deterministic inspection", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PairingBackpressureUsesTooManyRequestsInsteadOfCreatingUnboundedState()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-viewer-pairing-capacity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var host = new ViewerWebApplicationFactory(directory);
            using var client = host.CreateClient();
            for (var index = 0; index < OwnerAuthorityStore.MaximumPendingPairings; index++)
            {
                using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using var started = await client.PostAsJsonAsync(
                    "/api/v1/pairings",
                    new StartOwnerPairingHttpRequest(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())));
                Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            }

            using var overflowKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var overflow = await client.PostAsJsonAsync(
                "/api/v1/pairings",
                new StartOwnerPairingHttpRequest(Convert.ToBase64String(overflowKey.ExportSubjectPublicKeyInfo())));

            Assert.Equal((HttpStatusCode)429, overflow.StatusCode);
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
    public async Task PairedDeviceCanObserveThenIssueServerValidatedControlAndPausedAuthoringRequests()
    {
        using var client = factory.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var pairing = await StartAndActivateAsync(client, key, publicKey);

        using var observation = await SendSignedAsync(
            client,
            key,
            pairing.DeviceId,
            "/api/v1/owner/reconnect",
            new OwnerReconnectAction(0),
            OwnerHttpBinding.ReconnectPayload(new OwnerReconnectAction(0)));
        var reconnect = await observation.Content.ReadFromJsonAsync<ViewerOwnerReconnect>();

        Assert.Equal(HttpStatusCode.OK, observation.StatusCode);
        Assert.NotNull(reconnect);
        Assert.Contains("owner-observation.read.v1", reconnect.Handshake.ServerCapabilities);
        Assert.Contains("owner-control.request.v1", reconnect.Handshake.ServerCapabilities);
        Assert.Single(reconnect.Baseline.Snapshot.Inhabitants);
        Assert.NotNull(reconnect.Baseline.Snapshot.Authoring);

        using var pause = await SendSignedAsync(
            client,
            key,
            pairing.DeviceId,
            "/api/v1/owner/control/pause",
            new OwnerControlAction("pause"),
            OwnerHttpBinding.EmptyPayload("pause"));
        var pauseReceipt = await pause.Content.ReadFromJsonAsync<OwnerControlReceipt>();
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);
        Assert.True(pauseReceipt!.Changed);
        Assert.True(pauseReceipt.IsPaused);

        var instructionAction = new OwnerInstructionAction(
            "instruction-http-1",
            "actor-scout",
            "must_do",
            "Return to the bedroll.");
        using var instruction = await SendSignedAsync(
            client,
            key,
            pairing.DeviceId,
            "/api/v1/owner/instructions",
            instructionAction,
            OwnerHttpBinding.InstructionPayload(instructionAction));
        var instructionReceipt = await instruction.Content.ReadFromJsonAsync<OwnerInstructionReceipt>();
        Assert.Equal(HttpStatusCode.OK, instruction.StatusCode);
        Assert.NotNull(instructionReceipt);
        Assert.Equal("instruction-0000000001", instructionReceipt.InstructionId);

        var runtime = factory.Services.GetRequiredService<OwnerWorldRuntime>();
        var beforeAuthoring = runtime.Capture();
        var water = beforeAuthoring.Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;
        var authoringAction = new OwnerAuthoringBatchAction(
            "authoring-http-1",
            [new OwnerAuthoringOperationAction(
                "set_terrain",
                null,
                "mountain",
                null,
                water.X,
                water.Y,
                null)]);
        using var authoring = await SendSignedAsync(
            client,
            key,
            pairing.DeviceId,
            "/api/v1/owner/authoring",
            authoringAction,
            OwnerHttpBinding.AuthoringPayload(authoringAction));
        var authoringReceipt = await authoring.Content.ReadFromJsonAsync<OwnerAuthoringBatchReceipt>();
        var afterAuthoring = runtime.Capture();

        Assert.Equal(HttpStatusCode.OK, authoring.StatusCode);
        Assert.True(authoringReceipt!.Applied, authoringReceipt.Failure);
        Assert.NotEqual(beforeAuthoring.Snapshot.CurrentMapManifestDigest, afterAuthoring.Snapshot.CurrentMapManifestDigest);
        Assert.Equal(beforeAuthoring.Snapshot.InitialMapManifestDigest, afterAuthoring.Snapshot.InitialMapManifestDigest);
        var queued = Assert.Single(afterAuthoring.Snapshot.Instructions);
        Assert.Equal($"owner-device:{pairing.DeviceId}", queued.IssuerId);
        Assert.Contains(
            $"issuer:owner-device:{pairing.DeviceId}",
            afterAuthoring.Events.Single(worldEvent => worldEvent.Kind == "paused").Detail,
            StringComparison.Ordinal);
        Assert.Contains(
            $"issuer=owner-device:{pairing.DeviceId}",
            afterAuthoring.Events[^1].Detail,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PairedOwnerCanAttachOnlyAnExactReferenceFromTheHostConfiguredCatalog()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-viewer-approved-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var catalogPath = System.IO.Path.Combine(directory, "approved-assets.json");
            File.WriteAllText(
                catalogPath,
                $$"""{"schemaVersion":1,"references":[{"assetId":"portrait-alice","assetDigest":"{{ApprovedAssetDigest}}"}]}""");

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var host = new ViewerWebApplicationFactory(directory, catalogPath);
            using var client = host.CreateClient();
            var device = await StartAndActivateAsync(host, client, key);

            using var pause = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/control/pause",
                new OwnerControlAction("pause"),
                OwnerHttpBinding.EmptyPayload("pause"));
            Assert.Equal(HttpStatusCode.OK, pause.StatusCode);

            var allowedAction = new OwnerAuthoringBatchAction(
                "approved-asset-http",
                [new OwnerAuthoringOperationAction(
                    "add_approved_asset_reference",
                    "portrait-alice",
                    ApprovedAssetDigest,
                    null,
                    null,
                    null,
                    null)]);
            using var allowed = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/authoring",
                allowedAction,
                OwnerHttpBinding.AuthoringPayload(allowedAction));
            var allowedReceipt = await allowed.Content.ReadFromJsonAsync<OwnerAuthoringBatchReceipt>();

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            Assert.NotNull(allowedReceipt);
            Assert.True(allowedReceipt.Applied, allowedReceipt.Failure);

            var rejectedAction = allowedAction with
            {
                BatchId = "unapproved-asset-http",
                Operations = [new OwnerAuthoringOperationAction(
                    "add_approved_asset_reference",
                    "portrait-bob",
                    ApprovedAssetDigest,
                    null,
                    null,
                    null,
                    null)],
            };
            using var rejected = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/authoring",
                rejectedAction,
                OwnerHttpBinding.AuthoringPayload(rejectedAction));
            var rejectedReceipt = await rejected.Content.ReadFromJsonAsync<OwnerAuthoringBatchReceipt>();

            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.NotNull(rejectedReceipt);
            Assert.False(rejectedReceipt.Applied);
            Assert.Contains("server-owned asset catalog", rejectedReceipt.Failure, StringComparison.Ordinal);
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
    public async Task PairedOwnerCanGovernPrivateContentThroughTheSignedLifecycle()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-viewer-private-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var host = new ViewerWebApplicationFactory(directory, null, privateWorld: true);
            using var client = host.CreateClient();
            var device = await StartAndActivateAsync(host, client, key);
            var packageDigest =
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var packageVersion = ContentVersion.Parse("1.0.0");
            var building = new BuildingDefinition(
                packageDigest,
                "camp-kitchen",
                packageVersion,
                "Camp kitchen",
                1,
                1,
                2,
                [new ContentQuantity("wood", 2)],
                ["camp"]);
            var package = new OwnerContentPackageAction(
                "camp-recipes",
                "1.0.0",
                packageDigest,
                [],
                [new OwnerContentDefinitionAction(
                    BuildingDefinition.SchemaKind,
                    building.LocalId,
                    building.Version.ToString(),
                    building.DisplayName,
                    building.PayloadDigest,
                    """{"schema":"building/v1","width":1,"height":1,"capacity":2,"buildCosts":[{"resourceId":"wood","amount":2}],"tags":["camp"]}""")],
                []);

            using var proposed = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/content/propose",
                package,
                OwnerContentBinding.ProposePayload(package));
            var proposedReceipt = await proposed.Content.ReadFromJsonAsync<OwnerContentPackageReceipt>();
            Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
            Assert.Equal("proposed", proposedReceipt!.Lifecycle);

            var packageId = new OwnerContentPackageIdAction(package.PackageId);
            using var validated = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/content/validate",
                packageId,
                OwnerContentBinding.PackageIdPayload("validate", packageId));
            var validatedReceipt = await validated.Content.ReadFromJsonAsync<OwnerContentPackageReceipt>();
            Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
            Assert.Equal("validated", validatedReceipt!.Lifecycle);
            Assert.NotNull(validatedReceipt.LockDigest);

            using var approved = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/content/approve",
                packageId,
                OwnerContentBinding.PackageIdPayload("approve", packageId));
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

            using var staged = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/content/stage",
                packageId,
                OwnerContentBinding.PackageIdPayload("stage", packageId));
            var stagedReceipt = await staged.Content.ReadFromJsonAsync<OwnerContentPackageReceipt>();
            Assert.Equal(HttpStatusCode.OK, staged.StatusCode);
            Assert.Equal("staged", stagedReceipt!.Lifecycle);
            Assert.Equal(0, stagedReceipt.StagedTick);

            var runtime = host.Services.GetRequiredService<PrivateWorldRuntime>();
            _ = await runtime.AdvanceOneTickAsync();
            host.Services.GetRequiredService<PrivateWorldStateFile>().Save(runtime);
            Assert.Equal("active", runtime.Content.Packages.Single().Lifecycle.ToString().ToLowerInvariant());
            Assert.Equal(building.CanonicalId, Assert.Single(runtime.WorldContent.Buildings).CanonicalId);

            var worker = runtime.Inhabitants.Single(item => item.InhabitantId == "founder-rowan");
            var placementAction = new OwnerBuildingPlacementAction(
                "camp-kitchen-one",
                building.CanonicalId,
                worker.Position.X,
                worker.Position.Y);
            using var placed = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/buildings/place",
                placementAction,
                OwnerContentBinding.BuildingPlacementPayload(placementAction));
            var placementReceipt = await placed.Content.ReadFromJsonAsync<BuildingPlacementResult>();
            Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
            Assert.True(placementReceipt!.Applied, placementReceipt.Failure);
            Assert.Single(runtime.WorldSimulation.Buildings);

            var rollback = new OwnerContentRollbackAction(package.PackageId, "preview mismatch");
            using var rolledBack = await SendSignedAsync(
                host,
                client,
                key,
                device.DeviceId,
                "/api/v1/owner/content/rollback",
                rollback,
                OwnerContentBinding.RollbackPayload(rollback));
            var rollbackReceipt = await rolledBack.Content.ReadFromJsonAsync<OwnerContentPackageReceipt>();
            Assert.Equal(HttpStatusCode.OK, rolledBack.StatusCode);
            Assert.Equal("quarantined", rollbackReceipt!.Lifecycle);
            Assert.Empty(runtime.WorldContent.Buildings);
            Assert.Empty(runtime.WorldSimulation.Buildings);
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
    public async Task ReplayedChallengeIsRejectedBeforeASecondControlCanReachTheRuntime()
    {
        using var client = factory.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pairing = await StartAndActivateAsync(client, key, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        var envelope = await CreateSignedRequestAsync(
            client,
            key,
            pairing.DeviceId,
            "/api/v1/owner/control/pause",
            new OwnerControlAction("pause"),
            OwnerHttpBinding.EmptyPayload("pause"));

        using var first = await client.PostAsJsonAsync("/api/v1/owner/control/pause", envelope);
        using var replay = await client.PostAsJsonAsync("/api/v1/owner/control/pause", envelope);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var runtime = factory.Services.GetRequiredService<OwnerWorldRuntime>();
        Assert.True(runtime.Capture().Snapshot.IsPaused);
        Assert.Single(runtime.Capture().Events, worldEvent => worldEvent.Kind == "paused");
    }

    [Fact]
    public async Task PairedOwnerCanApproveAndRevokeAnotherDeviceThroughSignedRequests()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-viewer-device-management-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var host = new ViewerWebApplicationFactory(directory);
            using var client = host.CreateClient();
            var firstDevice = await StartAndActivateAsync(host, client, firstKey);

            using var start = await client.PostAsJsonAsync(
                "/api/v1/pairings",
                new StartOwnerPairingHttpRequest(Convert.ToBase64String(secondKey.ExportSubjectPublicKeyInfo())));
            var pending = await start.Content.ReadFromJsonAsync<OwnerPairingStart>();
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);
            Assert.NotNull(pending);

            var approveAction = new OwnerPairingApprovalAction(pending.PairingId, pending.PairingCode);
            using var approve = await SendSignedAsync(
                host,
                client,
                firstKey,
                firstDevice.DeviceId,
                "/api/v1/owner/pairings/approve",
                approveAction,
                OwnerHttpBinding.PairingApprovalPayload(approveAction));
            var approval = await approve.Content.ReadFromJsonAsync<OwnerPairingApproval>();
            Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
            Assert.NotNull(approval);
            Assert.Equal(pending.DeviceId, approval.DeviceId);

            var activation = new ActivateOwnerPairingHttpRequest(
                pending.PairingId,
                pending.ActivationCanonicalProof,
                Sign(secondKey, pending.ActivationCanonicalProof));
            using var activate = await client.PostAsJsonAsync("/api/v1/pairings/activate", activation);
            var secondDevice = await activate.Content.ReadFromJsonAsync<OwnerDevice>();
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
            Assert.NotNull(secondDevice);
            Assert.Equal(OwnerDeviceState.Active, secondDevice.State);

            var listAction = new OwnerDeviceListAction();
            var activeListEnvelope = await CreateSignedRequestAsync(
                host,
                client,
                firstKey,
                firstDevice.DeviceId,
                "/api/v1/owner/devices/list",
                listAction,
                OwnerHttpBinding.DeviceListPayload());
            using var activeList = await client.PostAsJsonAsync(
                "/api/v1/owner/devices/list",
                activeListEnvelope);
            using var activeListReplay = await client.PostAsJsonAsync(
                "/api/v1/owner/devices/list",
                activeListEnvelope);
            var activeDevices = await activeList.Content.ReadFromJsonAsync<OwnerDevice[]>();

            Assert.Equal(HttpStatusCode.OK, activeList.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, activeListReplay.StatusCode);
            Assert.NotNull(activeDevices);
            Assert.Equal(2, activeDevices.Length);
            Assert.All(activeDevices, device => Assert.Equal(OwnerDeviceState.Active, device.State));
            var listedFirstDevice = Assert.Single(activeDevices, device => device.DeviceId == firstDevice.DeviceId);
            var listedSecondDevice = Assert.Single(activeDevices, device => device.DeviceId == secondDevice.DeviceId);
            Assert.Equal(firstDevice.PublicKeyFingerprint, listedFirstDevice.PublicKeyFingerprint);
            Assert.Equal(secondDevice.PublicKeyFingerprint, listedSecondDevice.PublicKeyFingerprint);
            Assert.Equal(firstDevice.PublicKeySpkiBase64, listedFirstDevice.PublicKeySpkiBase64);
            Assert.Equal(secondDevice.PublicKeySpkiBase64, listedSecondDevice.PublicKeySpkiBase64);
            Assert.Null(listedFirstDevice.RevokedAtUtc);
            Assert.Null(listedSecondDevice.RevokedAtUtc);

            var revokeAction = new OwnerDeviceManagementAction(secondDevice.DeviceId);
            using var revoke = await SendSignedAsync(
                host,
                client,
                firstKey,
                firstDevice.DeviceId,
                "/api/v1/owner/devices/revoke",
                revokeAction,
                OwnerHttpBinding.DeviceManagementPayload(revokeAction));
            var revoked = await revoke.Content.ReadFromJsonAsync<OwnerDevice>();
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
            Assert.NotNull(revoked);
            Assert.Equal(OwnerDeviceState.Revoked, revoked.State);

            using var revokedList = await SendSignedAsync(
                host,
                client,
                firstKey,
                firstDevice.DeviceId,
                "/api/v1/owner/devices/list",
                listAction,
                OwnerHttpBinding.DeviceListPayload());
            var devicesAfterRevoke = await revokedList.Content.ReadFromJsonAsync<OwnerDevice[]>();

            Assert.Equal(HttpStatusCode.OK, revokedList.StatusCode);
            Assert.NotNull(devicesAfterRevoke);
            Assert.Equal(2, devicesAfterRevoke.Length);
            Assert.Equal(
                OwnerDeviceState.Active,
                Assert.Single(devicesAfterRevoke, device => device.DeviceId == firstDevice.DeviceId).State);
            var listedRevokedDevice = Assert.Single(devicesAfterRevoke, device => device.DeviceId == secondDevice.DeviceId);
            Assert.Equal(OwnerDeviceState.Revoked, listedRevokedDevice.State);
            Assert.NotNull(listedRevokedDevice.RevokedAtUtc);

            var authority = host.Services.GetRequiredService<OwnerAuthorityStore>();
            const string rejectedRequestId = "revoked-device-challenge";
            var issueProof = OwnerAuthorityStore.CreateChallengeIssueCanonicalProof(
                authority.Identity,
                secondDevice.DeviceId,
                rejectedRequestId);
            using var rejectedChallenge = await client.PostAsJsonAsync(
                "/api/v1/owner/challenges",
                new IssueOwnerChallengeHttpRequest(
                    secondDevice.DeviceId,
                    rejectedRequestId,
                    issueProof,
                    Sign(secondKey, issueProof)));

            Assert.Equal(HttpStatusCode.Forbidden, rejectedChallenge.StatusCode);
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
    public async Task PairedOwnerReconnectAndPausedWorldSurviveAHostRestart()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-viewer-restart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OwnerDevice pairedDevice;
            using (var firstHost = new ViewerWebApplicationFactory(directory))
            using (var firstClient = firstHost.CreateClient())
            {
                pairedDevice = await StartAndActivateAsync(firstHost, firstClient, key);
                var firstRuntime = firstHost.Services.GetRequiredService<OwnerWorldRuntime>();
                Assert.True(firstRuntime.Pause($"owner-device:{pairedDevice.DeviceId}"));
                firstHost.Services.GetRequiredService<OwnerWorldStateFile>().Save(firstRuntime);
            }

            using var restartedHost = new ViewerWebApplicationFactory(directory);
            using var restartedClient = restartedHost.CreateClient();
            using var reconnect = await SendSignedAsync(
                restartedHost,
                restartedClient,
                key,
                pairedDevice.DeviceId,
                "/api/v1/owner/reconnect",
                new OwnerReconnectAction(0),
                OwnerHttpBinding.ReconnectPayload(new OwnerReconnectAction(0)));
            var baseline = await reconnect.Content.ReadFromJsonAsync<ViewerOwnerReconnect>();

            Assert.Equal(HttpStatusCode.OK, reconnect.StatusCode);
            Assert.NotNull(baseline);
            Assert.True(baseline.Baseline.Snapshot.Authoring!.IsPaused);
            Assert.Contains(
                baseline.Baseline.Events.Events,
                worldEvent => worldEvent.Kind == "paused" &&
                    worldEvent.Detail.Contains($"issuer:owner-device:{pairedDevice.DeviceId}", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private async Task<OwnerDevice> StartAndActivateAsync(HttpClient client, ECDsa key, string publicKey)
    {
        using var start = await client.PostAsJsonAsync("/api/v1/pairings", new StartOwnerPairingHttpRequest(publicKey));
        var pairing = await start.Content.ReadFromJsonAsync<OwnerPairingStart>();
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.NotNull(pairing);

        var authority = factory.Services.GetRequiredService<OwnerAuthorityStore>();
        var stateFile = factory.Services.GetRequiredService<OwnerAuthorityStateFile>();
        Assert.True(authority.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode).IsSuccess);
        stateFile.Save(authority);

        var activation = new ActivateOwnerPairingHttpRequest(
            pairing.PairingId,
            pairing.ActivationCanonicalProof,
            Sign(key, pairing.ActivationCanonicalProof));
        using var activated = await client.PostAsJsonAsync("/api/v1/pairings/activate", activation);
        var device = await activated.Content.ReadFromJsonAsync<OwnerDevice>();
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.NotNull(device);
        return device;
    }

    private static async Task<OwnerDevice> StartAndActivateAsync(
        ViewerWebApplicationFactory host,
        HttpClient client,
        ECDsa key)
    {
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var start = await client.PostAsJsonAsync("/api/v1/pairings", new StartOwnerPairingHttpRequest(publicKey));
        var pairing = await start.Content.ReadFromJsonAsync<OwnerPairingStart>();
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.NotNull(pairing);

        var authority = host.Services.GetRequiredService<OwnerAuthorityStore>();
        var stateFile = host.Services.GetRequiredService<OwnerAuthorityStateFile>();
        Assert.True(authority.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode).IsSuccess);
        stateFile.Save(authority);

        var activation = new ActivateOwnerPairingHttpRequest(
            pairing.PairingId,
            pairing.ActivationCanonicalProof,
            Sign(key, pairing.ActivationCanonicalProof));
        using var activated = await client.PostAsJsonAsync("/api/v1/pairings/activate", activation);
        var device = await activated.Content.ReadFromJsonAsync<OwnerDevice>();
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.NotNull(device);
        return device;
    }

    private async Task<HttpResponseMessage> SendSignedAsync<TAction>(
        HttpClient client,
        ECDsa key,
        string deviceId,
        string path,
        TAction action,
        string canonicalPayload)
        where TAction : class
    {
        var envelope = await CreateSignedRequestAsync(client, key, deviceId, path, action, canonicalPayload);
        return await client.PostAsJsonAsync(path, envelope);
    }

    private static async Task<HttpResponseMessage> SendSignedAsync<TAction>(
        ViewerWebApplicationFactory host,
        HttpClient client,
        ECDsa key,
        string deviceId,
        string path,
        TAction action,
        string canonicalPayload)
        where TAction : class
    {
        var envelope = await CreateSignedRequestAsync(host, client, key, deviceId, path, action, canonicalPayload);
        return await client.PostAsJsonAsync(path, envelope);
    }

    private async Task<OwnerSignedHttpRequest<TAction>> CreateSignedRequestAsync<TAction>(
        HttpClient client,
        ECDsa key,
        string deviceId,
        string path,
        TAction action,
        string canonicalPayload)
        where TAction : class
    {
        var authority = factory.Services.GetRequiredService<OwnerAuthorityStore>();
        var requestId = $"request-{Guid.NewGuid():N}";
        var issueProof = OwnerAuthorityStore.CreateChallengeIssueCanonicalProof(authority.Identity, deviceId, requestId);
        var challengeRequest = new IssueOwnerChallengeHttpRequest(
            deviceId,
            requestId,
            issueProof,
            Sign(key, issueProof));
        using var issued = await client.PostAsJsonAsync("/api/v1/owner/challenges", challengeRequest);
        var challenge = await issued.Content.ReadFromJsonAsync<OwnerChallenge>();
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.NotNull(challenge);

        var binding = OwnerHttpBinding.Create("POST", path, requestId, canonicalPayload);
        var consumeProof = OwnerAuthorityStore.CreateChallengeConsumeCanonicalProof(
            authority.Identity,
            deviceId,
            challenge.ChallengeId,
            challenge.Nonce,
            binding);
        return new OwnerSignedHttpRequest<TAction>(
            deviceId,
            challenge.ChallengeId,
            challenge.Nonce,
            binding,
            consumeProof,
            Sign(key, consumeProof),
            requestId,
            action);
    }

    private static async Task<OwnerSignedHttpRequest<TAction>> CreateSignedRequestAsync<TAction>(
        ViewerWebApplicationFactory host,
        HttpClient client,
        ECDsa key,
        string deviceId,
        string path,
        TAction action,
        string canonicalPayload)
        where TAction : class
    {
        var authority = host.Services.GetRequiredService<OwnerAuthorityStore>();
        var requestId = $"request-{Guid.NewGuid():N}";
        var issueProof = OwnerAuthorityStore.CreateChallengeIssueCanonicalProof(authority.Identity, deviceId, requestId);
        var challengeRequest = new IssueOwnerChallengeHttpRequest(
            deviceId,
            requestId,
            issueProof,
            Sign(key, issueProof));
        using var issued = await client.PostAsJsonAsync("/api/v1/owner/challenges", challengeRequest);
        var challenge = await issued.Content.ReadFromJsonAsync<OwnerChallenge>();
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.NotNull(challenge);

        var binding = OwnerHttpBinding.Create("POST", path, requestId, canonicalPayload);
        var consumeProof = OwnerAuthorityStore.CreateChallengeConsumeCanonicalProof(
            authority.Identity,
            deviceId,
            challenge.ChallengeId,
            challenge.Nonce,
            binding);
        return new OwnerSignedHttpRequest<TAction>(
            deviceId,
            challenge.ChallengeId,
            challenge.Nonce,
            binding,
            consumeProof,
            Sign(key, consumeProof),
            requestId,
            action);
    }

    private static string Sign(ECDsa key, string proof) => Convert.ToBase64String(key.SignData(
        Encoding.UTF8.GetBytes(proof),
        HashAlgorithmName.SHA256,
        DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
}

public sealed class ViewerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string stateDirectory;
    private readonly bool ownsStateDirectory;
    private readonly string? approvedAssetCatalogPath;
    private readonly bool privateWorld;

    public ViewerWebApplicationFactory()
        : this(null)
    {
    }

    internal ViewerWebApplicationFactory(
        string? persistedStateDirectory,
        string? approvedAssetCatalogPath = null,
        bool privateWorld = false)
    {
        ownsStateDirectory = persistedStateDirectory is null;
        stateDirectory = persistedStateDirectory ?? System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-viewer-http-{Guid.NewGuid():N}");
        this.approvedAssetCatalogPath = approvedAssetCatalogPath;
        this.privateWorld = privateWorld;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(stateDirectory);
        // The compatibility suite uses fixture mode; selected tests opt into
        // the integrated private runtime through the same real host boundary.
        builder.UseSetting("AgentWorld:Runtime:WorldMode", privateWorld ? "private" : "fixture");
        builder.UseSetting("AgentWorld:Runtime:AdvanceScript", "false");
        builder.UseSetting("AgentWorld:Pairing:StatePath", System.IO.Path.Combine(stateDirectory, "authority.json"));
        builder.UseSetting("AgentWorld:Runtime:StatePath", System.IO.Path.Combine(stateDirectory, "runtime.json"));
        builder.UseSetting("AgentWorld:Pairing:ServerAuthorityId", "authority-http-tests");
        if (!string.IsNullOrWhiteSpace(approvedAssetCatalogPath))
        {
            builder.UseSetting("AgentWorld:Assets:CatalogPath", approvedAssetCatalogPath);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && ownsStateDirectory && Directory.Exists(stateDirectory))
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }
}
