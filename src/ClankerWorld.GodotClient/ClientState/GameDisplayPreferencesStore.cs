using System.Text.Json;

namespace ClankerWorld.GodotClient.ClientState;

/// <summary>
/// Installation-local display choices. These do not alter world time or save data.
/// </summary>
public sealed record GameDisplayPreferences(bool UseTwelveHourClock = false);

public sealed class GameDisplayPreferencesStore(string path)
{
    private readonly string path = Path.GetFullPath(path);

    public GameDisplayPreferences Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<GameDisplayPreferences>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(GameDisplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var directory = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException("The game settings path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
