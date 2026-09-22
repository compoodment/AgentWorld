namespace AgentWorld.Simulation.Content;

/// <summary>
/// The result of evaluating data-only package content against an immutable
/// preview world. A failed preview returns the original base projection, never
/// a partially applied result.
/// </summary>
public sealed record ContentPreviewResult(
    bool IsValid,
    ContentResolutionResult Resolution,
    DeclarativeWorldContentState WorldContent,
    IReadOnlyList<string> Diagnostics)
{
    public string? FailureCode => Diagnostics.Count == 0
        ? null
        : Diagnostics[0].Split(':', 2)[0];

    public string Diagnostic => string.Join('\n', Diagnostics);
}

/// <summary>
/// Resolves and evaluates declarative packages without mutating a live world.
/// This is deliberately a data-only test-world boundary: it cannot invoke
/// executable content or host capabilities, and it does not retain package
/// state after returning.
/// </summary>
public static class ContentPackagePreview
{
    public static ContentPreviewResult Run(
        IEnumerable<ContentPackageManifest> availablePackages,
        IEnumerable<string> rootPackageIds,
        DeclarativeWorldContentState? baseWorldContent = null)
    {
        ArgumentNullException.ThrowIfNull(availablePackages);
        ArgumentNullException.ThrowIfNull(rootPackageIds);
        var baseContent = baseWorldContent ?? new DeclarativeWorldContentState([], []);
        baseContent.Validate();

        var packages = availablePackages.ToArray();
        var resolution = ContentPackageResolver.Resolve(packages, rootPackageIds);
        if (!resolution.IsSuccess)
        {
            return new ContentPreviewResult(false, resolution, baseContent, [
                $"{resolution.FailureCode ?? "resolution_failed"}: {resolution.Diagnostic ?? "content resolution failed."}"
            ]);
        }

        try
        {
            var projected = baseContent;
            foreach (var lockEntry in resolution.Lock)
            {
                var manifest = packages.SingleOrDefault(package =>
                    package.PackageId == lockEntry.PackageId &&
                    package.Version == lockEntry.Version &&
                    package.PackageDigest == lockEntry.PackageDigest);
                if (manifest is null)
                {
                    return Failure(
                        resolution,
                        baseContent,
                        "lock_manifest_missing: the resolved content lock references a missing manifest.");
                }

                projected = ContentDefinitionPayloadCodec.ApplyPackage(projected, manifest);
            }

            projected.Validate();
            return new ContentPreviewResult(true, resolution, projected, []);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidDataException or InvalidOperationException)
        {
            return Failure(
                resolution,
                baseContent,
                $"typed_content_invalid: {exception.Message}");
        }
    }

    private static ContentPreviewResult Failure(
        ContentResolutionResult resolution,
        DeclarativeWorldContentState baseContent,
        string diagnostic) =>
        new(false, resolution, baseContent, [diagnostic]);
}
