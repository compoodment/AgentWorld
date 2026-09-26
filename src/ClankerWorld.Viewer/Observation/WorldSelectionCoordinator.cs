using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Viewer.Control;

namespace ClankerWorld.Viewer.Observation;

/// <summary>Serializes paused-world selection against saves and provider routing.</summary>
public sealed class WorldSelectionCoordinator(
    WorldCatalogStore catalog,
    PrivateWorldRuntime runtime,
    PrivateWorldStateFile stateFile,
    ProviderConfigurationStore providers,
    WorldAutosaveStore autosave,
    WorldJevPolicy jevPolicy,
    ILogger<WorldSelectionCoordinator> logger,
    Func<string, IDecisionProvider> providerFactory)
{
    private readonly object gate = new();

    public WorldCatalogSnapshot List()
    {
        lock (gate) return catalog.Capture();
    }

    public ViewerWorldPreview Preview(GeographyOptions geography)
    {
        ArgumentNullException.ThrowIfNull(geography);
        if (geography.Size is not (WorldSizePreset.Small or WorldSizePreset.Medium))
            throw new ArgumentException("Only Small and Medium are playable yet.", nameof(geography));
        lock (gate)
        {
            RequirePaused();
            var map = GeneratedCampMapGenerator.Generate(geography);
            var camp = map.GetObject("bedroll").Position;
            WorldSelectionTelemetry.Previewed(logger, map.Width, map.Height);
            return new ViewerWorldPreview(OwnerWorldObservationStore.PackTerrain(map),
                new ViewerPosition(camp.X, camp.Y), map.ManifestDigest);
        }
    }

    public CatalogWorld Create(string name, GeographyOptions geography)
    {
        ArgumentNullException.ThrowIfNull(geography);
        if (geography.Size is not (WorldSizePreset.Small or WorldSizePreset.Medium))
            throw new ArgumentException("Large, Huge and Mega need compact persistent terrain before they can be played.", nameof(geography));
        lock (gate)
        {
            RequirePaused();
            using var created = new PrivateWorldRuntime(geography.Seed, providerFactory,
                startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
            var entry = catalog.Add(name, created.ExportState());
            SelectCore(entry, created.ExportState());
            if (logger.IsEnabled(LogLevel.Information))
            {
                var sizeName = geography.Size.ToString().ToLowerInvariant();
                WorldSelectionTelemetry.Created(logger, entry.Id, sizeName);
            }
            return entry;
        }
    }

    public CatalogWorld Select(string id)
    {
        lock (gate)
        {
            RequirePaused();
            var entry = catalog.Capture().Worlds.SingleOrDefault(world => world.Id == id)
                ?? throw new FileNotFoundException("The selected world does not exist.");
            if (entry.Id == catalog.Capture().ActiveId) return entry;
            SelectCore(entry, catalog.Read(id));
            WorldSelectionTelemetry.Selected(logger, entry.Id);
            return entry;
        }
    }

    private void SelectCore(CatalogWorld entry, PrivateWorldRuntimeState target)
    {
        var old = runtime.ExportState();
        var oldEntry = catalog.Active();
        var oldAssignments = providers.CaptureRuntimeConfiguration().Assignments ?? [];
        var oldAutosave = autosave.Capture();
        catalog.ArchiveActive(old, oldAssignments, oldAutosave);
        try
        {
            runtime.SwitchPausedWorld(target);
            stateFile.Save(runtime);
            providers.RestoreWorldAssignments(entry.Assignments);
            autosave.SelectWorld(entry.WorldId, entry.AutosaveSettings);
            jevPolicy.Initialize(runtime.JevEnabled, runtime.JevPolicyRevision);
            catalog.Select(entry.Id);
        }
        catch
        {
            WorldSelectionTelemetry.Failed(logger, entry.Id, "commit_failed");
            runtime.SwitchPausedWorld(old);
            stateFile.Save(runtime);
            providers.RestoreWorldAssignments(oldAssignments);
            autosave.SelectWorld(oldEntry.WorldId, oldAutosave);
            jevPolicy.Initialize(runtime.JevEnabled, runtime.JevPolicyRevision);
            catalog.Select(oldEntry.Id);
            throw;
        }
    }

    private void RequirePaused()
    {
        if (!runtime.Society.IsPaused)
            throw new InvalidOperationException("Pause the current world before switching worlds.");
    }
}
