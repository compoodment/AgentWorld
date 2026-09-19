using System.Text.Json;

namespace AgentWorld.Simulation.Persistence;

/// <summary>
/// A small, directed schema migration used to test preflight and recovery
/// behavior without committing to a production migration framework.
/// </summary>
public sealed record MigrationPlan(
    string FromSchemaVersion,
    string ToSchemaVersion,
    int RequiredMinimumCounter);

/// <summary>
/// The result of checking a save against the runtime's required locks.
/// </summary>
public sealed record MigrationPreflight(bool IsCompatible, string? Refusal)
{
    public static MigrationPreflight Accept() => new(true, null);

    public static MigrationPreflight Refuse(string reason) => new(false, reason);
}

/// <summary>
/// Validates a migration plan on decoded copies before any activation work.
/// </summary>
public static class MigrationPreflightChecker
{
    public static MigrationPreflight Evaluate(
        PersistedWorld source,
        SaveCompatibility required,
        MigrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(plan);

        MiniatureWorldState current;
        try
        {
            var snapshot = CanonicalPersistenceCodec.DecodeSnapshot(source.SnapshotBytes);
            var events = CanonicalPersistenceCodec.DecodeEventLog(source.EventLogBytes);
            current = WorldReplay.ReplaySnapshotSuffix(snapshot, events);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            return MigrationPreflight.Refuse($"Save is not inspectable: {exception.Message}");
        }

        var identity = current.Identity;
        if (!string.Equals(identity.SchemaVersion, plan.FromSchemaVersion, StringComparison.Ordinal))
        {
            return MigrationPreflight.Refuse("Source schema does not match the migration plan.");
        }

        if (!string.Equals(plan.ToSchemaVersion, required.SchemaVersion, StringComparison.Ordinal))
        {
            return MigrationPreflight.Refuse("Migration target does not match the required schema.");
        }

        return FirstMismatch(identity, required) is { } mismatch
            ? MigrationPreflight.Refuse(mismatch)
            : MigrationPreflight.Accept();
    }

    private static string? FirstMismatch(WorldIdentity identity, SaveCompatibility required)
    {
        if (!string.Equals(identity.ContractVersion, required.ContractVersion, StringComparison.Ordinal))
        {
            return "Contract version mismatch.";
        }

        if (!string.Equals(identity.SimulationVersion, required.SimulationVersion, StringComparison.Ordinal))
        {
            return "Simulation version mismatch.";
        }

        if (!string.Equals(identity.ClockConfigVersion, required.ClockConfigVersion, StringComparison.Ordinal))
        {
            return "Clock configuration mismatch.";
        }

        if (!string.Equals(identity.GeneratorId, required.GeneratorId, StringComparison.Ordinal) ||
            !string.Equals(identity.GeneratorVersion, required.GeneratorVersion, StringComparison.Ordinal) ||
            !string.Equals(identity.CanonicalGeneratorConfigDigest, required.CanonicalGeneratorConfigDigest, StringComparison.Ordinal))
        {
            return "Generator lock mismatch.";
        }

        if (!string.Equals(identity.InitialMapManifestDigest, required.InitialMapManifestDigest, StringComparison.Ordinal))
        {
            return "Initial map manifest lock mismatch.";
        }

        if (!string.Equals(identity.InstalledContentLockDigest, required.InstalledContentLockDigest, StringComparison.Ordinal))
        {
            return "Content lock mismatch.";
        }

        if (!string.Equals(identity.AssetLockDigest, required.AssetLockDigest, StringComparison.Ordinal))
        {
            return "Asset lock mismatch.";
        }

        return null;
    }
}

/// <summary>
/// A deliberately injected interruption point immediately after checkpointing.
/// </summary>
public enum MigrationExecutionMode
{
    Activate,
    InterruptAfterCheckpoint,
}

/// <summary>
/// The outcome of an isolated migration attempt.
/// </summary>
public enum MigrationOutcomeKind
{
    Completed,
    Refused,
    Failed,
    Interrupted,
}

/// <summary>
/// Carries a migration outcome and a recoverable source checkpoint.
/// </summary>
public sealed record MigrationOutcome(
    MigrationOutcomeKind Kind,
    PersistedWorld Checkpoint,
    PersistedWorld? MigratedSave,
    string? Reason);

/// <summary>
/// Applies a single migration on an isolated copy and activates only a fully
/// validated result.
/// </summary>
public static class AtomicMigrator
{
    public static MigrationOutcome Apply(
        PersistedWorld source,
        SaveCompatibility required,
        MigrationPlan plan,
        MigrationExecutionMode mode = MigrationExecutionMode.Activate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(plan);

        var checkpoint = source.Checkpoint();
        var preflight = MigrationPreflightChecker.Evaluate(checkpoint, required, plan);
        if (!preflight.IsCompatible)
        {
            return new MigrationOutcome(MigrationOutcomeKind.Refused, checkpoint, null, preflight.Refusal);
        }

        if (mode == MigrationExecutionMode.InterruptAfterCheckpoint)
        {
            return new MigrationOutcome(
                MigrationOutcomeKind.Interrupted,
                checkpoint,
                null,
                "Interrupted after the source checkpoint and before activation.");
        }

        try
        {
            var snapshot = CanonicalPersistenceCodec.DecodeSnapshot(checkpoint.SnapshotBytes);
            var events = CanonicalPersistenceCodec.DecodeEventLog(checkpoint.EventLogBytes).ToList();
            var current = WorldReplay.ReplaySnapshotSuffix(snapshot, events);

            if (current.Counter < plan.RequiredMinimumCounter)
            {
                return new MigrationOutcome(
                    MigrationOutcomeKind.Failed,
                    checkpoint,
                    null,
                    "Migration state validation failed: counter is below the required minimum.");
            }

            var migrationEvent = new PersistenceEvent(
                current.LastEventId + 1,
                current.Identity.WorldTick,
                PersistenceEventKind.MigrationApplied,
                0,
                plan.ToSchemaVersion);
            var migrated = WorldReplay.Apply(current, migrationEvent);
            events.Add(migrationEvent);

            var migratedSave = new PersistedWorld(
                CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(migrated)),
                CanonicalPersistenceCodec.EncodeEventLog(events));
            _ = SaveInspector.Inspect(migratedSave);

            return new MigrationOutcome(MigrationOutcomeKind.Completed, checkpoint, migratedSave, null);
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException or JsonException)
        {
            return new MigrationOutcome(MigrationOutcomeKind.Failed, checkpoint, null, exception.Message);
        }
    }

    public static MigrationOutcome Resume(
        MigrationOutcome interrupted,
        SaveCompatibility required,
        MigrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(interrupted);
        if (interrupted.Kind != MigrationOutcomeKind.Interrupted)
        {
            throw new InvalidOperationException("Only an interrupted migration can be resumed.");
        }

        return Apply(interrupted.Checkpoint, required, plan);
    }
}

/// <summary>
/// Decodes a save without attempting to run it, preserving the contract's
/// recoverable-for-inspection path.
/// </summary>
public static class SaveInspector
{
    public static SaveInspection Inspect(PersistedWorld save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var snapshot = CanonicalPersistenceCodec.DecodeSnapshot(save.SnapshotBytes);
        var events = CanonicalPersistenceCodec.DecodeEventLog(save.EventLogBytes);
        var current = WorldReplay.ReplaySnapshotSuffix(snapshot, events);

        return new SaveInspection(
            current.Identity,
            current.LastEventId,
            events.Count,
            CanonicalPersistenceCodec.StateDigest(current),
            CanonicalPersistenceCodec.EventDigest(events));
    }
}

/// <summary>
/// The non-mutating summary returned by <see cref="SaveInspector"/>.
/// </summary>
public sealed record SaveInspection(
    WorldIdentity Identity,
    long LastEventId,
    int EventCount,
    string StateDigest,
    string EventDigest);
