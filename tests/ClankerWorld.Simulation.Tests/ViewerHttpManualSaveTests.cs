using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Control;
using ClankerWorld.Viewer.Observation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class ViewerHttpTests
{
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
                Assert.False(host.Services.GetRequiredService<ManualWorldSaveStore>().Read(backupId).JevEnabled);
                Assert.Contains(providers.CaptureRuntimeConfiguration().Assignments ?? [],
                    item => item.InhabitantId == "founder:checkpoint" && item.CredentialSlotId == slotId);
                Assert.Contains(saveLog.Messages, message => message.Contains("manual_save outcome=created", StringComparison.Ordinal));
                Assert.Contains(saveLog.Messages, message => message.Contains("manual_save outcome=loaded", StringComparison.Ordinal));
                Assert.DoesNotContain(saveLog.Messages, message => message.Contains("test-secret-key", StringComparison.Ordinal));
            }

            using var restarted = new ViewerWebApplicationFactory(directory.FullName, null, privateWorld: true,
                legacyPrivateWorld: false);
            using var restartedClient = restarted.CreateClient();
            Assert.True(restarted.Services.GetRequiredService<PrivateWorldRuntime>().JevEnabled);
            var saves = restarted.Services.GetRequiredService<ManualWorldSaveStore>().List();
            Assert.Contains(saves, item => item.Id == saveId);
            Assert.Contains(saves, item => item.Id == backupId);
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
}
