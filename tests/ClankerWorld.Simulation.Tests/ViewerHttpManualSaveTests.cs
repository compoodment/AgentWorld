using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;
using ClankerWorld.Viewer.Control;
using ClankerWorld.Viewer.Observation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class ViewerHttpTests
{
    [Fact]
    public async Task SignedWorldCreationAndSelectionKeepTwoIndependentPausedWorldsAcrossRestart()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-world-catalog-");
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string firstId;
            string generatedId;
            string deviceId;
            using (var host = new ViewerWebApplicationFactory(directory.FullName, null,
                       privateWorld: true, legacyPrivateWorld: false))
            using (var client = host.CreateClient())
            {
                var selectionLog = new RecordingLogger<WorldSelectionCoordinator>();
                host.Services.GetRequiredService<ILoggerFactory>().AddProvider(
                    new RecordingLoggerProvider<WorldSelectionCoordinator>(selectionLog));
                var device = await StartAndActivateAsync(host, client, key);
                deviceId = device.DeviceId;
                var listAction = new OwnerControlAction("list-worlds");
                using var listed = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/worlds/list", listAction,
                    OwnerHttpBinding.EmptyPayload("list-worlds"));
                Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
                var initial = (await listed.Content.ReadFromJsonAsync<WorldCatalogSnapshot>())!;
                firstId = initial.ActiveId;
                Assert.Single(initial.Worlds);
                var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
                var credentialSlotId = Guid.NewGuid().ToString("N");
                providers.Configure(new OwnerProviderConfigurationAction("personal", "openai", "gpt-5-mini",
                    "test-secret-key", false, "founder:checkpoint", credentialSlotId, "Test account"));
                host.Services.GetRequiredService<WorldAutosaveStore>().Configure(false, 1, 0);

                var create = new OwnerWorldCreationAction("Riverland", "riverland-test-seed",
                    "Small", 45, true);
                const string createPath = "/api/v1/owner/worlds/create";
                var signed = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                    createPath, create, OwnerHttpBinding.WorldCreationPayload(create));
                using var tampered = await client.PostAsJsonAsync(createPath, signed with
                {
                    Action = create with { Seed = "different-seed" },
                });
                Assert.False(tampered.IsSuccessStatusCode);
                using var created = await SendSignedAsync(host, client, key, device.DeviceId,
                    createPath, create, OwnerHttpBinding.WorldCreationPayload(create));
                Assert.Equal(HttpStatusCode.OK, created.StatusCode);
                var entry = (await created.Content.ReadFromJsonAsync<CatalogWorld>())!;
                generatedId = entry.Id;
                var runtime = host.Services.GetRequiredService<PrivateWorldRuntime>();
                Assert.True(runtime.Society.IsPaused);
                Assert.Empty(runtime.Inhabitants);
                Assert.Equal(WorldSizePreset.Small, runtime.ExportState().Geography?.Size);
                Assert.Equal(256, runtime.ExportState().Map.Width);
                var reconnect = new OwnerReconnectAction(0);
                using var observed = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/reconnect", reconnect,
                    OwnerHttpBinding.ReconnectPayload(reconnect));
                Assert.Equal(HttpStatusCode.OK, observed.StatusCode);
                var view = (await observed.Content.ReadFromJsonAsync<ViewerOwnerReconnect>())!;
                Assert.Equal(entry.WorldId, view.Baseline.Snapshot.WorldId);
                Assert.Equal(256, view.Baseline.Snapshot.PackedTerrain?.Width);
                Assert.Empty(view.Baseline.Snapshot.Tiles);
                Assert.Empty(providers.CaptureRuntimeConfiguration().Assignments ?? []);
                Assert.Equal(5, host.Services.GetRequiredService<WorldAutosaveStore>().Capture().IntervalMinutes);
                Assert.DoesNotContain("test-secret-key", File.ReadAllText(Path.Combine(
                    host.Services.GetRequiredService<PrivateWorldStateFile>().Path + ".worlds", "catalog.json")));
                Assert.Contains(selectionLog.Messages, message => message.Contains(
                    "world_selection outcome=created", StringComparison.Ordinal));
                Assert.DoesNotContain(selectionLog.Messages, message => message.Contains(
                    "test-secret-key", StringComparison.Ordinal));

                var select = new OwnerManualSaveAction("select-world", firstId);
                using var selected = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/worlds/select", select,
                    OwnerHttpBinding.ManualSavePayload(select));
                Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
                Assert.Null(runtime.ExportState().Geography);
                Assert.True(runtime.Society.IsPaused);
                Assert.Contains(providers.CaptureRuntimeConfiguration().Assignments ?? [],
                    assignment => assignment.CredentialSlotId == credentialSlotId);
                Assert.False(host.Services.GetRequiredService<WorldAutosaveStore>().Capture().Enabled);
                var returnToGenerated = new OwnerManualSaveAction("select-world", generatedId);
                using var returned = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/worlds/select", returnToGenerated,
                    OwnerHttpBinding.ManualSavePayload(returnToGenerated));
                Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            }

            using var restarted = new ViewerWebApplicationFactory(directory.FullName, null,
                privateWorld: true, legacyPrivateWorld: false);
            using var restartedClient = restarted.CreateClient();
            var listAfterRestart = new OwnerControlAction("list-worlds");
            using var signedListAfterRestart = await SendSignedAsync(restarted, restartedClient,
                key, deviceId, "/api/v1/owner/worlds/list", listAfterRestart,
                OwnerHttpBinding.EmptyPayload("list-worlds"));
            Assert.Equal(HttpStatusCode.OK, signedListAfterRestart.StatusCode);
            var restoredCatalog = restarted.Services.GetRequiredService<WorldCatalogStore>().Capture();
            Assert.Equal(generatedId, restoredCatalog.ActiveId);
            Assert.Equal(2, restoredCatalog.Worlds.Count);
            Assert.Contains(restoredCatalog.Worlds, world => world.Id == generatedId);
            var restoredRuntime = restarted.Services.GetRequiredService<PrivateWorldRuntime>();
            Assert.Equal(WorldSizePreset.Small, restoredRuntime.ExportState().Geography?.Size);
            var selectedOld = restarted.Services.GetRequiredService<WorldSelectionCoordinator>()
                .Select(firstId);
            Assert.Equal(firstId, selectedOld.Id);
            Assert.Null(restoredRuntime.ExportState().Geography);
            Assert.False(restarted.Services.GetRequiredService<WorldAutosaveStore>().Capture().Enabled);
            var restoredEntry = restarted.Services.GetRequiredService<WorldSelectionCoordinator>()
                .Select(generatedId);
            Assert.Equal("Riverland", restoredEntry.Name);
            Assert.Equal(WorldSizePreset.Small, restoredRuntime.ExportState().Geography?.Size);
            Assert.True(restoredRuntime.Society.IsPaused);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task SignedManualSaveSurvivesRestartAndLoadPreservesThePreviousTimeline()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-manual-save-");
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string saveId;
            string backupId;
            using (var host = new ViewerWebApplicationFactory(directory.FullName, null, privateWorld: true,
                       legacyPrivateWorld: false))
            using (var client = host.CreateClient())
            {
                var saveLog = new RecordingLogger<ViewerHttpTests>();
                host.Services.GetRequiredService<ILoggerFactory>().AddProvider(
                    new RecordingLoggerProvider<ViewerHttpTests>(saveLog));
                var device = await StartAndActivateAsync(host, client, key);
                var runtime = host.Services.GetRequiredService<PrivateWorldRuntime>();
                Assert.True(runtime.Society.IsPaused);
                Assert.True(runtime.JevEnabled);
                var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
                var slotId = Guid.NewGuid().ToString("N");
                providers.Configure(new OwnerProviderConfigurationAction("personal", "openai", "gpt-5-mini",
                    "test-secret-key", false, "founder:checkpoint", slotId, "Test account"));
                var create = new OwnerManualSaveAction("create", "Before changing Jev");
                const string createPath = "/api/v1/owner/saves/create";
                var envelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                    createPath, create, OwnerHttpBinding.ManualSavePayload(create));
                using var tampered = await client.PostAsJsonAsync(createPath, envelope with
                {
                    Action = create with { Value = "Different save" },
                });
                Assert.False(tampered.IsSuccessStatusCode);
                Assert.Empty(host.Services.GetRequiredService<ManualWorldSaveStore>().List());
                using var created = await SendSignedAsync(host, client, key, device.DeviceId,
                    createPath, create, OwnerHttpBinding.ManualSavePayload(create));
                Assert.Equal(HttpStatusCode.OK, created.StatusCode);
                var saved = await created.Content.ReadFromJsonAsync<ManualWorldSave>();
                Assert.NotNull(saved);
                saveId = saved.Id;
                Assert.DoesNotContain("test-secret-key", File.ReadAllText(Path.Combine(
                    host.Services.GetRequiredService<PrivateWorldStateFile>().Path + ".manual", saveId + ".meta.json")));
                Assert.DoesNotContain("test-secret-key", File.ReadAllText(Path.Combine(
                    host.Services.GetRequiredService<PrivateWorldStateFile>().Path + ".manual", saveId + ".save")));

                var change = new OwnerJevAssistanceAction(false);
                using var changed = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/control/jev-assistance", change,
                    OwnerHttpBinding.JevAssistancePayload(change));
                Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
                Assert.False(runtime.JevEnabled);
                providers.Configure(new OwnerProviderConfigurationAction("personal", "inherit", null,
                    null, false, "founder:checkpoint"));
                Assert.Empty(providers.CaptureRuntimeConfiguration().Assignments ?? []);
                host.Services.GetRequiredService<WorldAutosaveStore>().Configure(false, 1, 0);

                var list = new OwnerControlAction("list-saves");
                using var listed = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/saves/list", list, OwnerHttpBinding.EmptyPayload("list-saves"));
                Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
                Assert.Contains((await listed.Content.ReadFromJsonAsync<ManualWorldSave[]>())!,
                    item => item.Id == saveId && item.Name == "Before changing Jev");

                var load = new OwnerManualSaveAction("load", saveId);
                using var loaded = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/saves/load", load, OwnerHttpBinding.ManualSavePayload(load));
                Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
                var receipt = await loaded.Content.ReadFromJsonAsync<ManualSaveLoadReceiptForTest>();
                Assert.NotNull(receipt);
                backupId = receipt.BackupId;
                Assert.True(runtime.JevEnabled);
                Assert.True(runtime.Society.IsPaused);
                Assert.True(host.Services.GetRequiredService<WorldAutosaveStore>().Capture().Enabled);
                Assert.Equal(5, host.Services.GetRequiredService<WorldAutosaveStore>().Capture().IntervalMinutes);
                Assert.False(host.Services.GetRequiredService<ManualWorldSaveStore>().Read(backupId).JevEnabled);
                Assert.Contains(providers.CaptureRuntimeConfiguration().Assignments ?? [],
                    item => item.InhabitantId == "founder:checkpoint" && item.CredentialSlotId == slotId);
                var statusAction = new OwnerControlAction("autosave-status");
                using var status = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/saves/autosave/status", statusAction,
                    OwnerHttpBinding.EmptyPayload("autosave-status"));
                Assert.Equal(HttpStatusCode.OK, status.StatusCode);
                Assert.Equal(5, (await status.Content.ReadFromJsonAsync<WorldAutosaveSettings>())!.IntervalMinutes);
                var autosaveAction = new OwnerAutosaveConfigurationAction(true, 1, 0);
                const string autosavePath = "/api/v1/owner/saves/autosave/configure";
                var autosaveEnvelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                    autosavePath, autosaveAction, OwnerHttpBinding.AutosaveConfigurationPayload(autosaveAction));
                using var tamperedAutosave = await client.PostAsJsonAsync(autosavePath, autosaveEnvelope with
                {
                    Action = autosaveAction with { Enabled = false },
                });
                Assert.False(tamperedAutosave.IsSuccessStatusCode);
                using var configured = await SendSignedAsync(host, client, key, device.DeviceId,
                    autosavePath, autosaveAction, OwnerHttpBinding.AutosaveConfigurationPayload(autosaveAction));
                Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
                Assert.Equal(0, host.Services.GetRequiredService<WorldAutosaveStore>().Capture().RotationCount);
                Assert.Contains(saveLog.Messages, message => message.Contains("manual_save outcome=created", StringComparison.Ordinal));
                Assert.Contains(saveLog.Messages, message => message.Contains("manual_save outcome=loaded", StringComparison.Ordinal));
                Assert.Contains(saveLog.Messages, message => message.Contains("autosave_settings outcome=changed", StringComparison.Ordinal));
                Assert.DoesNotContain(saveLog.Messages, message => message.Contains("test-secret-key", StringComparison.Ordinal));
            }

            using var restarted = new ViewerWebApplicationFactory(directory.FullName, null, privateWorld: true,
                legacyPrivateWorld: false);
            using var restartedClient = restarted.CreateClient();
            Assert.True(restarted.Services.GetRequiredService<PrivateWorldRuntime>().JevEnabled);
            var saves = restarted.Services.GetRequiredService<ManualWorldSaveStore>().List();
            Assert.Contains(saves, item => item.Id == saveId);
            Assert.Contains(saves, item => item.Id == backupId);
            Assert.Equal(1, restarted.Services.GetRequiredService<WorldAutosaveStore>().Capture().IntervalMinutes);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed record ManualSaveLoadReceiptForTest(string LoadedId, string BackupId, long WorldTick);

    [Fact]
    public void LoadingAnEarlierFounderCheckpointRestoresTheSetupGate()
    {
        using var runtime = new PrivateWorldRuntime("founder-save-rewind", startPace: WorldStartPace.FounderSetup);
        var emptyCamp = runtime.ExportState();
        runtime.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new(0, 0));
        Assert.Single(runtime.FounderSetup!.FounderIds);

        runtime.LoadPausedCheckpoint(emptyCamp);

        Assert.Empty(runtime.FounderSetup!.FounderIds);
        Assert.Empty(runtime.Inhabitants);
        Assert.Throws<InvalidOperationException>(runtime.Resume);
    }

    [Fact]
    public async Task AutosaveScheduleRotatesOnlyAutomaticSnapshotsAndPersistsOwnerChoices()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-autosave-");
        try
        {
            using var runtime = new PrivateWorldRuntime("autosave-rotation");
            var path = Path.Combine(directory.FullName, "world.json");
            var saves = new ManualWorldSaveStore(path);
            var autosave = new WorldAutosaveStore(path, runtime.Society.WorldId);
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new ProviderConfigurationSeed("deterministic", null, null, null, null, null, null));
            Assert.True(autosave.Capture().Enabled);
            Assert.Equal(5, autosave.Capture().IntervalMinutes);
            Assert.Equal(5, autosave.Capture().RotationCount);
            runtime.Pause();
            var manual = saves.Create("Keep me", runtime, []);
            runtime.Resume();

            var firstTick = await runtime.AdvanceOneTickAsync();
            Assert.True(firstTick.Advanced);
            var start = DateTimeOffset.UtcNow.AddMinutes(6);
            Assert.NotNull(autosave.MaybeSave(start, runtime, providers, saves));
            Assert.Null(autosave.MaybeSave(start.AddMinutes(6), runtime, providers, saves));
            Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
            Assert.NotNull(autosave.MaybeSave(start.AddMinutes(12), runtime, providers, saves));
            Assert.Equal(2, saves.List().Count(item => item.IsAutosave));

            autosave.Configure(true, 1, 0);
            Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
            Assert.NotNull(autosave.MaybeSave(start.AddMinutes(24), runtime, providers, saves));
            Assert.Single(saves.List(), item => item.IsAutosave);
            Assert.Contains(saves.List(), item => item.Id == manual.Id && !item.IsAutosave);

            autosave.Configure(false, 1, 0);
            Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
            Assert.Null(autosave.MaybeSave(start.AddMinutes(36), runtime, providers, saves));
            var reloaded = new WorldAutosaveStore(path, runtime.Society.WorldId);
            Assert.False(reloaded.Capture().Enabled);
            Assert.Equal(0, reloaded.Capture().RotationCount);
        }
        finally { directory.Delete(recursive: true); }
    }
}
