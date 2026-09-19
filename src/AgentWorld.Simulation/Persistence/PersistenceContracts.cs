namespace AgentWorld.Simulation.Persistence;

/// <summary>
/// The versioned identity that accompanies every persistence-spike save.
/// </summary>
/// <remarks>
/// This mirrors the Phase 1 contract's identity fields. The spike uses strings
/// for version and digest values so it can test compatibility boundaries without
/// committing to a production identifier or storage type.
/// </remarks>
public sealed record WorldIdentity(
    string WorldId,
    string ContractVersion,
    string SimulationVersion,
    string SchemaVersion,
    string ClockConfigVersion,
    long WorldTick,
    string WorldSeed,
    string GeneratorId,
    string GeneratorVersion,
    string CanonicalGeneratorConfigDigest,
    int GenerationAttempt,
    string InitialMapManifestDigest,
    string InstalledContentLockDigest,
    string AssetLockDigest);

/// <summary>
/// The miniature state used solely to exercise persistence semantics.
/// </summary>
public sealed record MiniatureWorldState(WorldIdentity Identity, int Counter, long LastEventId)
{
    public static MiniatureWorldState Genesis(WorldIdentity identity) => new(identity, 0, 0);
}

/// <summary>
/// The two transition forms required by this feasibility spike.
/// </summary>
public enum PersistenceEventKind
{
    CounterAdjusted,
    MigrationApplied,
}

/// <summary>
/// An ordered, append-only persistence event.
/// </summary>
public sealed record PersistenceEvent(
    long EventId,
    long WorldTick,
    PersistenceEventKind Kind,
    int CounterDelta,
    string? TargetSchemaVersion);

/// <summary>
/// A snapshot whose state is valid immediately after its final committed event.
/// </summary>
public sealed record WorldSnapshot(MiniatureWorldState State)
{
    public long LastEventId => State.LastEventId;
}

/// <summary>
/// The exact bytes that would be persisted by the spike.
/// </summary>
public sealed record PersistedWorld(byte[] SnapshotBytes, byte[] EventLogBytes)
{
    public PersistedWorld Checkpoint() => new(SnapshotBytes.ToArray(), EventLogBytes.ToArray());
}

/// <summary>
/// The compatibility locks that a target runtime requires before migration.
/// </summary>
public sealed record SaveCompatibility(
    string ContractVersion,
    string SimulationVersion,
    string SchemaVersion,
    string ClockConfigVersion,
    string GeneratorId,
    string GeneratorVersion,
    string CanonicalGeneratorConfigDigest,
    string InitialMapManifestDigest,
    string InstalledContentLockDigest,
    string AssetLockDigest)
{
    public static SaveCompatibility From(WorldIdentity identity) => new(
        identity.ContractVersion,
        identity.SimulationVersion,
        identity.SchemaVersion,
        identity.ClockConfigVersion,
        identity.GeneratorId,
        identity.GeneratorVersion,
        identity.CanonicalGeneratorConfigDigest,
        identity.InitialMapManifestDigest,
        identity.InstalledContentLockDigest,
        identity.AssetLockDigest);
}
