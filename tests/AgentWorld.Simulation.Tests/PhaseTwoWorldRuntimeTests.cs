using AgentWorld.Simulation.Harness;
using System.Text.Json;

namespace AgentWorld.Simulation.Tests;

public sealed class PhaseTwoWorldRuntimeTests
{
    [Fact]
    public void GlobalEventsAreMonotonicAndIndependentFromTheHarnessEventIds()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        _ = runtime.SubmitInstruction(new PhaseTwoInstructionRequest(
            "instruction-key-1",
            "owner",
            "actor-scout",
            PhaseTwoInstructionKind.Suggestive,
            "Gather food."));
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.True(runtime.Pause());
        Assert.True(runtime.Resume());

        var capture = runtime.Capture();

        Assert.Equal(
            Enumerable.Range(1, capture.Events.Count).Select(index => (long)index),
            capture.Events.Select(worldEvent => worldEvent.EventId));
        Assert.Equal(
            capture.Events.OrderBy(worldEvent => worldEvent.EventId),
            capture.Events);
        Assert.Single(capture.Snapshot.World.Events);
        Assert.Equal("instruction_queued", capture.Events[0].Kind);
        Assert.Equal(
            capture.Snapshot.World.Events[0].EventId + 1,
            capture.Events.Single(worldEvent => worldEvent.Kind == "fixture_action_committed").EventId);
    }

    [Fact]
    public void InstructionSubmissionIsServerMintedAndIdempotent()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        var request = new PhaseTwoInstructionRequest(
            "same-request",
            "owner",
            "actor-scout",
            PhaseTwoInstructionKind.MustDo,
            "Return to camp.");

        var first = runtime.SubmitInstruction(request);
        var replay = runtime.SubmitInstruction(request);
        var capture = runtime.Capture();

        Assert.Equal(first, replay);
        Assert.Equal("instruction-0000000001", first.InstructionId);
        Assert.Single(capture.Snapshot.Instructions);
        Assert.Single(capture.Events);
        Assert.Equal("must_do", capture.Events.Single().Detail.Split(':')[1]);
    }

    [Fact]
    public void InstructionCannotTargetAnInhabitantThatIsNotActiveInThisWorld()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");

        var exception = Assert.Throws<ArgumentException>(() => runtime.SubmitInstruction(
            new PhaseTwoInstructionRequest(
                "missing-target",
                "owner-device:test",
                "not-a-real-inhabitant",
                PhaseTwoInstructionKind.Suggestive,
                "Do a little dance.")));

        Assert.Contains("No active inhabitant", exception.Message, StringComparison.Ordinal);
        Assert.Empty(runtime.Capture().Events);
    }

    [Fact]
    public void PauseBlocksFixtureTicksAndResumeCreatesANewRunEpoch()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        Assert.True(runtime.TryAdvanceOneAction());
        var beforePause = runtime.Capture();

        Assert.True(runtime.Pause());
        Assert.False(runtime.TryAdvanceOneAction());
        var paused = runtime.Capture();
        Assert.Equal(beforePause.Snapshot.World.Identity.WorldTick, paused.Snapshot.World.Identity.WorldTick);
        Assert.True(paused.Snapshot.IsPaused);

        Assert.True(runtime.Resume());
        var resumed = runtime.Capture();

        Assert.False(resumed.Snapshot.IsPaused);
        Assert.Equal(beforePause.Snapshot.RunEpoch + 1, resumed.Snapshot.RunEpoch);
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.Equal(beforePause.Snapshot.World.Identity.WorldTick + 1, runtime.Capture().Snapshot.World.Identity.WorldTick);
    }

    [Fact]
    public void ValidPausedBatchIsAtomicAndChangesOnlyTheCurrentTopologyDigest()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        var before = runtime.Capture();
        var water = before.Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;
        Assert.True(runtime.Pause());

        var receipt = runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "turn-water-into-mountain",
            [new SetTerrainOperation(water, TerrainKind.Mountain)]));
        var after = runtime.Capture();

        Assert.True(receipt.Applied, receipt.Failure);
        Assert.Equal(before.Snapshot.InitialMapManifestDigest, after.Snapshot.InitialMapManifestDigest);
        Assert.Equal(before.Snapshot.World.Identity.InitialMapManifestDigest, after.Snapshot.InitialMapManifestDigest);
        Assert.NotEqual(before.Snapshot.CurrentMapManifestDigest, after.Snapshot.CurrentMapManifestDigest);
        Assert.Equal(before.Snapshot.TopologyRevision + 1, after.Snapshot.TopologyRevision);
        Assert.Equal(before.Snapshot.World.Map.ManifestDigest, after.Snapshot.World.Map.ManifestDigest);
        Assert.Equal(TerrainKind.Mountain, after.Snapshot.CurrentMap.Tiles.Single(tile => tile.Position == water).Terrain);
    }

    [Fact]
    public void InvalidMixedPausedBatchLeavesEveryLiveProjectionUntouched()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        Assert.True(runtime.Pause());
        var before = runtime.Capture();
        var water = before.Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;

        var receipt = runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "mixed-invalid",
            [
                new SetTerrainOperation(water, TerrainKind.Mountain),
                new PlaceResourceOperation("berry-patch", "food", new GridPoint(3, 2), true),
            ]));
        var after = runtime.Capture();

        Assert.False(receipt.Applied);
        Assert.Equal(before.Snapshot.Revision, after.Snapshot.Revision);
        Assert.Equal(before.Snapshot.TopologyRevision, after.Snapshot.TopologyRevision);
        Assert.Equal(before.Snapshot.CurrentMapManifestDigest, after.Snapshot.CurrentMapManifestDigest);
        Assert.Equal(
            MapManifestCodec.Encode(before.Snapshot.CurrentMap),
            MapManifestCodec.Encode(after.Snapshot.CurrentMap));
        Assert.Equal(before.Events, after.Events);
    }

    [Fact]
    public void UnapprovedAssetReferenceRejectsTheEntirePausedAuthoringBatch()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        Assert.True(runtime.Pause("owner-device:alice"));
        var before = runtime.Capture();

        var receipt = runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "unapproved-asset-is-atomic",
            [
                new SetWeatherOperation("rain"),
                new AddApprovedAssetReferenceOperation("portrait-alice", "sha256:untrusted"),
            ],
            "owner-device:alice"));
        var after = runtime.Capture();

        Assert.False(receipt.Applied);
        Assert.Contains("not approved", receipt.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before.Snapshot.Revision, after.Snapshot.Revision);
        Assert.Equal(before.Snapshot.Climate, after.Snapshot.Climate);
        Assert.Empty(after.Snapshot.ApprovedAssetReferences);
        Assert.Equal(before.Events, after.Events);
    }

    [Fact]
    public void ServerSuppliedAssetPolicyAllowsOnlyItsExactReference()
    {
        var approved = new PhaseTwoApprovedAssetReference("portrait-alice", "sha256:approved");
        var runtime = new PhaseTwoWorldRuntime(
            "camp-alpha",
            new AllowListedAssetReferencePolicy(approved));
        Assert.True(runtime.Pause("owner-device:alice"));

        var applied = runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "approved-asset",
            [new AddApprovedAssetReferenceOperation(approved.AssetId, approved.AssetDigest)],
            "owner-device:alice"));
        var beforeRejectedRetry = runtime.Capture();
        var rejected = runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "wrong-asset-digest",
            [new AddApprovedAssetReferenceOperation("portrait-alice-copy", "sha256:different")],
            "owner-device:alice"));
        var afterRejectedRetry = runtime.Capture();

        Assert.True(applied.Applied, applied.Failure);
        Assert.Equal(approved, Assert.Single(beforeRejectedRetry.Snapshot.ApprovedAssetReferences));
        Assert.False(rejected.Applied);
        Assert.Contains("not approved", rejected.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeRejectedRetry.Snapshot.Revision, afterRejectedRetry.Snapshot.Revision);
        Assert.Equal(beforeRejectedRetry.Events, afterRejectedRetry.Events);
        Assert.Equal(approved, Assert.Single(afterRejectedRetry.Snapshot.ApprovedAssetReferences));
    }

    [Fact]
    public void AuthoringIsRejectedWhileTheWorldIsRunning()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        var before = runtime.Capture();
        var water = before.Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;

        var receipt = runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "must-pause-first",
            [new SetTerrainOperation(water, TerrainKind.Mountain)]));
        var after = runtime.Capture();

        Assert.False(receipt.Applied);
        Assert.Contains("paused", receipt.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before.Snapshot.Revision, after.Snapshot.Revision);
        Assert.Equal(before.Snapshot.CurrentMapManifestDigest, after.Snapshot.CurrentMapManifestDigest);
        Assert.Empty(after.Events);
    }

    [Fact]
    public void AppliedAuthoringBatchIsIdempotentAndRetainsTheServerStampedIssuer()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        var water = runtime.Capture().Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;
        Assert.True(runtime.Pause("owner-device:first"));
        var request = new PhaseTwoAuthoringBatch(
            "same-authoring-request",
            [new SetTerrainOperation(water, TerrainKind.Mountain)],
            "owner-device:first");

        var first = runtime.ApplyAuthoringBatch(request);
        var afterFirst = runtime.Capture();
        var replay = runtime.ApplyAuthoringBatch(request);
        var afterReplay = runtime.Capture();
        var collision = runtime.ApplyAuthoringBatch(request with
        {
            Operations = [new SetTerrainOperation(water, TerrainKind.Meadow)],
        });

        Assert.True(first.Applied, first.Failure);
        Assert.Equal(first, replay);
        Assert.Equal(afterFirst.Snapshot.Revision, afterReplay.Snapshot.Revision);
        Assert.Equal(afterFirst.Events, afterReplay.Events);
        Assert.Contains("issuer=owner-device:first", afterReplay.Events[^1].Detail, StringComparison.Ordinal);
        Assert.False(collision.Applied);
        Assert.Contains("cannot be reused", collision.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializedStateRestoresTheReconnectCursorAndContinuesGlobalEventIds()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        var water = runtime.Capture().Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;
        Assert.True(runtime.TryAdvanceOneAction());
        Assert.True(runtime.Pause("owner-device:alice"));
        Assert.True(runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "persisted-map-edit",
            [new SetTerrainOperation(water, TerrainKind.Mountain)],
            "owner-device:alice")).Applied);
        _ = runtime.SubmitInstruction(new PhaseTwoInstructionRequest(
            "persisted-instruction",
            "owner-device:alice",
            "actor-scout",
            PhaseTwoInstructionKind.Suggestive,
            "Stay close to camp."));
        Assert.True(runtime.Resume("owner-device:alice"));

        var beforeRestart = runtime.Capture();
        var serialized = JsonSerializer.Serialize(runtime.ExportState());
        var persistedState = JsonSerializer.Deserialize<PhaseTwoWorldRuntimeState>(serialized) ??
            throw new InvalidOperationException("The serialized runtime state was empty.");
        var restored = PhaseTwoWorldRuntime.Restore(persistedState, "camp-alpha");
        var reconnect = restored.Capture(beforeRestart.Snapshot.LatestGlobalEventId);

        Assert.Equal(beforeRestart.Snapshot.World.Identity.WorldTick, reconnect.Snapshot.World.Identity.WorldTick);
        Assert.Equal(beforeRestart.Snapshot.LatestGlobalEventId, reconnect.Snapshot.LatestGlobalEventId);
        Assert.Equal(beforeRestart.Snapshot.CurrentMapManifestDigest, reconnect.Snapshot.CurrentMapManifestDigest);
        Assert.Empty(reconnect.Events);

        Assert.True(restored.TryAdvanceOneAction());
        var continuation = restored.Capture(beforeRestart.Snapshot.LatestGlobalEventId);

        Assert.Single(continuation.Events);
        Assert.Equal(beforeRestart.Snapshot.LatestGlobalEventId + 1, continuation.Events.Single().EventId);
    }

    [Fact]
    public void RestorePreservesPauseAuthoringInstructionAndIdempotencyState()
    {
        var approvedAsset = new PhaseTwoApprovedAssetReference("portrait-lena", "sha256:portrait-lena");
        var approvedAssetPolicy = new AllowListedAssetReferencePolicy(approvedAsset);
        var runtime = new PhaseTwoWorldRuntime("camp-alpha", approvedAssetPolicy);
        var water = runtime.Capture().Snapshot.CurrentMap.Tiles.Single(tile => tile.Terrain == TerrainKind.Water).Position;
        var batch = new PhaseTwoAuthoringBatch(
            "preserve-authoring",
            [
                new SetTerrainOperation(water, TerrainKind.Mountain),
                new SetWeatherSeasonOperation("rain", "winter"),
                new CreateFounderDraftOperation("founder-lena", "Lena", new GridPoint(3, 2)),
                new AddApprovedAssetReferenceOperation(approvedAsset.AssetId, approvedAsset.AssetDigest),
            ],
            "owner-device:alice");
        var instruction = new PhaseTwoInstructionRequest(
            "preserve-instruction",
            "owner-device:alice",
            "actor-scout",
            PhaseTwoInstructionKind.MustDo,
            "Return to camp.");

        Assert.True(runtime.Pause("owner-device:alice"));
        var applied = runtime.ApplyAuthoringBatch(batch);
        var queued = runtime.SubmitInstruction(instruction);
        var restored = PhaseTwoWorldRuntime.Restore(
            runtime.ExportState(),
            "camp-alpha",
            approvedAssetPolicy);
        var capture = restored.Capture();

        Assert.True(capture.Snapshot.IsPaused);
        Assert.Equal(
            TerrainKind.Mountain,
            capture.Snapshot.CurrentMap.Tiles.Single(tile => tile.Position == water).Terrain);
        Assert.Equal(new PhaseTwoClimate("rain", "winter"), capture.Snapshot.Climate);
        Assert.Equal(
            new PhaseTwoFounderDraft("founder-lena", "Lena", new GridPoint(3, 2), applied.Revision),
            Assert.Single(capture.Snapshot.FounderDrafts));
        Assert.Equal(
            approvedAsset,
            Assert.Single(capture.Snapshot.ApprovedAssetReferences));
        Assert.False(restored.TryAdvanceOneAction());
        Assert.Equal(applied, restored.ApplyAuthoringBatch(batch));
        Assert.Equal(queued, restored.SubmitInstruction(instruction));
        Assert.True(restored.Resume("owner-device:alice"));
        Assert.True(restored.TryAdvanceOneAction());
    }

    [Fact]
    public void RestoreFailsClosedWhenNoPolicyApprovesPersistedAssetReferences()
    {
        var approvedAsset = new PhaseTwoApprovedAssetReference("portrait-lena", "sha256:portrait-lena");
        var runtime = new PhaseTwoWorldRuntime(
            "camp-alpha",
            new AllowListedAssetReferencePolicy(approvedAsset));
        Assert.True(runtime.Pause("owner-device:alice"));
        Assert.True(runtime.ApplyAuthoringBatch(new PhaseTwoAuthoringBatch(
            "persisted-approved-asset",
            [new AddApprovedAssetReferenceOperation(approvedAsset.AssetId, approvedAsset.AssetDigest)],
            "owner-device:alice")).Applied);

        var exception = Assert.Throws<InvalidDataException>(() =>
            PhaseTwoWorldRuntime.Restore(runtime.ExportState(), "camp-alpha"));

        Assert.Contains("saved authoring operations", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreRejectsTamperedRuntimeState()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        _ = runtime.SubmitInstruction(new PhaseTwoInstructionRequest(
            "tamper-target",
            "owner-device:alice",
            "actor-scout",
            PhaseTwoInstructionKind.Suggestive,
            "Observe the horizon."));
        var exported = runtime.ExportState();
        var invalidCounter = exported with
        {
            NextGlobalEventId = exported.NextGlobalEventId + 1,
        };
        var invalidMap = exported with
        {
            CurrentMap = exported.CurrentMap with { ManifestDigest = "sha256:tampered" },
        };

        Assert.Throws<InvalidDataException>(() => PhaseTwoWorldRuntime.Restore(invalidCounter, "camp-alpha"));
        Assert.Throws<InvalidDataException>(() => PhaseTwoWorldRuntime.Restore(invalidMap, "camp-alpha"));
    }

    [Fact]
    public void RemoveResourceOperationAppliesOnceAndCanBeRetried()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        const string resourceId = "temporary-food";
        var batch = new PhaseTwoAuthoringBatch(
            "remove-resource-once",
            [
                new PlaceResourceOperation(resourceId, "food", new GridPoint(3, 2), true),
                new RemoveResourceOperation(resourceId),
            ],
            "owner-device:alice");
        Assert.True(runtime.Pause("owner-device:alice"));

        var applied = runtime.ApplyAuthoringBatch(batch);

        Assert.True(applied.Applied, applied.Failure);
        Assert.DoesNotContain(
            runtime.Capture().Snapshot.CurrentMap.Resources,
            resource => string.Equals(resource.Id, resourceId, StringComparison.Ordinal));
        Assert.Equal(applied, runtime.ApplyAuthoringBatch(batch));
    }

    private sealed class AllowListedAssetReferencePolicy : IPhaseTwoApprovedAssetReferencePolicy
    {
        private readonly HashSet<PhaseTwoApprovedAssetReference> approvedReferences;

        public AllowListedAssetReferencePolicy(params PhaseTwoApprovedAssetReference[] approvedReferences)
        {
            ArgumentNullException.ThrowIfNull(approvedReferences);
            this.approvedReferences = new HashSet<PhaseTwoApprovedAssetReference>(approvedReferences);
        }

        public bool IsApproved(PhaseTwoApprovedAssetReference reference)
        {
            ArgumentNullException.ThrowIfNull(reference);
            return approvedReferences.Contains(reference);
        }
    }
}
