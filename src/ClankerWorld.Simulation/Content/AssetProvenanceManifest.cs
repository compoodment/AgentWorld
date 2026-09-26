using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.Content;

/// <summary>
/// Portable provenance metadata for one normalized inert asset. The manifest
/// contains identity, rights, preview, and bounded raster metadata, but never
/// the PNG bytes, a path, a URL, a process handle, or executable content.
/// </summary>
public sealed record AssetProvenanceManifest(
    string AssetId,
    string DisplayName,
    string OriginalDigest,
    string NormalizedDigest,
    AssetProvenance Provenance,
    AssetPreviewReference Preview,
    AssetPngMetadata Png,
    string CanonicalMetadata,
    long CandidateBytes,
    long DecodedBytes,
    long DurableStorageBytes)
{
    public const string Schema = "agentworld.asset-provenance-manifest/v1";

    public bool CanExport => Provenance.Rights.ExportAllowed;

    public static AssetProvenanceManifest From(NormalizedInertRasterAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var manifest = new AssetProvenanceManifest(
            asset.AssetId,
            asset.DisplayName,
            asset.OriginalDigest,
            asset.NormalizedDigest,
            asset.Provenance,
            asset.Preview,
            asset.Png,
            asset.CanonicalMetadata,
            asset.CandidateBytes,
            asset.DecodedBytes,
            asset.DurableStorageBytes);
        manifest.Validate();
        return manifest;
    }

    public NormalizedInertRasterAsset ToNormalizedAsset()
    {
        Validate();
        return new NormalizedInertRasterAsset(
            AssetId,
            DisplayName,
            OriginalDigest,
            NormalizedDigest,
            Provenance,
            Preview,
            Png,
            CanonicalMetadata,
            CandidateBytes,
            DecodedBytes,
            DurableStorageBytes);
    }

    public void Validate() => AssetProvenanceManifestCodec.Validate(this);
}

/// <summary>
/// Canonical strict JSON for data-only asset provenance manifests. Collection
/// order is normalized, unknown JSON members are rejected, and decoding only
/// returns after the input bytes exactly match the canonical encoding.
/// </summary>
public static class AssetProvenanceManifestCodec
{
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

    public static byte[] Encode(AssetProvenanceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(ToWire(manifest), JsonOptions);
    }

    public static AssetProvenanceManifest Decode(ReadOnlySpan<byte> utf8Json)
    {
        WireManifest wire;
        try
        {
            wire = JsonSerializer.Deserialize<WireManifest>(utf8Json, JsonOptions)
                ?? throw new InvalidDataException("The asset provenance manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The asset provenance manifest is not valid canonical JSON.", exception);
        }

        if (!string.Equals(wire.Schema, AssetProvenanceManifest.Schema, StringComparison.Ordinal) ||
            wire.Provenance is null ||
            wire.Provenance.Rights is null ||
            wire.Provenance.UpstreamAssetIds is null ||
            wire.Preview is null ||
            wire.Png is null ||
            wire.Png.FrameDurationsMilliseconds is null)
        {
            throw new InvalidDataException("The asset provenance manifest schema or collections are invalid.");
        }

        var manifest = FromWire(wire);
        Validate(manifest);
        var canonical = Encode(manifest);
        if (!utf8Json.SequenceEqual(canonical))
        {
            throw new InvalidDataException("The asset provenance manifest is not in canonical byte form.");
        }

        return manifest;
    }

    public static string ComputeManifestDigest(AssetProvenanceManifest manifest) =>
        AssetRules.Sha256(Encode(manifest));

    internal static void Validate(AssetProvenanceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        try
        {
            AssetRules.ValidateCanonicalAssetId(manifest.AssetId);
            ContentPackageRules.ValidateDigest(manifest.OriginalDigest, nameof(manifest.OriginalDigest));
            ContentPackageRules.ValidateDigest(manifest.NormalizedDigest, nameof(manifest.NormalizedDigest));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The asset provenance manifest has an invalid identity.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The asset provenance manifest has an invalid identity.", exception);
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName) ||
            manifest.DisplayName != manifest.DisplayName.Trim() ||
            manifest.DisplayName.Any(char.IsControl))
        {
            throw new InvalidDataException("Asset provenance display names must be trimmed, non-empty, and printable.");
        }

        AssetNormalizer.ValidateProvenanceForManifest(manifest.Provenance);
        AssetNormalizer.ValidatePreviewForManifest(manifest.Preview);
        ValidatePng(manifest.Png);

        if (manifest.CandidateBytes <= 0 ||
            manifest.DecodedBytes <= 0 ||
            manifest.DurableStorageBytes <= 0)
        {
            throw new InvalidDataException("Asset provenance byte reservations must be positive.");
        }

        long expectedDecodedBytes;
        try
        {
            expectedDecodedBytes = manifest.Png.DecodedRgba8Bytes;
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Asset provenance decoded-byte accounting overflowed.", exception);
        }

        if (manifest.DecodedBytes != expectedDecodedBytes)
        {
            throw new InvalidDataException("Asset provenance decoded-byte accounting does not match PNG metadata.");
        }

        ValidateCanonicalMetadata(manifest);
    }

    private static WireManifest ToWire(AssetProvenanceManifest manifest) => new(
        AssetProvenanceManifest.Schema,
        manifest.AssetId,
        manifest.DisplayName,
        manifest.OriginalDigest,
        manifest.NormalizedDigest,
        new WireProvenance(
            AssetRules.CanonicalSourceKind(manifest.Provenance.SourceKind),
            manifest.Provenance.CreatorId,
            manifest.Provenance.ProposerId,
            new WireRights(
                AssetRules.CanonicalRightsStatus(manifest.Provenance.Rights.Status),
                manifest.Provenance.Rights.RightsHolder,
                manifest.Provenance.Rights.LicenseId,
                manifest.Provenance.Rights.BundledLicenseReference,
                AssetRules.CanonicalRedistributionStatus(manifest.Provenance.Rights.Redistribution),
                manifest.Provenance.Rights.AttributionNotices.Order(StringComparer.Ordinal).ToArray()),
            manifest.Provenance.UpstreamAssetIds.Order(StringComparer.Ordinal).ToArray()),
        new WirePreview(manifest.Preview.PreviewId, manifest.Preview.PreviewDigest),
        new WirePng(
            manifest.Png.Width,
            manifest.Png.Height,
            AssetRules.CanonicalColorType(manifest.Png.ColorType),
            manifest.Png.BitDepth,
            manifest.Png.HasAlpha,
            manifest.Png.IsInterlaced,
            manifest.Png.ColorProfile == AssetPngColorProfile.Srgb ? "srgb" : "unspecified",
            manifest.Png.FrameCount,
            manifest.Png.AnimationDurationMilliseconds,
            manifest.Png.AnimationSampleRateHz,
            manifest.Png.FrameDurationsMilliseconds.ToArray(),
            manifest.Png.LoopCount),
        manifest.CanonicalMetadata,
        manifest.CandidateBytes,
        manifest.DecodedBytes,
        manifest.DurableStorageBytes);

    private static AssetProvenanceManifest FromWire(WireManifest wire)
    {
        var provenance = new AssetProvenance(
            ParseSourceKind(wire.Provenance!.SourceKind),
            wire.Provenance.CreatorId,
            wire.Provenance.ProposerId,
            new AssetRightsMetadata(
                ParseRightsStatus(wire.Provenance.Rights!.Status),
                wire.Provenance.Rights.RightsHolder,
                wire.Provenance.Rights.LicenseId,
                wire.Provenance.Rights.BundledLicenseReference,
                ParseRedistributionStatus(wire.Provenance.Rights.Redistribution),
                wire.Provenance.Rights.AttributionNotices!.ToArray()),
            wire.Provenance.UpstreamAssetIds!.ToArray());
        var png = new AssetPngMetadata(
            wire.Png!.Width,
            wire.Png.Height,
            ParseColorType(wire.Png.ColorType),
            wire.Png.BitDepth,
            wire.Png.HasAlpha,
            wire.Png.IsInterlaced,
            ParseColorProfile(wire.Png.ColorProfile),
            wire.Png.FrameCount,
            wire.Png.AnimationDurationMilliseconds,
            wire.Png.AnimationSampleRateHz,
            wire.Png.FrameDurationsMilliseconds!.ToArray(),
            wire.Png.LoopCount);
        return new AssetProvenanceManifest(
            wire.AssetId,
            wire.DisplayName,
            wire.OriginalDigest,
            wire.NormalizedDigest,
            provenance,
            new AssetPreviewReference(wire.Preview!.PreviewId, wire.Preview.PreviewDigest),
            png,
            wire.CanonicalMetadata,
            wire.CandidateBytes,
            wire.DecodedBytes,
            wire.DurableStorageBytes);
    }

    private static void ValidatePng(AssetPngMetadata? png)
    {
        if (png is null ||
            png.Width <= 0 ||
            png.Height <= 0 ||
            !Enum.IsDefined(png.ColorType) ||
            !Enum.IsDefined(png.ColorProfile) ||
            png.BitDepth <= 0 ||
            png.FrameCount <= 0 ||
            png.FrameDurationsMilliseconds is null ||
            png.FrameDurationsMilliseconds.Count != png.FrameCount ||
            png.AnimationDurationMilliseconds < 0 ||
            png.AnimationSampleRateHz < 0 ||
            png.LoopCount < 0)
        {
            throw new InvalidDataException("Asset provenance PNG metadata is incomplete or invalid.");
        }

        var durationTotal = 0L;
        try
        {
            foreach (var duration in png.FrameDurationsMilliseconds)
            {
                if (duration < 0 || (png.FrameCount > 1 && duration == 0))
                {
                    throw new InvalidDataException("Asset provenance frame durations are invalid.");
                }

                durationTotal = checked(durationTotal + duration);
            }
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Asset provenance animation duration overflowed.", exception);
        }

        if (durationTotal != png.AnimationDurationMilliseconds ||
            (png.FrameCount == 1 && (png.AnimationDurationMilliseconds != 0 || png.AnimationSampleRateHz != 0)) ||
            (png.FrameCount > 1 && png.AnimationSampleRateHz <= 0) ||
            !ValidBitDepth(png.ColorType, png.BitDepth))
        {
            throw new InvalidDataException("Asset provenance animation timing does not match its frame metadata.");
        }

        var expectedAlpha = png.ColorType is AssetPngColorType.GrayscaleAlpha or AssetPngColorType.TruecolorAlpha;
        if (png.HasAlpha != expectedAlpha)
        {
            throw new InvalidDataException("Asset provenance alpha metadata does not match its PNG color type.");
        }
    }

    private static void ValidateCanonicalMetadata(AssetProvenanceManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.CanonicalMetadata))
        {
            throw new InvalidDataException("Asset provenance must retain canonical metadata JSON.");
        }

        try
        {
            using var document = JsonDocument.Parse(manifest.CanonicalMetadata);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !string.Equals(root.GetProperty("schema").GetString(), "agentworld.inert-raster.v1", StringComparison.Ordinal) ||
                !string.Equals(root.GetProperty("assetId").GetString(), manifest.AssetId, StringComparison.Ordinal) ||
                !string.Equals(root.GetProperty("originalDigest").GetString(), manifest.OriginalDigest, StringComparison.Ordinal) ||
                !string.Equals(root.GetProperty("normalizedDigest").GetString(), manifest.NormalizedDigest, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Asset provenance canonical metadata does not match its identity.");
            }

            var expected = AssetNormalizer.RebuildCanonicalMetadata(manifest);
            if (!string.Equals(manifest.CanonicalMetadata, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Asset provenance canonical metadata does not match its structured fields.");
            }
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidDataException("Asset provenance canonical metadata is missing required identity fields.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Asset provenance canonical metadata is not valid JSON.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("Asset provenance canonical metadata has invalid field types.", exception);
        }
    }

    private static bool ValidBitDepth(AssetPngColorType colorType, int bitDepth) => colorType switch
    {
        AssetPngColorType.Grayscale => bitDepth is 1 or 2 or 4 or 8 or 16,
        AssetPngColorType.Truecolor => bitDepth is 8 or 16,
        AssetPngColorType.Indexed => bitDepth is 1 or 2 or 4 or 8,
        AssetPngColorType.GrayscaleAlpha or AssetPngColorType.TruecolorAlpha => bitDepth is 8 or 16,
        _ => false,
    };

    private static AssetSourceKind ParseSourceKind(string value) => value switch
    {
        "repository" => AssetSourceKind.Repository,
        "human" => AssetSourceKind.Human,
        "generated" => AssetSourceKind.Generated,
        "transformed" => AssetSourceKind.Transformed,
        "inhabitant" => AssetSourceKind.Inhabitant,
        _ => throw new InvalidDataException("Asset provenance has an unknown source kind."),
    };

    private static AssetRightsStatus ParseRightsStatus(string value) => value switch
    {
        "unknown" => AssetRightsStatus.Unknown,
        "known" => AssetRightsStatus.Known,
        "restricted" => AssetRightsStatus.Restricted,
        _ => throw new InvalidDataException("Asset provenance has an unknown rights status."),
    };

    private static AssetRedistributionStatus ParseRedistributionStatus(string value) => value switch
    {
        "unknown" => AssetRedistributionStatus.Unknown,
        "allowed" => AssetRedistributionStatus.Allowed,
        "restricted" => AssetRedistributionStatus.Restricted,
        _ => throw new InvalidDataException("Asset provenance has an unknown redistribution status."),
    };

    private static AssetPngColorType ParseColorType(string value) => value switch
    {
        "grayscale" => AssetPngColorType.Grayscale,
        "truecolor" => AssetPngColorType.Truecolor,
        "indexed" => AssetPngColorType.Indexed,
        "grayscale-alpha" => AssetPngColorType.GrayscaleAlpha,
        "truecolor-alpha" => AssetPngColorType.TruecolorAlpha,
        _ => throw new InvalidDataException("Asset provenance has an unknown PNG color type."),
    };

    private static AssetPngColorProfile ParseColorProfile(string value) => value switch
    {
        "unspecified" => AssetPngColorProfile.Unspecified,
        "srgb" => AssetPngColorProfile.Srgb,
        _ => throw new InvalidDataException("Asset provenance has an unknown PNG color profile."),
    };

    private sealed record WireManifest(
        string Schema,
        string AssetId,
        string DisplayName,
        string OriginalDigest,
        string NormalizedDigest,
        WireProvenance? Provenance,
        WirePreview? Preview,
        WirePng? Png,
        string CanonicalMetadata,
        long CandidateBytes,
        long DecodedBytes,
        long DurableStorageBytes);

    private sealed record WireProvenance(
        string SourceKind,
        string CreatorId,
        string ProposerId,
        WireRights? Rights,
        IReadOnlyList<string>? UpstreamAssetIds);

    private sealed record WireRights(
        string Status,
        string? RightsHolder,
        string? LicenseId,
        string? BundledLicenseReference,
        string Redistribution,
        IReadOnlyList<string>? AttributionNotices);

    private sealed record WirePreview(string PreviewId, string PreviewDigest);

    private sealed record WirePng(
        int Width,
        int Height,
        string ColorType,
        int BitDepth,
        bool HasAlpha,
        bool IsInterlaced,
        string ColorProfile,
        int FrameCount,
        int AnimationDurationMilliseconds,
        int AnimationSampleRateHz,
        IReadOnlyList<int>? FrameDurationsMilliseconds,
        int LoopCount);
}
