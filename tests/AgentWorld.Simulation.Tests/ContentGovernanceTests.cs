using AgentWorld.Simulation.Content;

namespace AgentWorld.Simulation.Tests;

public sealed class ContentGovernanceTests
{
    [Fact]
    public void ContentDefinitionUsesAnImmutablePackageDigestIdentity()
    {
        var definition = Definition("item", "berry", "sha256:" + new string('b', 64));

        definition.Validate();

        Assert.Equal(
            $"sha256:{new string('a', 64)}/item/berry@1.0.0",
            definition.CanonicalId("sha256:" + new string('a', 64)));
    }

    [Fact]
    public void ResolverChoosesTheHighestCompatibleVersionAndLocksDependenciesFirst()
    {
        var coreV1 = Package("core", "1.0.0", 'a');
        var coreV11 = Package("core", "1.1.0", 'b');
        var world = Package(
            "world",
            "1.0.0",
            'c',
            [new ContentDependency(
                "core",
                new ContentVersionRange(ContentVersion.Parse("1.0.0"), ContentVersion.Parse("2.0.0")))]);

        var result = ContentPackageResolver.Resolve([world, coreV1, coreV11], ["world"]);

        Assert.True(result.IsSuccess, result.Diagnostic);
        Assert.Equal(["core", "world"], result.Lock.Select(entry => entry.PackageId));
        Assert.Equal(ContentVersion.Parse("1.1.0"), result.Lock[0].Version);
        Assert.Equal(ContentPackageRules.LockDigest(result.Lock), ContentPackageRules.LockDigest(result.Lock));
    }

    [Fact]
    public void ResolverReportsCyclesAndDoesNotReturnAPartialLock()
    {
        var first = Package(
            "first",
            "1.0.0",
            'a',
            [Dependency("second")]);
        var second = Package(
            "second",
            "1.0.0",
            'b',
            [Dependency("first")]);

        var result = ContentPackageResolver.Resolve([first, second], ["first"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("dependency_cycle", result.FailureCode);
        Assert.Empty(result.Lock);
    }

    [Fact]
    public void MissingOptionalDependenciesDoNotBlockResolution()
    {
        var package = Package(
            "world",
            "1.0.0",
            'a',
            [new ContentDependency(
                "optional-art",
                new ContentVersionRange(ContentVersion.Parse("1.0.0"), ContentVersion.Parse("2.0.0")),
                Optional: true)]);

        var result = ContentPackageResolver.Resolve([package], ["world"]);

        Assert.True(result.IsSuccess, result.Diagnostic);
        Assert.Single(result.Lock);
    }

    [Fact]
    public void DataOnlyPackageFollowsTheProposeValidateApproveStageActivateLifecycle()
    {
        var package = Package("camp-items", "1.0.0", 'a');
        var resolution = ContentPackageResolver.Resolve([package], [package.PackageId]);
        var registry = new ContentPackageRegistry();

        registry.Propose(package);
        registry.Validate(package.PackageId, resolution, worldTick: 4);
        registry.Approve(package.PackageId, worldTick: 4);
        registry.Stage(package.PackageId, worldTick: 4);
        var active = registry.Activate(package.PackageId, worldTick: 5);

        Assert.Equal(ContentPackageLifecycle.Active, active.Lifecycle);
        Assert.Equal(5, active.ActivationTick);
        Assert.Equal(
            ["package_proposed", "package_validated", "package_approved", "package_staged", "package_activated"],
            registry.ExportState().Events.Select(item => item.Kind));
    }

    [Fact]
    public void ExecutableCapabilitiesAreDisabledAtTheDataOnlyBoundary()
    {
        var package = Package("unsafe", "1.0.0", 'a', capabilities: ["network"]);
        var registry = new ContentPackageRegistry();

        var exception = Assert.Throws<InvalidOperationException>(() => registry.Propose(package));

        Assert.Contains("disabled capabilities", exception.Message, StringComparison.Ordinal);
    }

    private static ContentPackageManifest Package(
        string id,
        string version,
        char digestCharacter,
        IReadOnlyList<ContentDependency>? dependencies = null,
        IReadOnlyList<string>? capabilities = null) => new(
        id,
        ContentVersion.Parse(version),
        "sha256:" + new string(digestCharacter, 64),
        dependencies ?? [],
        [Definition("item", $"{id}-content", "sha256:" + new string(digestCharacter, 64))],
        capabilities ?? []);

    private static ContentDependency Dependency(string packageId) => new(
        packageId,
        new ContentVersionRange(ContentVersion.Parse("1.0.0"), ContentVersion.Parse("2.0.0")));

    private static ContentDefinition Definition(string kind, string localId, string digest) => new(
        kind,
        localId,
        ContentVersion.Parse("1.0.0"),
        localId,
        digest);
}
