using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Draws only camera-visible terrain; it never creates a node per tile.</summary>
public partial class WorldTerrainLayer : Control
{
    private WorldTerrainMap? world;
    private Rect2 visibleTiles;
    private int tileSize;
    private int tileGap;

    public int VisibleTileCount { get; private set; }

    public WorldTerrainLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetWorld(WorldTerrainMap map)
    {
        world = map;
        QueueRedraw();
    }

    public void SetCamera(Rect2 visible, int size, int gap)
    {
        visibleTiles = visible;
        tileSize = size;
        tileGap = gap;
        var bounds = VisibleBounds();
        VisibleTileCount = bounds.Width * bounds.Height;
        QueueRedraw();
    }

    private (int Left, int Top, int Width, int Height) VisibleBounds()
    {
        if (world is null || tileSize <= 0) return (0, 0, 0, 0);
        var left = Math.Clamp(Mathf.FloorToInt(visibleTiles.Position.X), 0, world.Width);
        var top = Math.Clamp(Mathf.FloorToInt(visibleTiles.Position.Y), 0, world.Height);
        var right = Math.Clamp(Mathf.CeilToInt(visibleTiles.End.X), left, world.Width);
        var bottom = Math.Clamp(Mathf.CeilToInt(visibleTiles.End.Y), top, world.Height);
        return (left, top, right - left, bottom - top);
    }

    public override void _Draw()
    {
        if (world is null) return;
        var bounds = VisibleBounds();
        var stride = tileSize + tileGap;
        for (var y = bounds.Top; y < bounds.Top + bounds.Height; y++)
        {
            for (var x = bounds.Left; x < bounds.Left + bounds.Width; x++)
            {
                var kind = world.At(x, y);
                var position = new Vector2(x * stride, y * stride);
                DrawRect(new Rect2(position, new Vector2(tileSize, tileSize)), WorldTerrainMap.ColorFor(kind));
                if (tileSize >= 28 && kind is 2 or 3 or 4 or 10)
                {
                    var marker = kind is 3 or 10 ? "▲" : "≈";
                    DrawString(ThemeDB.FallbackFont, position + new Vector2(tileSize * 0.4f, tileSize * 0.65f),
                        marker, fontSize: Math.Clamp(tileSize / 5, 12, 28), modulate: new Color("E6F0E8"));
                }
            }
        }
    }
}
