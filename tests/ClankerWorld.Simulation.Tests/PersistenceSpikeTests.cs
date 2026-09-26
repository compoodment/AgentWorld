using System.Text;
using ClankerWorld.Simulation.Persistence;

namespace ClankerWorld.Simulation.Tests;

public sealed class PersistenceSpikeTests
{
    [Fact]
    public void GenesisAndSnapshotSuffixReplayHaveIdenticalStateAndEventDigests()
    {
        var identity = CreateIdentity();
        var events = CreateEvents();
        var genesisReplay = WorldReplay.ReplayGenesis(identity, events);
        var snapshotState = WorldReplay.ReplayGenesis(identity, events.Take(2));
        var snapshot = CanonicalPersistenceCodec.DecodeSnapshot(
            CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(snapshotState)));
        var suffixReplay = WorldReplay.ReplaySnapshotSuffix(snapshot, events);

        Assert.Equal(
            CanonicalPersistenceCodec.StateDigest(genesisReplay),
            CanonicalPersistenceCodec.StateDigest(suffixReplay));
        Assert.Equal(
            CanonicalPersistenceCodec.EventDigest(events),
            CanonicalPersistenceCodec.EventDigest(
                CanonicalPersistenceCodec.DecodeEventLog(CanonicalPersistenceCodec.EncodeEventLog(events))));
    }

    [Fact]
    public void CanonicalSnapshotAndEventLogRoundTripByteForByteAndRemainInspectable()
    {
        var identity = CreateIdentity();
        var events = CreateEvents();
        var snapshotState = WorldReplay.ReplayGenesis(identity, events.Take(2));
        var save = new PersistedWorld(
            CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(snapshotState)),
            CanonicalPersistenceCodec.EncodeEventLog(events));

        var decodedSnapshot = CanonicalPersistenceCodec.DecodeSnapshot(save.SnapshotBytes);
        var decodedEvents = CanonicalPersistenceCodec.DecodeEventLog(save.EventLogBytes);
        var inspection = SaveInspector.Inspect(save);

        Assert.True(save.SnapshotBytes.SequenceEqual(CanonicalPersistenceCodec.EncodeSnapshot(decodedSnapshot)));
        Assert.True(save.EventLogBytes.SequenceEqual(CanonicalPersistenceCodec.EncodeEventLog(decodedEvents)));
        Assert.Equal(identity.WorldId, inspection.Identity.WorldId);
        Assert.Equal(3, inspection.LastEventId);
        Assert.Equal(3, inspection.EventCount);
    }

    [Fact]
    public void VersionOneSnapshotRemainsDecodableAfterTheCanonicalPayloadExtension()
    {
        var state = WorldReplay.ReplayGenesis(CreateIdentity(), CreateEvents().Take(2));
        var current = Encoding.UTF8.GetString(
            CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(state)));
        var versionOne = current
            .Replace(
                "\"format\":\"agentworld.persistence-spike/v2\"",
                "\"format\":\"agentworld.persistence-spike/v1\"",
                StringComparison.Ordinal)
            .Replace(",\"canonical_state_payload\":null", string.Empty, StringComparison.Ordinal);

        var decoded = CanonicalPersistenceCodec.DecodeSnapshot(Encoding.UTF8.GetBytes(versionOne));

        Assert.Equal(state.Identity, decoded.State.Identity);
        Assert.Equal(state.Counter, decoded.State.Counter);
        Assert.Equal(state.LastEventId, decoded.State.LastEventId);
        Assert.Null(decoded.State.CanonicalStatePayload);
    }

    [Fact]
    public void PreflightRefusesContentLockMismatchWithoutMutatingTheSave()
    {
        var source = CreateSave(CreateIdentity(), CreateEvents());
        var originalSnapshot = source.SnapshotBytes.ToArray();
        var originalEvents = source.EventLogBytes.ToArray();
        var required = SaveCompatibility.From(CreateIdentity("2")) with
        {
            InstalledContentLockDigest = "content-lock-different",
        };
        var plan = new MigrationPlan("1", "2", 0);

        var preflight = MigrationPreflightChecker.Evaluate(source, required, plan);

        Assert.False(preflight.IsCompatible);
        Assert.Equal("Content lock mismatch.", preflight.Refusal);
        Assert.True(originalSnapshot.SequenceEqual(source.SnapshotBytes));
        Assert.True(originalEvents.SequenceEqual(source.EventLogBytes));
        Assert.Equal(3, SaveInspector.Inspect(source).EventCount);
    }

    [Fact]
    public void PreflightChecksEveryRequiredRuntimeLock()
    {
        var source = CreateSave(CreateIdentity(), CreateEvents());
        var required = SaveCompatibility.From(CreateIdentity("2"));
        var plan = new MigrationPlan("1", "2", 0);
        var incompatibleTargets = new (SaveCompatibility Target, string Reason)[]
        {
            (required with { ContractVersion = "contract-other" }, "Contract version mismatch."),
            (required with { SimulationVersion = "simulation-other" }, "Simulation version mismatch."),
            (required with { ClockConfigVersion = "clock-other" }, "Clock configuration mismatch."),
            (required with { GeneratorVersion = "generator-other" }, "Generator lock mismatch."),
            (required with { InitialMapManifestDigest = "map-other" }, "Initial map manifest lock mismatch."),
            (required with { InstalledContentLockDigest = "content-other" }, "Content lock mismatch."),
            (required with { AssetLockDigest = "asset-other" }, "Asset lock mismatch."),
        };

        foreach (var (target, reason) in incompatibleTargets)
        {
            var outcome = AtomicMigrator.Apply(source, target, plan);

            Assert.Equal(MigrationOutcomeKind.Refused, outcome.Kind);
            Assert.Equal(reason, outcome.Reason);
            Assert.Null(outcome.MigratedSave);
        }
    }

    [Fact]
    public void FailingMigrationLeavesTheLiveSourceUntouchedAndInspectable()
    {
        var source = CreateSave(CreateIdentity(), CreateEvents());
        var originalSnapshot = source.SnapshotBytes.ToArray();
        var originalEvents = source.EventLogBytes.ToArray();
        var required = SaveCompatibility.From(CreateIdentity("2"));
        var plan = new MigrationPlan("1", "2", 99);

        var outcome = AtomicMigrator.Apply(source, required, plan);

        Assert.Equal(MigrationOutcomeKind.Failed, outcome.Kind);
        Assert.Null(outcome.MigratedSave);
        Assert.True(originalSnapshot.SequenceEqual(source.SnapshotBytes));
        Assert.True(originalEvents.SequenceEqual(source.EventLogBytes));
        Assert.True(originalSnapshot.SequenceEqual(outcome.Checkpoint.SnapshotBytes));
        Assert.True(originalEvents.SequenceEqual(outcome.Checkpoint.EventLogBytes));
        Assert.Equal("world-spike", SaveInspector.Inspect(source).Identity.WorldId);
    }

    [Fact]
    public void InterruptedMigrationResumesDeterministicallyFromItsCheckpoint()
    {
        var identity = CreateIdentity();
        var source = CreateSave(identity, CreateEvents());
        var required = SaveCompatibility.From(CreateIdentity("2"));
        var plan = new MigrationPlan("1", "2", 0);

        var uninterrupted = AtomicMigrator.Apply(source, required, plan);
        var interrupted = AtomicMigrator.Apply(
            source,
            required,
            plan,
            MigrationExecutionMode.InterruptAfterCheckpoint);
        var resumed = AtomicMigrator.Resume(interrupted, required, plan);

        Assert.Equal(MigrationOutcomeKind.Completed, uninterrupted.Kind);
        Assert.Equal(MigrationOutcomeKind.Interrupted, interrupted.Kind);
        Assert.Equal(MigrationOutcomeKind.Completed, resumed.Kind);
        Assert.NotNull(uninterrupted.MigratedSave);
        Assert.NotNull(resumed.MigratedSave);
        Assert.True(uninterrupted.MigratedSave.SnapshotBytes.SequenceEqual(resumed.MigratedSave.SnapshotBytes));
        Assert.True(uninterrupted.MigratedSave.EventLogBytes.SequenceEqual(resumed.MigratedSave.EventLogBytes));

        var migratedSnapshot = CanonicalPersistenceCodec.DecodeSnapshot(resumed.MigratedSave.SnapshotBytes);
        var migratedEvents = CanonicalPersistenceCodec.DecodeEventLog(resumed.MigratedSave.EventLogBytes);
        var migratedGenesisReplay = WorldReplay.ReplayGenesis(identity, migratedEvents);

        Assert.Equal("2", migratedSnapshot.State.Identity.SchemaVersion);
        Assert.Equal(
            CanonicalPersistenceCodec.StateDigest(migratedSnapshot.State),
            CanonicalPersistenceCodec.StateDigest(migratedGenesisReplay));
        Assert.Equal(
            CanonicalPersistenceCodec.EventDigest(migratedEvents),
            SaveInspector.Inspect(resumed.MigratedSave).EventDigest);
    }

    private static PersistedWorld CreateSave(WorldIdentity identity, IReadOnlyList<PersistenceEvent> events)
    {
        var snapshotState = WorldReplay.ReplayGenesis(identity, events.Take(2));
        return new PersistedWorld(
            CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(snapshotState)),
            CanonicalPersistenceCodec.EncodeEventLog(events));
    }

    private static WorldIdentity CreateIdentity(string schemaVersion = "1") => new(
        "world-spike",
        "contract-1",
        "simulation-1",
        schemaVersion,
        "clock-1",
        0,
        "seed-123",
        "temperate-camp",
        "generator-1",
        "generator-config-digest",
        1,
        "map-manifest-digest",
        "content-lock-a",
        "asset-lock-a");

    private static IReadOnlyList<PersistenceEvent> CreateEvents() =>
    [
        new PersistenceEvent(1, 1, PersistenceEventKind.CounterAdjusted, 5, null),
        new PersistenceEvent(2, 2, PersistenceEventKind.CounterAdjusted, -2, null),
        new PersistenceEvent(3, 3, PersistenceEventKind.CounterAdjusted, 10, null),
    ];
}
