using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Playtest;

namespace AgentWorld.Viewer.Observation;

/// <summary>
/// Atomic persistence for the integrated private-world alpha runtime. The
/// provider factory is supplied by the host and credentials never enter the
/// checkpoint bytes.
/// </summary>
public sealed class PrivateWorldStateFile
{
    private readonly object gate = new();
    private readonly Func<string, IDecisionProvider>? providerFactory;

    public PrivateWorldStateFile(string path, Func<string, IDecisionProvider>? providerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        this.providerFactory = providerFactory;
    }

    public string Path { get; }

    public PrivateWorldRuntime LoadOrCreate(string worldSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        lock (gate)
        {
            if (!File.Exists(Path))
            {
                var created = new PrivateWorldRuntime(worldSeed, providerFactory);
                SaveUnsafe(created.ExportState());
                return created;
            }

            var state = PrivateWorldRuntimeCodec.Decode(File.ReadAllBytes(Path));
            if (!string.Equals(state.WorldSeed, worldSeed, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The private-world save belongs to a different configured seed.");
            }

            var restored = PrivateWorldRuntime.Restore(state, providerFactory);
            SaveUnsafe(restored.ExportState());
            return restored;
        }
    }

    public void Save(PrivateWorldRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        lock (gate)
        {
            SaveUnsafe(runtime.ExportState());
        }
    }

    private void SaveUnsafe(PrivateWorldRuntimeState state)
    {
        var directory = System.IO.Path.GetDirectoryName(Path) ??
            throw new InvalidOperationException("The private-world state path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporaryPath, PrivateWorldRuntimeCodec.Encode(state));
            RestrictPermissions(temporaryPath);
            File.Move(temporaryPath, Path, overwrite: true);
            RestrictPermissions(Path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void RestrictPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
