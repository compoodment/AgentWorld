using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Compact, indexed terrain shared by the local camera and the overview.</summary>
public sealed class WorldTerrainMap
{
    private readonly byte[] terrain;

    private WorldTerrainMap(int width, int height, byte[] terrain)
    {
        Width = width;
        Height = height;
        this.terrain = terrain;
    }

    public int Width { get; }
    public int Height { get; }

    public static WorldTerrainMap FromTiles(IReadOnlyList<OwnerWorldTile> tiles, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var terrain = new byte[checked(width * height)];
        foreach (var tile in tiles)
        {
            if (tile.X < 0 || tile.X >= width || tile.Y < 0 || tile.Y >= height) continue;
            terrain[tile.Y * width + tile.X] = tile.Terrain switch
            {
                "meadow" => 1,
                "water" => 2,
                "mountain" => 3,
                "river" => 4,
                "lake" => 5,
                "ocean" => 6,
                "sand" => 7,
                "forest" => 8,
                "snow" => 9,
                "peak" => 10,
                _ => 0,
            };
        }
        return new WorldTerrainMap(width, height, terrain);
    }

    public byte At(int x, int y) => terrain[y * Width + x];

    public static Color ColorFor(byte kind) => kind switch
    {
        1 => WorldMapPalette.TerrainColor("meadow"),
        2 => WorldMapPalette.TerrainColor("water"),
        3 => WorldMapPalette.TerrainColor("mountain"),
        4 => new Color("4786AB"),
        5 => new Color("598FB3"),
        6 => new Color("325F89"),
        7 => new Color("BAA77B"),
        8 => new Color("426D4B"),
        9 => new Color("CCD7D1"),
        10 => new Color("999B9A"),
        _ => new Color("9B5463"),
    };
}
