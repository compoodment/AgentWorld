using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentWorld.Simulation.Persistence;

/// <summary>
/// Encodes the spike's snapshot and event-log schemas with an explicit,
/// deterministic JSON property order.
/// </summary>
public static class CanonicalPersistenceCodec
{
    private const string Format = "agentworld.persistence-spike/v1";

    public static byte[] EncodeSnapshot(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var stream = new MemoryStream();
        using (var writer = CreateWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WritePropertyName("world_identity");
            WriteIdentity(writer, snapshot.State.Identity);
            writer.WritePropertyName("state");
            writer.WriteStartObject();
            writer.WriteNumber("counter", snapshot.State.Counter);
            writer.WriteNumber("last_event_id", snapshot.State.LastEventId);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static WorldSnapshot DecodeSnapshot(ReadOnlyMemory<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        RequireFormat(root);

        var identity = ReadIdentity(root.GetProperty("world_identity"));
        var state = root.GetProperty("state");
        return new WorldSnapshot(
            new MiniatureWorldState(
                identity,
                state.GetProperty("counter").GetInt32(),
                state.GetProperty("last_event_id").GetInt64()));
    }

    public static byte[] EncodeEventLog(IEnumerable<PersistenceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var orderedEvents = events.ToArray();
        ValidateOrderedEvents(orderedEvents);

        using var stream = new MemoryStream();
        foreach (var worldEvent in orderedEvents)
        {
            using (var writer = CreateWriter(stream))
            {
                WriteEvent(writer, worldEvent);
            }

            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    public static IReadOnlyList<PersistenceEvent> DecodeEventLog(ReadOnlyMemory<byte> bytes)
    {
        var events = new List<PersistenceEvent>();
        foreach (var line in Encoding.UTF8.GetString(bytes.Span).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var document = JsonDocument.Parse(line);
            events.Add(ReadEvent(document.RootElement));
        }

        ValidateOrderedEvents(events);
        return events;
    }

    public static string StateDigest(MiniatureWorldState state) =>
        Digest(EncodeSnapshot(new WorldSnapshot(state)));

    public static string EventDigest(IEnumerable<PersistenceEvent> events) => Digest(EncodeEventLog(events));

    private static Utf8JsonWriter CreateWriter(Stream stream) =>
        new(stream, new JsonWriterOptions { Indented = false });

    private static void WriteIdentity(Utf8JsonWriter writer, WorldIdentity identity)
    {
        writer.WriteStartObject();
        writer.WriteString("world_id", identity.WorldId);
        writer.WriteString("contract_version", identity.ContractVersion);
        writer.WriteString("simulation_version", identity.SimulationVersion);
        writer.WriteString("schema_version", identity.SchemaVersion);
        writer.WriteString("clock_config_version", identity.ClockConfigVersion);
        writer.WriteNumber("world_tick", identity.WorldTick);
        writer.WriteString("world_seed", identity.WorldSeed);
        writer.WriteString("generator_id", identity.GeneratorId);
        writer.WriteString("generator_version", identity.GeneratorVersion);
        writer.WriteString("canonical_generator_config_digest", identity.CanonicalGeneratorConfigDigest);
        writer.WriteNumber("generation_attempt", identity.GenerationAttempt);
        writer.WriteString("initial_map_manifest_digest", identity.InitialMapManifestDigest);
        writer.WriteString("installed_content_lock_digest", identity.InstalledContentLockDigest);
        writer.WriteString("asset_lock_digest", identity.AssetLockDigest);
        writer.WriteEndObject();
    }

    private static WorldIdentity ReadIdentity(JsonElement identity) => new(
        identity.GetProperty("world_id").GetString()!,
        identity.GetProperty("contract_version").GetString()!,
        identity.GetProperty("simulation_version").GetString()!,
        identity.GetProperty("schema_version").GetString()!,
        identity.GetProperty("clock_config_version").GetString()!,
        identity.GetProperty("world_tick").GetInt64(),
        identity.GetProperty("world_seed").GetString()!,
        identity.GetProperty("generator_id").GetString()!,
        identity.GetProperty("generator_version").GetString()!,
        identity.GetProperty("canonical_generator_config_digest").GetString()!,
        identity.GetProperty("generation_attempt").GetInt32(),
        identity.GetProperty("initial_map_manifest_digest").GetString()!,
        identity.GetProperty("installed_content_lock_digest").GetString()!,
        identity.GetProperty("asset_lock_digest").GetString()!);

    private static void WriteEvent(Utf8JsonWriter writer, PersistenceEvent worldEvent)
    {
        writer.WriteStartObject();
        writer.WriteNumber("event_id", worldEvent.EventId);
        writer.WriteNumber("world_tick", worldEvent.WorldTick);
        writer.WriteString("kind", ToWireValue(worldEvent.Kind));
        writer.WriteNumber("counter_delta", worldEvent.CounterDelta);
        if (worldEvent.TargetSchemaVersion is null)
        {
            writer.WriteNull("target_schema_version");
        }
        else
        {
            writer.WriteString("target_schema_version", worldEvent.TargetSchemaVersion);
        }

        writer.WriteEndObject();
    }

    private static PersistenceEvent ReadEvent(JsonElement element) => new(
        element.GetProperty("event_id").GetInt64(),
        element.GetProperty("world_tick").GetInt64(),
        FromWireValue(element.GetProperty("kind").GetString()!),
        element.GetProperty("counter_delta").GetInt32(),
        element.GetProperty("target_schema_version").ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty("target_schema_version").GetString());

    private static void RequireFormat(JsonElement root)
    {
        if (!string.Equals(root.GetProperty("format").GetString(), Format, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Snapshot format is not supported by this persistence spike.");
        }
    }

    private static string ToWireValue(PersistenceEventKind kind) => kind switch
    {
        PersistenceEventKind.CounterAdjusted => "counter_adjusted",
        PersistenceEventKind.MigrationApplied => "migration_applied",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static PersistenceEventKind FromWireValue(string value) => value switch
    {
        "counter_adjusted" => PersistenceEventKind.CounterAdjusted,
        "migration_applied" => PersistenceEventKind.MigrationApplied,
        _ => throw new InvalidDataException($"Unknown persistence event kind '{value}'."),
    };

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void ValidateOrderedEvents(IReadOnlyList<PersistenceEvent> events)
    {
        long previousEventId = 0;
        long previousTick = 0;
        foreach (var worldEvent in events)
        {
            if (worldEvent.EventId <= previousEventId)
            {
                throw new InvalidDataException("Event IDs must be strictly increasing.");
            }

            if (worldEvent.WorldTick < previousTick)
            {
                throw new InvalidDataException("Event ticks must not move backwards.");
            }

            previousEventId = worldEvent.EventId;
            previousTick = worldEvent.WorldTick;
        }
    }
}
