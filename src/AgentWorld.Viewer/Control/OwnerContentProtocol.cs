using System.Globalization;
using System.Text;
using AgentWorld.Simulation.Content;

namespace AgentWorld.Viewer.Control;

/// <summary>
/// Scalar, signed transport records for the private-world data-only content
/// lifecycle. The wire format never accepts a client-supplied lock digest or
/// activation tick; those are authoritative server outputs.
/// </summary>
public sealed record OwnerContentDependencyAction(
    string PackageId,
    string MinimumVersion,
    string MaximumExclusiveVersion,
    bool Optional);

public sealed record OwnerContentDefinitionAction(
    string Kind,
    string LocalId,
    string Version,
    string DisplayName,
    string PayloadDigest,
    string? PayloadJson = null);

public sealed record OwnerContentPackageAction(
    string PackageId,
    string Version,
    string PackageDigest,
    IReadOnlyList<OwnerContentDependencyAction> Dependencies,
    IReadOnlyList<OwnerContentDefinitionAction> Definitions,
    IReadOnlyList<string> DeclaredCapabilities);

public sealed record OwnerContentPackageIdAction(string PackageId);

public sealed record OwnerContentRollbackAction(string PackageId, string Reason);

public sealed record OwnerContentPackageReceipt(
    string Operation,
    bool Applied,
    string PackageId,
    string Version,
    string PackageDigest,
    string Lifecycle,
    string? LockDigest,
    long? ValidationTick,
    long? StagedTick,
    long? ActivationTick,
    string? Failure)
{
    public static OwnerContentPackageReceipt From(
        string operation,
        ContentPackageRecord record) => new(
            operation,
            true,
            record.Manifest.PackageId,
            record.Manifest.Version.ToString(),
            record.Manifest.PackageDigest,
            record.Lifecycle.ToString().ToLowerInvariant(),
            record.LockDigest,
            record.ValidationTick,
            record.StagedTick,
            record.ActivationTick,
            null);
}

public static class OwnerContentBinding
{
    public static string ProposePayload(OwnerContentPackageAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(action.Dependencies);
        ArgumentNullException.ThrowIfNull(action.Definitions);
        ArgumentNullException.ThrowIfNull(action.DeclaredCapabilities);

        var lines = new List<string>
        {
            "agentworld.owner-content-propose.v1",
            $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}",
            $"version={EncodeRequired(action.Version, nameof(action.Version))}",
            $"package-digest={EncodeRequired(action.PackageDigest, nameof(action.PackageDigest))}",
            $"dependency-count={action.Dependencies.Count.ToString(CultureInfo.InvariantCulture)}",
            $"definition-count={action.Definitions.Count.ToString(CultureInfo.InvariantCulture)}",
            $"capability-count={action.DeclaredCapabilities.Count.ToString(CultureInfo.InvariantCulture)}",
        };

        for (var index = 0; index < action.Dependencies.Count; index++)
        {
            var dependency = action.Dependencies[index] ?? throw new ArgumentException(
                "Content dependencies cannot contain null.",
                nameof(action));
            var prefix = $"dependency-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.package-id={EncodeRequired(dependency.PackageId, nameof(dependency.PackageId))}");
            lines.Add($"{prefix}.minimum={EncodeRequired(dependency.MinimumVersion, nameof(dependency.MinimumVersion))}");
            lines.Add($"{prefix}.maximum={EncodeRequired(dependency.MaximumExclusiveVersion, nameof(dependency.MaximumExclusiveVersion))}");
            lines.Add($"{prefix}.optional={dependency.Optional.ToString().ToLowerInvariant()}");
        }

        for (var index = 0; index < action.Definitions.Count; index++)
        {
            var definition = action.Definitions[index] ?? throw new ArgumentException(
                "Content definitions cannot contain null.",
                nameof(action));
            var prefix = $"definition-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.kind={EncodeRequired(definition.Kind, nameof(definition.Kind))}");
            lines.Add($"{prefix}.local-id={EncodeRequired(definition.LocalId, nameof(definition.LocalId))}");
            lines.Add($"{prefix}.version={EncodeRequired(definition.Version, nameof(definition.Version))}");
            lines.Add($"{prefix}.display-name={EncodeRequired(definition.DisplayName, nameof(definition.DisplayName))}");
            lines.Add($"{prefix}.payload-digest={EncodeRequired(definition.PayloadDigest, nameof(definition.PayloadDigest))}");
            lines.Add($"{prefix}.payload-json={EncodeOptional(definition.PayloadJson)}");
        }

        for (var index = 0; index < action.DeclaredCapabilities.Count; index++)
        {
            lines.Add($"capability-{index.ToString(CultureInfo.InvariantCulture)}={EncodeRequired(
                action.DeclaredCapabilities[index],
                nameof(action.DeclaredCapabilities))}");
        }

        return string.Join('\n', lines);
    }

    public static string PackageIdPayload(string operation, OwnerContentPackageIdAction action) => string.Join(
        '\n',
        "agentworld.owner-content-lifecycle.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}",
        $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}");

    public static string RollbackPayload(OwnerContentRollbackAction action) => string.Join(
        '\n',
        "agentworld.owner-content-rollback.v1",
        $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}",
        $"reason={EncodeRequired(action.Reason, nameof(action.Reason))}");

    public static bool TryMapManifest(
        OwnerContentPackageAction? action,
        out ContentPackageManifest? manifest,
        out string failure)
    {
        manifest = null;
        failure = string.Empty;
        if (action is null || action.Dependencies is null || action.Definitions is null || action.DeclaredCapabilities is null)
        {
            failure = "A content package and all of its dependency, definition, and capability lists are required.";
            return false;
        }

        try
        {
            var dependencies = action.Dependencies
                .Select(dependency =>
                {
                    ArgumentNullException.ThrowIfNull(dependency);
                    return new ContentDependency(
                        dependency.PackageId,
                        new ContentVersionRange(
                            ContentVersion.Parse(dependency.MinimumVersion),
                            ContentVersion.Parse(dependency.MaximumExclusiveVersion)),
                        dependency.Optional);
                })
                .ToArray();
            var definitions = action.Definitions
                .Select(definition =>
                {
                    ArgumentNullException.ThrowIfNull(definition);
                    return new ContentDefinition(
                        definition.Kind,
                        definition.LocalId,
                        ContentVersion.Parse(definition.Version),
                        definition.DisplayName,
                        definition.PayloadDigest,
                        definition.PayloadJson);
                })
                .ToArray();
            manifest = new ContentPackageManifest(
                action.PackageId,
                ContentVersion.Parse(action.Version),
                action.PackageDigest,
                dependencies,
                definitions,
                action.DeclaredCapabilities.ToArray());
            manifest.Validate();
            return true;
        }
        catch (ArgumentException exception)
        {
            failure = exception.Message;
            return false;
        }
        catch (FormatException exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    private static string EncodeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string EncodeOptional(string? value) => value is null
        ? "-"
        : Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
