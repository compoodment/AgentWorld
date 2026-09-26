using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace ClankerWorld.GodotClient.Pairing;

/// <summary>
/// Identifies where an owner device signing key lives. The public key may be
/// sent to the server; the private key remains in the platform key store or
/// process memory and is never serialized by this client library.
/// </summary>
public enum OwnerDeviceKeyStorageKind
{
    WindowsCurrentUserCng,
    EphemeralContinuousIntegration,
}

/// <summary>
/// Controls device-key acquisition. On Windows the default is a durable,
/// per-user Microsoft Software Key Storage Provider key. Outside Windows an
/// in-memory key is permitted only when a recognized CI environment is active.
/// </summary>
public sealed record OwnerDeviceKeyOptions(
    string KeyName = OwnerDeviceKey.DefaultKeyName,
    bool AllowEphemeralFallbackForContinuousIntegration = true);

/// <summary>
/// A narrowly scoped signing-key abstraction for owner pairing. It exposes
/// only public-key material and proof signing; it intentionally offers no
/// private-key export, serialization, or logging API.
/// </summary>
public interface IOwnerDeviceSigner : IDisposable
{
    string PublicKeySpkiBase64 { get; }

    string PublicKeyFingerprint { get; }

    string SignCanonicalProof(string canonicalProof);
}

/// <summary>
/// Holds a P-256 device signing key. Windows uses a non-exportable current-user
/// CNG key whenever supported. Linux and other headless targets intentionally
/// have no durable fallback: an ephemeral key can be used only by an explicitly
/// allowed recognized continuous-integration process, so a production device
/// never appears paired after its private key has been discarded.
/// </summary>
public sealed class OwnerDeviceKey : IOwnerDeviceSigner
{
    /// <summary>
    /// Stable default CNG key name. CNG current-user isolation prevents this
    /// name from identifying a key belonging to another Windows user.
    /// </summary>
    // Existing Windows CNG keys use this name; changing it would orphan
    // already-paired clients after the product rename.
    public const string DefaultKeyName = "ClankerWorld.OwnerDevice.v1";

    private readonly ECDsa signingKey;

    private OwnerDeviceKey(ECDsa signingKey, OwnerDeviceKeyStorageKind storageKind)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        this.signingKey = signingKey;
        StorageKind = storageKind;
        PublicKeySpkiBase64 = OwnerPairingProtocol.ExportP256PublicKeySpkiBase64(signingKey);
        PublicKeyFingerprint = OwnerPairingProtocol.CreatePublicKeyFingerprint(PublicKeySpkiBase64);
    }

    /// <summary>
    /// Describes the storage lifetime without exposing a private-key location.
    /// </summary>
    public OwnerDeviceKeyStorageKind StorageKind { get; }

    /// <summary>
    /// Standard base64 SubjectPublicKeyInfo representation of the P-256 public
    /// key. It is non-secret and is the only key material sent during pairing.
    /// </summary>
    public string PublicKeySpkiBase64 { get; }

    /// <summary>
    /// Non-secret SHA-256 fingerprint of <see cref="PublicKeySpkiBase64"/>.
    /// </summary>
    public string PublicKeyFingerprint { get; }

    /// <summary>
    /// Opens or creates a durable current-user CNG key on Windows. If the
    /// process is running in recognized CI and the caller leaves the default
    /// fallback enabled, an ephemeral P-256 key is allowed on non-Windows
    /// platforms (or if CNG is unavailable in CI). Normal non-Windows runtime
    /// use fails closed rather than creating an unpairable durable identity.
    /// </summary>
    public static OwnerDeviceKey OpenOrCreate(OwnerDeviceKeyOptions? options = null)
    {
        options ??= new OwnerDeviceKeyOptions();
        ValidateKeyName(options.KeyName);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                return FromSigningKey(
                    OpenOrCreateWindowsCurrentUserCngKey(options.KeyName),
                    OwnerDeviceKeyStorageKind.WindowsCurrentUserCng);
            }
            catch (PlatformNotSupportedException exception)
            {
                return CreateEphemeralFallbackOrThrow(options, exception);
            }
            catch (CryptographicException exception)
            {
                return CreateEphemeralFallbackOrThrow(options, exception);
            }
        }

        return CreateEphemeralFallbackOrThrow(options, failure: null);
    }

    /// <summary>
    /// Explicit CI-only key creation for focused headless tests. This method
    /// still checks the environment; it cannot be used as a production Linux
    /// persistence substitute.
    /// </summary>
    public static OwnerDeviceKey CreateEphemeralForContinuousIntegration()
    {
        if (!IsContinuousIntegrationEnvironment())
        {
            throw new InvalidOperationException(
                "Ephemeral owner keys are restricted to recognized continuous-integration environments.");
        }

        return FromSigningKey(
            ECDsa.Create(ECCurve.NamedCurves.nistP256),
            OwnerDeviceKeyStorageKind.EphemeralContinuousIntegration);
    }

    /// <summary>
    /// Signs a server-validated canonical proof with P-256 ECDSA, SHA-256, and
    /// IEEE-P1363 fixed-field concatenation. No private key bytes leave the key
    /// provider.
    /// </summary>
    public string SignCanonicalProof(string canonicalProof) =>
        OwnerPairingProtocol.SignP256Sha256P1363(signingKey, canonicalProof);

    /// <inheritdoc />
    public void Dispose() => signingKey.Dispose();

    [SupportedOSPlatform("windows")]
    private static ECDsaCng OpenOrCreateWindowsCurrentUserCngKey(string keyName)
    {
        var provider = CngProvider.MicrosoftSoftwareKeyStorageProvider;
        CngKey cngKey;
        if (CngKey.Exists(keyName, provider, CngKeyOpenOptions.UserKey))
        {
            cngKey = CngKey.Open(keyName, provider, CngKeyOpenOptions.UserKey);
        }
        else
        {
            var creationParameters = new CngKeyCreationParameters
            {
                Provider = provider,
                KeyCreationOptions = CngKeyCreationOptions.None,
                KeyUsage = CngKeyUsages.Signing,
                ExportPolicy = CngExportPolicies.None,
            };
            cngKey = CngKey.Create(CngAlgorithm.ECDsaP256, keyName, creationParameters);
        }

        try
        {
            return new ECDsaCng(cngKey);
        }
        catch
        {
            cngKey.Dispose();
            throw;
        }
    }

    private static OwnerDeviceKey CreateEphemeralFallbackOrThrow(
        OwnerDeviceKeyOptions options,
        Exception? failure)
    {
        if (options.AllowEphemeralFallbackForContinuousIntegration && IsContinuousIntegrationEnvironment())
        {
            return CreateEphemeralForContinuousIntegration();
        }

        throw new PlatformNotSupportedException(
            "Owner device keys require the Windows current-user CNG store. " +
            "The in-memory fallback is restricted to recognized continuous-integration environments.",
            failure);
    }

    private static OwnerDeviceKey FromSigningKey(ECDsa signingKey, OwnerDeviceKeyStorageKind storageKind)
    {
        try
        {
            return new OwnerDeviceKey(signingKey, storageKind);
        }
        catch
        {
            signingKey.Dispose();
            throw;
        }
    }

    private static void ValidateKeyName(string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        if (keyName.Length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(keyName), "The CNG key name is too long.");
        }
    }

    private static bool IsContinuousIntegrationEnvironment() =>
        IsEnvironmentVariableSetToTrue("CI") ||
        IsEnvironmentVariableSetToTrue("GITHUB_ACTIONS") ||
        IsEnvironmentVariableSetToTrue("TF_BUILD") ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILD_BUILDID")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JENKINS_URL"));

    private static bool IsEnvironmentVariableSetToTrue(string name) =>
        string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);
}
