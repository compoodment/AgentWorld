using System.Text.Json;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Control;

namespace ClankerWorld.Viewer.Observation;

public sealed record ManualWorldSave(string Id, string Name, DateTimeOffset CreatedUtc, long WorldTick);

/// <summary>
/// Owner-only named checkpoints for the currently active world. Opaque IDs,
/// atomic writes, and private files keep names out of paths and credentials
/// out of world saves. History segments remain alongside the active save.
/// </summary>
public sealed class ManualWorldSaveStore
{
    private sealed record Metadata(ManualWorldSave Save, IReadOnlyList<InhabitantProviderAssignment> Assignments);
    private readonly object gate = new();
    private readonly string directory;

    public ManualWorldSaveStore(string activeSavePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeSavePath);
        directory = Path.GetFullPath(activeSavePath) + ".manual";
    }

    public static string NormalizeName(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 80 || normalized.Any(char.IsControl))
            throw new ArgumentException("Save name must be 1–80 printable characters.", nameof(name));
        return normalized;
    }

    public ManualWorldSave Create(string name, PrivateWorldRuntime runtime,
        IReadOnlyList<InhabitantProviderAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(assignments);
        name = NormalizeName(name);
        var state = runtime.ExportState();
        if (!state.Society.Society.IsPaused)
            throw new InvalidOperationException("Pause the world before making a manual save.");
        var entry = new ManualWorldSave(Guid.NewGuid().ToString("N"), name, DateTimeOffset.UtcNow,
            state.Society.Society.WorldTick);
        lock (gate)
        {
            Directory.CreateDirectory(directory);
            RestrictDirectory();
            WriteAtomic(StatePath(entry.Id), PrivateWorldRuntimeCodec.Encode(state));
            WriteAtomic(MetadataPath(entry.Id), JsonSerializer.SerializeToUtf8Bytes(new Metadata(entry, assignments)));
        }
        return entry;
    }

    public IReadOnlyList<ManualWorldSave> List()
    {
        lock (gate)
        {
            if (!Directory.Exists(directory)) return [];
            return Directory.EnumerateFiles(directory, "*.meta.json")
                .Select(path => JsonSerializer.Deserialize<Metadata>(File.ReadAllBytes(path))?.Save)
                .Where(item => item is not null && IsId(item.Id) && File.Exists(StatePath(item.Id)))
                .Select(item => item!)
                .OrderByDescending(item => item.CreatedUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public PrivateWorldRuntimeState Read(string id)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            if (!File.Exists(MetadataPath(id)) || !File.Exists(StatePath(id)))
                throw new FileNotFoundException("The manual save does not exist.");
            return PrivateWorldRuntimeCodec.Decode(File.ReadAllBytes(StatePath(id)));
        }
    }

    public IReadOnlyList<InhabitantProviderAssignment> ReadAssignments(string id)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            if (!File.Exists(StatePath(id)) || !File.Exists(MetadataPath(id)))
                throw new FileNotFoundException("The manual save does not exist.");
            return JsonSerializer.Deserialize<Metadata>(File.ReadAllBytes(MetadataPath(id)))?.Assignments
                ?? throw new InvalidDataException("The manual save metadata is invalid.");
        }
    }

    private string StatePath(string id) => Path.Combine(directory, id + ".save");
    private string MetadataPath(string id) => Path.Combine(directory, id + ".meta.json");
    private static bool IsId(string? id) => id is { Length: 32 } && id.All(char.IsAsciiHexDigit);

    private void RestrictDirectory()
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void WriteAtomic(string destination, byte[] bytes)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
