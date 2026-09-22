using AgentWorld.Simulation.Content;

namespace AgentWorld.Simulation.Tests;

public sealed class WorldAssetReservationTests
{
    private const string PackageDigestA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string PackageDigestB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string SharedNormalizedDigest =
        "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void SharedNormalizedDigestsAreChargedOnceButReferencesStillChargeRenderWork()
    {
        var ledger = new WorldAssetReservationLedger(new WorldAssetReservationPolicy
        {
            MaxDurableStorageBytes = 100,
            MaxDecodedCacheBytes = 100,
            MaxGpuBytes = 100,
            MaxRenderUnits = 8,
        });

        var first = ledger.TryReservePackage(
            "first",
            [Request(PackageDigestA, "portrait-a", SharedNormalizedDigest, 50, 2)],
            0);
        var second = ledger.TryReservePackage(
            "second",
            [Request(PackageDigestB, "portrait-b", SharedNormalizedDigest, 50, 3)],
            1);

        Assert.True(first.IsSuccess, first.Diagnostic);
        Assert.True(second.IsSuccess, second.Diagnostic);
        Assert.Equal(1, second.Totals.DistinctAssetCount);
        Assert.Equal(50, second.Totals.DurableStorageBytes);
        Assert.Equal(50, second.Totals.DecodedCacheBytes);
        Assert.Equal(50, second.Totals.GpuBytes);
        Assert.Equal(5, second.Totals.RenderUnits);
    }

    [Fact]
    public void WorldReservationBudgetFailureIsAtomic()
    {
        var ledger = new WorldAssetReservationLedger(new WorldAssetReservationPolicy
        {
            MaxDurableStorageBytes = 10,
            MaxDecodedCacheBytes = 10,
            MaxGpuBytes = 10,
            MaxRenderUnits = 4,
        });
        Assert.True(ledger.TryReservePackage("first", [Request(PackageDigestA, "one", PackageDigestA, 10, 1)], 0).IsSuccess);

        var rejected = ledger.TryReservePackage(
            "second",
            [Request(PackageDigestB, "two", PackageDigestB, 1, 1)],
            1);

        Assert.False(rejected.IsSuccess);
        Assert.Equal("world_storage_breach", rejected.FailureCode);
        Assert.Single(ledger.ExportState().Reservations);
        Assert.Empty(ledger.GetPackageReservations("second"));
    }

    [Fact]
    public void WorldReservationLedgerRoundTripsCanonicallyAndReleasesAWholePackage()
    {
        var ledger = new WorldAssetReservationLedger();
        Assert.True(ledger.TryReservePackage("first", [Request(PackageDigestA, "one", PackageDigestA, 1, 1)], 0).IsSuccess);
        Assert.True(ledger.TryReservePackage("second", [Request(PackageDigestB, "two", PackageDigestB, 1, 1)], 1).IsSuccess);

        var encoded = System.Text.Json.JsonSerializer.Serialize(ledger.ExportState());
        var restored = WorldAssetReservationLedger.Restore(
            System.Text.Json.JsonSerializer.Deserialize<WorldAssetReservationLedgerState>(encoded));

        Assert.Equal(ledger.ExportState().Reservations, restored.ExportState().Reservations);
        Assert.Equal(ledger.ExportState().Events, restored.ExportState().Events);
        Assert.True(restored.ReleasePackage("first", 2));
        Assert.Single(restored.ExportState().Reservations);
        restored.Validate();
    }

    private static WorldAssetReservationRequest Request(
        string packageDigest,
        string localId,
        string normalizedDigest,
        long bytes,
        int renderUnits) => new(
        AssetRules.CanonicalAssetId(packageDigest, localId, ContentVersion.Parse("1.0.0")),
        normalizedDigest,
        "rgba8",
        bytes,
        bytes,
        bytes,
        renderUnits);
}
