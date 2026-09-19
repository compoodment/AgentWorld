using System.Security.Cryptography;
using System.Text;
using AgentWorld.Viewer.Control;

namespace AgentWorld.Simulation.Tests;

public sealed class OwnerAuthorityStateFileTests
{
    [Fact]
    public void PairingAndDeviceStateSurviveAHostRestartWithoutPersistingTheVisibleCode()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-owner-authority-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = System.IO.Path.Combine(directory, "owner-authority.json");
            var identity = new OwnerAuthorityIdentity("authority-test-file", "world-test-file");
            var clock = new MutableClock(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
            var stateFile = new OwnerAuthorityStateFile(path);
            using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var first = stateFile.LoadOrCreate(identity, clock, CryptographicOwnerAuthorityRandom.Instance);
            var pairing = first.StartPairing(new OwnerPairingRequest(
                Convert.ToBase64String(deviceKey.ExportSubjectPublicKeyInfo()))).Value!;
            stateFile.Save(first);

            var persistedText = File.ReadAllText(path);
            Assert.DoesNotContain(pairing.PairingCode, persistedText, StringComparison.Ordinal);

            var restarted = stateFile.LoadOrCreate(identity, clock, CryptographicOwnerAuthorityRandom.Instance);
            var approval = restarted.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode);
            Assert.True(approval.IsSuccess);
            stateFile.Save(restarted);

            var activatedAfterRestart = stateFile.LoadOrCreate(identity, clock, CryptographicOwnerAuthorityRandom.Instance);
            var activationProof = OwnerAuthorityStore.CreatePairingActivationCanonicalProof(
                identity,
                pairing.PairingId,
                pairing.DeviceId,
                pairing.PublicKeyFingerprint);
            var activated = activatedAfterRestart.ActivatePairing(new OwnerPairingActivationRequest(
                pairing.PairingId,
                activationProof,
                Sign(deviceKey, activationProof)));
            Assert.True(activated.IsSuccess);
            stateFile.Save(activatedAfterRestart);

            var finalRestart = stateFile.LoadOrCreate(identity, clock, CryptographicOwnerAuthorityRandom.Instance);
            var device = finalRestart.GetDevice(pairing.DeviceId);
            Assert.True(device.IsSuccess);
            Assert.Equal(OwnerDeviceState.Active, device.Value!.State);
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
    public void StateFileRejectsAnActiveDeviceThatDidNotComeFromAnActivePairing()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-owner-authority-tampered-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = System.IO.Path.Combine(directory, "owner-authority.json");
            var identity = new OwnerAuthorityIdentity("authority-test-file", "world-test-file");
            using var injectedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(injectedKey.ExportSubjectPublicKeyInfo());
            var injectedDevice = new OwnerStoredDevice(
                "device_injected",
                publicKey,
                OwnerAuthorityStore.GetPublicKeyFingerprint(publicKey),
                OwnerDeviceState.Active,
                new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
                null);
            var tampered = new OwnerAuthorityState(1, identity, [], [injectedDevice], []);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(tampered));

            var stateFile = new OwnerAuthorityStateFile(path);
            var exception = Assert.Throws<InvalidDataException>(() => stateFile.LoadOrCreate(identity));

            Assert.Contains("inconsistent", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public void StateFileRejectsAStateDocumentFromAnotherServerAuthority()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-owner-authority-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = System.IO.Path.Combine(directory, "owner-authority.json");
            var originalIdentity = new OwnerAuthorityIdentity("authority-original", "world-test-file");
            var stateFile = new OwnerAuthorityStateFile(path);
            stateFile.Save(new OwnerAuthorityStore(
                originalIdentity,
                SystemOwnerAuthorityClock.Instance,
                CryptographicOwnerAuthorityRandom.Instance));

            var exception = Assert.Throws<InvalidDataException>(() => stateFile.LoadOrCreate(
                new OwnerAuthorityIdentity("authority-replacement", originalIdentity.WorldId)));

            Assert.Contains("another server authority or world", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string Sign(ECDsa key, string canonicalProof) => Convert.ToBase64String(
        key.SignData(
            Encoding.UTF8.GetBytes(canonicalProof),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    private sealed class MutableClock(DateTimeOffset utcNow) : IOwnerAuthorityClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
