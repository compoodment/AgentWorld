using Godot;

namespace ClankerWorld.GodotClient.UI;

internal static class WorldMapPalette
{
    public static Color TerrainColor(string terrain) => terrain switch
    {
        "meadow" => new Color("5F8F5B"),
        "water" => new Color("4B7FA7"),
        "mountain" => new Color("756D68"),
        _ => new Color("9B5463"),
    };
}
