using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.Content;

/// <summary>
/// The data-only package handoff envelope. It contains one canonical package
/// manifest and the complete provenance manifests for its declared assets. It
/// never contains asset bytes, paths, URLs, executable payloads, or host
/// handles.
/// </summary>
public sealed record ContentPackageArtifact(
    ContentPackageManifest Manifest,
    IReadOnlyList<AssetProvenanceManifest> Assets)
{
    public string ManifestDigest => ContentPackageManifestCodec.ComputeManifestDigest(Manifest);

    public bool CanExport => Assets.All(asset => asset.CanExport);

    public static ContentPackageArtifact From(
        ContentPackageManifest manifest,
        IEnumerable<NormalizedInertRasterAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(assets);
        return new(manifest, assets.Select(AssetProvenanceManifest.From).ToArray());
    }

    public void Validate() => ContentPackageArtifactCodec.Validate(this);
}

/// <summary>
/// Canonical JSON envelope for a data-only package artifact. Nested manifests
/// are encoded as canonical base64 JSON so each existing codec remains the
/// authority for its own strict wire format.
/// </summary>
public static class ContentPackageArtifactCodec
{
    public const string Schema = "clankerworld.content-package-artifact/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    public static byte[] Encode(ContentPackageArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Validate(artifact);
        return JsonSerializer.SerializeToUtf8Bytes(ToWire(artifact), JsonOptions);
    }

    public static byte[] EncodeForExport(ContentPackageArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Validate(artifact);
        if (!artifact.CanExport)
        {
            throw new InvalidOperationException(
                $"Package '{artifact.Manifest.PackageId}' contains asset provenance that does not permit export.");
        }

        return JsonSerializer.SerializeToUtf8Bytes(ToWire(artifact), JsonOptions);
    }

    public static ContentPackageArtifact Decode(ReadOnlySpan<byte> utf8Json)
    {
        WireArtifact wire;
        try
        {
            wire = JsonSerializer.Deserialize<WireArtifact>(utf8Json, JsonOptions)
                ?? throw new InvalidDataException("The content package artifact is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The content package artifact is not valid canonical JSON.", exception);
        }

        if (!string.Equals(wire.Schema, Schema, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(wire.ManifestBase64) ||
            wire.AssetManifestsBase64 is null)
        {
            throw new InvalidDataException("The content package artifact schema or collections are invalid.");
        }

        var manifest = DecodeManifest(wire.ManifestBase64);
        var assets = wire.AssetManifestsBase64
            .Select(DecodeAsset)
            .ToArray();
        var artifact = new ContentPackageArtifact(manifest, assets);
        Validate(artifact);
        var canonical = Encode(artifact);
        if (!utf8Json.SequenceEqual(canonical))
        {
            throw new InvalidDataException("The content package artifact is not in canonical byte form.");
        }

        return artifact;
    }

    public static string ComputeArtifactDigest(ContentPackageArtifact artifact) =>
        AssetRules.Sha256(Encode(artifact));

    internal static void Validate(ContentPackageArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact.Manifest);
        ArgumentNullException.ThrowIfNull(artifact.Assets);
        artifact.Manifest.Validate();

        var forbiddenCapabilities = artifact.Manifest.DeclaredCapabilities
            .Where(capability => capability is "execute" or "network" or "filesystem" or "host_integration")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (forbiddenCapabilities.Length > 0)
        {
            throw new InvalidOperationException(
                $"Package '{artifact.Manifest.PackageId}' requests disabled capabilities: {string.Join(", ", forbiddenCapabilities)}.");
        }

        var reservations = (artifact.Manifest.AssetReservations ?? [])
            .ToDictionary(item => item.AssetId, StringComparer.Ordinal);
        var seenAssetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in artifact.Assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            asset.Validate();
            if (!seenAssetIds.Add(asset.AssetId))
            {
                throw new InvalidDataException($"The package artifact repeats asset '{asset.AssetId}'.");
            }

            if (!asset.AssetId.StartsWith($"{artifact.Manifest.PackageDigest}/asset/", StringComparison.Ordinal) ||
                !reservations.TryGetValue(asset.AssetId, out var reservation))
            {
                throw new InvalidDataException(
                    $"Asset '{asset.AssetId}' is not declared by package '{artifact.Manifest.PackageId}'.");
            }

            if (!string.Equals(asset.NormalizedDigest, reservation.NormalizedDigest, StringComparison.Ordinal) ||
                asset.DurableStorageBytes != reservation.DurableStorageBytes ||
                asset.DecodedBytes != reservation.DecodedCacheBytes ||
                asset.DecodedBytes != reservation.GpuBytes)
            {
                throw new InvalidDataException(
                    $"Asset '{asset.AssetId}' provenance does not match its package reservation.");
            }
        }

        if (seenAssetIds.Count != reservations.Count)
        {
            throw new InvalidDataException(
                $"Package '{artifact.Manifest.PackageId}' is missing one or more declared asset provenance manifests.");
        }
    }

    private static WireArtifact ToWire(ContentPackageArtifact artifact) => new(
        Schema,
        Convert.ToBase64String(ContentPackageManifestCodec.Encode(artifact.Manifest)),
        artifact.Assets
            .OrderBy(asset => asset.AssetId, StringComparer.Ordinal)
            .Select(asset => Convert.ToBase64String(AssetProvenanceManifestCodec.Encode(asset)))
            .ToArray());

    private static ContentPackageManifest DecodeManifest(string base64)
    {
        try
        {
            return ContentPackageManifestCodec.Decode(Convert.FromBase64String(base64));
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The package artifact manifest is not valid base64.", exception);
        }
    }

    private static AssetProvenanceManifest DecodeAsset(string base64)
    {
        try
        {
            return AssetProvenanceManifestCodec.Decode(Convert.FromBase64String(base64));
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The package artifact asset manifest is not valid base64.", exception);
        }
    }

    private sealed record WireArtifact(
        string Schema,
        string ManifestBase64,
        IReadOnlyList<string>? AssetManifestsBase64);
}
