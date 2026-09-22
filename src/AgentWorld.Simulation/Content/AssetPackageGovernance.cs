using System.Globalization;

namespace AgentWorld.Simulation.Content;

/// <summary>
/// Controls whether a package may remain local-only when one of its assets is
/// not known to be redistributable. The default deliberately preserves the
/// per-asset contract: an asset can be accepted for local inspection while
/// export remains unavailable.
/// </summary>
public enum AssetPackageExportPolicy
{
    LocalOnly,
    RequireExportable,

    // Descriptive alias for callers that prefer the policy's outcome.
    ExportableOnly = RequireExportable,
}

/// <summary>
/// Aggregate acceptance ceilings for one inert asset package. Per-asset
/// ceilings are rechecked against <see cref="PerAssetPolicy"/> because a
/// normalized asset may have been produced under a different policy.
/// </summary>
public sealed record AssetPackageBudgetPolicy
{
    public const string Version = "asset-package-budget-v1";

    public AssetBudgetPolicy PerAssetPolicy { get; init; } = AssetBudgetPolicy.Default;

    public int MaxAssets { get; init; } = 512;

    public long MaxCandidateBytes { get; init; } = 64 * AssetBudgetPolicy.MiB;

    public long MaxDecodedBytes { get; init; } = 64 * AssetBudgetPolicy.MiB;

    public long MaxDurableStorageBytes { get; init; } = 64 * AssetBudgetPolicy.MiB;

    public int MaxFrames { get; init; } = 4_096;

    public int MaxWidth { get; init; } = 2_048;

    public int MaxHeight { get; init; } = 2_048;

    public AssetPackageExportPolicy ExportPolicy { get; init; } = AssetPackageExportPolicy.LocalOnly;

    public static AssetPackageBudgetPolicy Default { get; } = new();

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(PerAssetPolicy);
        PerAssetPolicy.Validate();

        if (MaxAssets <= 0 || MaxCandidateBytes <= 0 || MaxDecodedBytes <= 0 ||
            MaxDurableStorageBytes <= 0 || MaxFrames <= 0 || MaxWidth <= 0 || MaxHeight <= 0 ||
            !Enum.IsDefined(ExportPolicy))
        {
            throw new ArgumentException(
                "Package asset limits must be positive and the export policy must be defined.",
                nameof(AssetPackageBudgetPolicy));
        }
    }
}

/// <summary>
/// Counters produced in canonical package order. Duplicate assets are not
/// accepted, so every counter describes the candidates in the validation
/// collection rather than a de-duplicated storage reservation.
/// </summary>
public sealed record AssetPackageTotals(
    int AssetCount,
    int FrameCount,
    int MaximumWidth,
    int MaximumHeight,
    long CandidateBytes,
    long DecodedBytes,
    long DurableStorageBytes)
{
    public int TotalFrameCount => FrameCount;

    public long TotalCandidateBytes => CandidateBytes;

    public long TotalDecodedBytes => DecodedBytes;

    public long TotalDurableStorageBytes => DurableStorageBytes;
}

/// <summary>
/// The only preview input the simulation accepts. It is a value-only contract
/// containing identifiers, digests, and bounded image metadata. It deliberately
/// has no path, URL, stream, process, callback, or renderer handle, so creating
/// or validating it cannot perform host-side I/O.
/// </summary>
public sealed record AssetPreviewIsolationContract(
    string AssetId,
    string PreviewId,
    string PreviewDigest,
    string NormalizedDigest,
    int Width,
    int Height,
    int FrameCount,
    long DecodedBytes)
{
    public const string Version = "asset-preview-metadata-v1";

    public bool IsMetadataOnly => AssetId is not null;

    public static AssetPreviewIsolationContract From(NormalizedInertRasterAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(asset.Preview);
        ArgumentNullException.ThrowIfNull(asset.Png);

        return new AssetPreviewIsolationContract(
            asset.AssetId,
            asset.Preview.PreviewId,
            asset.Preview.PreviewDigest,
            asset.NormalizedDigest,
            asset.Png.Width,
            asset.Png.Height,
            asset.Png.FrameCount,
            asset.DecodedBytes);
    }
}

/// <summary>
/// Deterministic package-level validation output. <see cref="IsValid"/>
/// describes structural and budget acceptance; <see cref="CanExport"/> is
/// stricter and is false whenever any accepted asset has non-exportable rights.
/// </summary>
public sealed record AssetPackageValidationResult(
    bool IsValid,
    bool CanExport,
    AssetPackageTotals Totals,
    IReadOnlyList<NormalizedInertRasterAsset> Assets,
    IReadOnlyList<AssetPreviewIsolationContract> PreviewContracts,
    IReadOnlyList<AssetDiagnostic> Diagnostics)
{
    public bool ExportAllowed => CanExport;

    public string Diagnostic => string.Join('\n', Diagnostics.Select(diagnostic => diagnostic.ToString()));

    public string DiagnosticText => Diagnostic;

    public string? FailureCode => Diagnostics
        .Where(diagnostic => diagnostic.Severity == AssetDiagnosticSeverity.Error)
        .Select(diagnostic => diagnostic.Code)
        .FirstOrDefault();
}

/// <summary>
/// Validates a complete inert asset collection without reading, writing,
/// fetching, decoding, rendering, or launching anything. All collection
/// decisions are based on normalized value objects and stable metadata.
/// </summary>
public static class AssetPackageGovernance
{
    public static AssetPackageValidationResult Validate(
        IEnumerable<AssetNormalizationResult> normalizationResults,
        AssetPackageBudgetPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(normalizationResults);
        policy ??= AssetPackageBudgetPolicy.Default;
        policy.Validate();

        var diagnostics = new List<AssetDiagnostic>();
        var orderedResults = normalizationResults
            .Select(result => new OrderedNormalizationResult(result))
            .OrderBy(item => item.Asset?.AssetId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Asset?.NormalizedDigest ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Result?.InputDigest ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        var assets = new List<NormalizedInertRasterAsset>();

        foreach (var orderedResult in orderedResults)
        {
            var result = orderedResult.Result;
            if (result is null)
            {
                diagnostics.Add(Error(
                    "asset_result_missing",
                    "assets",
                    "null",
                    "normalization_result",
                    "Every package asset entry must be a normalization result."));
                continue;
            }

            if (!result.IsValid || result.Asset is null)
            {
                diagnostics.Add(Error(
                    "asset_rejected",
                    "assets",
                    result.InputDigest,
                    "valid_normalization_result",
                    "Every package asset must pass individual inert normalization before aggregate validation."));
                diagnostics.AddRange(result.Diagnostics);
                continue;
            }

            assets.Add(result.Asset);
            diagnostics.AddRange(result.Diagnostics);
        }

        return ValidateNormalizedAssets(assets, policy, diagnostics);
    }

    /// <summary>
    /// Validates already-normalized assets for callers that have retained the
    /// normalized values but not the original result wrappers.
    /// </summary>
    public static AssetPackageValidationResult ValidateAssets(
        IEnumerable<NormalizedInertRasterAsset> assets,
        AssetPackageBudgetPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        policy ??= AssetPackageBudgetPolicy.Default;
        policy.Validate();

        var results = assets.Select(asset => asset is null
            ? null!
            : new AssetNormalizationResult(true, asset.OriginalDigest, asset, []));
        return Validate(results, policy);
    }

    /// <summary>
    /// Alias with an explicit collection-oriented name for package loaders.
    /// </summary>
    public static AssetPackageValidationResult ValidateCollection(
        IEnumerable<AssetNormalizationResult> normalizationResults,
        AssetPackageBudgetPolicy? policy = null) =>
        Validate(normalizationResults, policy);

    private static AssetPackageValidationResult ValidateNormalizedAssets(
        IReadOnlyList<NormalizedInertRasterAsset> assets,
        AssetPackageBudgetPolicy policy,
        List<AssetDiagnostic> diagnostics)
    {
        var previewContracts = new List<AssetPreviewIsolationContract>();
        foreach (var asset in assets)
        {
            ValidateAssetIdentity(asset, diagnostics);
            ValidatePerAssetBudget(asset, policy.PerAssetPolicy, diagnostics);
            ValidatePreview(asset, previewContracts, diagnostics);
        }

        AddDuplicateDiagnostics(assets, diagnostics);

        var totals = SumTotals(assets, diagnostics);
        ValidatePackageBudgets(totals, policy, diagnostics);

        var exportBlocked = false;
        foreach (var asset in assets)
        {
            if (asset.CanExport)
            {
                continue;
            }

            exportBlocked = true;
            var severity = policy.ExportPolicy == AssetPackageExportPolicy.RequireExportable
                ? AssetDiagnosticSeverity.Error
                : AssetDiagnosticSeverity.Warning;
            diagnostics.Add(new AssetDiagnostic(
                severity,
                "export_rights_blocked",
                "provenance.rights",
                "The package contains an asset whose rights do not permit export.",
                asset.AssetId,
                "exportable"));
        }

        var orderedDiagnostics = OrderDiagnostics(diagnostics);
        var isValid = !orderedDiagnostics.Any(diagnostic => diagnostic.Severity == AssetDiagnosticSeverity.Error);
        var canExport = isValid && !exportBlocked;

        return new AssetPackageValidationResult(
            isValid,
            canExport,
            totals,
            assets.ToArray(),
            previewContracts.OrderBy(item => item.AssetId, StringComparer.Ordinal).ToArray(),
            orderedDiagnostics);
    }

    private static void ValidateAssetIdentity(
        NormalizedInertRasterAsset asset,
        List<AssetDiagnostic> diagnostics)
    {
        try
        {
            AssetRules.ValidateCanonicalAssetId(asset.AssetId);
        }
        catch (ArgumentException)
        {
            diagnostics.Add(Error(
                "asset_identity",
                "asset_id",
                asset.AssetId,
                "canonical_asset_id",
                "Normalized assets must retain a canonical immutable asset ID."));
        }
        catch (FormatException)
        {
            diagnostics.Add(Error(
                "asset_identity",
                "asset_id",
                asset.AssetId,
                "canonical_asset_id",
                "Normalized assets must retain a stable version in their immutable asset ID."));
        }

        try
        {
            ContentPackageRules.ValidateDigest(asset.OriginalDigest, nameof(asset.OriginalDigest));
            ContentPackageRules.ValidateDigest(asset.NormalizedDigest, nameof(asset.NormalizedDigest));
        }
        catch (ArgumentException)
        {
            diagnostics.Add(Error(
                "asset_identity",
                "digest",
                asset.AssetId,
                "sha256:<64-hex>",
                "Normalized assets must retain lowercase SHA-256 original and normalized digests."));
        }
    }

    private static void ValidatePerAssetBudget(
        NormalizedInertRasterAsset asset,
        AssetBudgetPolicy policy,
        List<AssetDiagnostic> diagnostics)
    {
        AddBudgetIfExceeded(
            diagnostics,
            "candidate_bytes",
            asset.CandidateBytes,
            policy.MaxCandidateBytes,
            "The asset exceeds the per-asset candidate-byte budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "decoded_bytes",
            asset.DecodedBytes,
            policy.MaxDecodedBytes,
            "The asset exceeds the per-asset decoded-byte budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "durable_storage",
            asset.DurableStorageBytes,
            policy.MaxDurableStorageBytes,
            "The asset exceeds the per-asset durable-storage budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "width",
            asset.Png.Width,
            policy.MaxWidth,
            "The asset width exceeds the per-asset dimension budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "height",
            asset.Png.Height,
            policy.MaxHeight,
            "The asset height exceeds the per-asset dimension budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "frame_count",
            asset.Png.FrameCount,
            policy.MaxFrames,
            "The asset frame count exceeds the per-asset animation budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "animation_duration_ms",
            asset.Png.AnimationDurationMilliseconds,
            policy.MaxAnimationDurationMilliseconds,
            "The asset animation duration exceeds the per-asset animation budget.");
        AddBudgetIfExceeded(
            diagnostics,
            "animation_sample_rate_hz",
            asset.Png.AnimationSampleRateHz,
            policy.MaxAnimationSampleRateHz,
            "The asset animation sample rate exceeds the per-asset animation budget.");
    }

    private static void ValidatePreview(
        NormalizedInertRasterAsset asset,
        List<AssetPreviewIsolationContract> contracts,
        List<AssetDiagnostic> diagnostics)
    {
        if (asset.Preview is null)
        {
            diagnostics.Add(Error(
                "preview_isolation_contract",
                "preview",
                asset.AssetId,
                AssetPreviewIsolationContract.Version,
                "A package preview must be represented by metadata-only identifiers and digests."));
            return;
        }

        var valid = true;
        try
        {
            ContentPackageRules.ValidateLocalId(asset.Preview.PreviewId);
        }
        catch (ArgumentException)
        {
            valid = false;
            diagnostics.Add(Error(
                "preview_isolation_contract",
                "preview.id",
                asset.Preview.PreviewId,
                "canonical_local_id",
                "Preview identifiers must be canonical inert local IDs."));
        }

        try
        {
            ContentPackageRules.ValidateDigest(asset.Preview.PreviewDigest, nameof(asset.Preview.PreviewDigest));
        }
        catch (ArgumentException)
        {
            valid = false;
            diagnostics.Add(Error(
                "preview_isolation_contract",
                "preview.digest",
                asset.Preview.PreviewDigest,
                "sha256:<64-hex>",
                "Preview references must use a lowercase SHA-256 digest."));
        }

        if (valid)
        {
            contracts.Add(AssetPreviewIsolationContract.From(asset));
        }
    }

    private static void AddDuplicateDiagnostics(
        IReadOnlyList<NormalizedInertRasterAsset> assets,
        List<AssetDiagnostic> diagnostics)
    {
        foreach (var duplicate in assets
                     .GroupBy(asset => asset.AssetId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "duplicate_asset_id",
                "asset_id",
                duplicate.Key,
                "unique",
                "Canonical asset IDs must be unique within a package."));
        }

        foreach (var duplicate in assets
                     .GroupBy(asset => asset.NormalizedDigest, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "duplicate_asset_digest",
                "normalized_digest",
                duplicate.Key,
                "unique",
                "Normalized asset digests must be unique within a package."));
        }
    }

    private static AssetPackageTotals SumTotals(
        IReadOnlyList<NormalizedInertRasterAsset> assets,
        List<AssetDiagnostic> diagnostics)
    {
        long candidateBytes = 0;
        long decodedBytes = 0;
        long durableStorageBytes = 0;
        var frameCount = 0;
        var maximumWidth = 0;
        var maximumHeight = 0;

        foreach (var asset in assets)
        {
            try
            {
                candidateBytes = checked(candidateBytes + asset.CandidateBytes);
                decodedBytes = checked(decodedBytes + asset.DecodedBytes);
                durableStorageBytes = checked(durableStorageBytes + asset.DurableStorageBytes);
                frameCount = checked(frameCount + asset.Png.FrameCount);
            }
            catch (OverflowException)
            {
                diagnostics.Add(Error(
                    "package_budget_breach",
                    "aggregate_bytes",
                    asset.AssetId,
                    long.MaxValue.ToString(CultureInfo.InvariantCulture),
                    "Package asset counters overflow the supported deterministic integer range."));
                candidateBytes = long.MaxValue;
                decodedBytes = long.MaxValue;
                durableStorageBytes = long.MaxValue;
                frameCount = int.MaxValue;
            }

            maximumWidth = Math.Max(maximumWidth, asset.Png.Width);
            maximumHeight = Math.Max(maximumHeight, asset.Png.Height);
        }

        return new AssetPackageTotals(
            assets.Count,
            frameCount,
            maximumWidth,
            maximumHeight,
            candidateBytes,
            decodedBytes,
            durableStorageBytes);
    }

    private static void ValidatePackageBudgets(
        AssetPackageTotals totals,
        AssetPackageBudgetPolicy policy,
        List<AssetDiagnostic> diagnostics)
    {
        AddPackageBudgetIfExceeded(
            diagnostics,
            "asset_count",
            totals.AssetCount,
            policy.MaxAssets,
            "The package contains more assets than the package load budget permits.");
        AddPackageBudgetIfExceeded(
            diagnostics,
            "candidate_bytes",
            totals.CandidateBytes,
            policy.MaxCandidateBytes,
            "The package candidate-byte reservation exceeds the aggregate package budget.");
        AddPackageBudgetIfExceeded(
            diagnostics,
            "decoded_bytes",
            totals.DecodedBytes,
            policy.MaxDecodedBytes,
            "The package decoded-data reservation exceeds the aggregate package budget.");
        AddPackageBudgetIfExceeded(
            diagnostics,
            "durable_storage",
            totals.DurableStorageBytes,
            policy.MaxDurableStorageBytes,
            "The package durable-storage reservation exceeds the aggregate package budget.");
        AddPackageBudgetIfExceeded(
            diagnostics,
            "frame_count",
            totals.FrameCount,
            policy.MaxFrames,
            "The package animation frame count exceeds the aggregate package budget.");
        AddPackageBudgetIfExceeded(
            diagnostics,
            "width",
            totals.MaximumWidth,
            policy.MaxWidth,
            "An asset in the package exceeds the aggregate width limit.");
        AddPackageBudgetIfExceeded(
            diagnostics,
            "height",
            totals.MaximumHeight,
            policy.MaxHeight,
            "An asset in the package exceeds the aggregate height limit.");
    }

    private static void AddBudgetIfExceeded(
        List<AssetDiagnostic> diagnostics,
        string field,
        long observed,
        long limit,
        string message)
    {
        if (observed > limit)
        {
            diagnostics.Add(Budget("asset_budget_breach", field, observed, limit, message));
        }
    }

    private static void AddPackageBudgetIfExceeded(
        List<AssetDiagnostic> diagnostics,
        string field,
        long observed,
        long limit,
        string message)
    {
        if (observed > limit)
        {
            diagnostics.Add(Budget("package_budget_breach", field, observed, limit, message));
        }
    }

    private static AssetDiagnostic Budget(
        string code,
        string field,
        long observed,
        long limit,
        string message) =>
        Error(
            code,
            field,
            observed.ToString(CultureInfo.InvariantCulture),
            limit.ToString(CultureInfo.InvariantCulture),
            message);

    private static AssetDiagnostic Error(
        string code,
        string field,
        string observed,
        string? limit,
        string message) =>
        new(AssetDiagnosticSeverity.Error, code, field, message, observed, limit);

    private static AssetDiagnostic[] OrderDiagnostics(IEnumerable<AssetDiagnostic> diagnostics) => diagnostics
        .OrderBy(diagnostic => diagnostic.Severity)
        .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
        .ThenBy(diagnostic => diagnostic.Field, StringComparer.Ordinal)
        .ThenBy(diagnostic => diagnostic.ObservedValue, StringComparer.Ordinal)
        .ThenBy(diagnostic => diagnostic.Limit, StringComparer.Ordinal)
        .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
        .ToArray();

    private sealed record OrderedNormalizationResult(AssetNormalizationResult? Result)
    {
        public NormalizedInertRasterAsset? Asset => Result?.Asset;
    }
}
