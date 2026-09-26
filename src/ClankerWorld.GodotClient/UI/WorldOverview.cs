using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>
/// A data-drawn atlas of the current world, not a second set of terrain art.
/// Its bright rectangle is the part visible in the main world view.
/// </summary>
public partial class WorldOverview : Control
{
    private IReadOnlyList<OwnerWorldTile> tiles = [];
    private int mapWidth;
    private int mapHeight;
    private Rect2 visibleTiles;
    private bool dragging;
    private Vector2 dragOffset;

    public event Action<Vector2>? CenterRequested;

    public Rect2 VisibleTiles => visibleTiles;

    public WorldOverview()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(230, 130);
        TooltipText = "Click to jump; drag the bright camera rectangle to move the world view.";
        Resized += QueueRedraw;
    }

    public void SetWorld(IReadOnlyList<OwnerWorldTile> worldTiles, int width, int height)
    {
        tiles = worldTiles;
        mapWidth = width;
        mapHeight = height;
        QueueRedraw();
    }

    public void SetVisibleTiles(Rect2 bounds)
    {
        visibleTiles = bounds;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("101A1E"));
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return;
        }

        var atlas = AtlasRect();
        DrawRect(atlas, new Color("273A3D"));
        foreach (var tile in tiles)
        {
            if (tile.X < 0 || tile.X >= mapWidth || tile.Y < 0 || tile.Y >= mapHeight)
            {
                continue;
            }

            DrawRect(new Rect2(
                atlas.Position + new Vector2(tile.X * atlas.Size.X / mapWidth, tile.Y * atlas.Size.Y / mapHeight),
                new Vector2(atlas.Size.X / mapWidth + 0.5f, atlas.Size.Y / mapHeight + 0.5f)),
                WorldMapPalette.TerrainColor(tile.Terrain));
        }

        DrawRect(atlas, new Color("AFC4BA"), filled: false, width: 1);
        var view = new Rect2(
            atlas.Position + new Vector2(visibleTiles.Position.X * atlas.Size.X / mapWidth,
                visibleTiles.Position.Y * atlas.Size.Y / mapHeight),
            new Vector2(Math.Max(2, visibleTiles.Size.X * atlas.Size.X / mapWidth),
                Math.Max(2, visibleTiles.Size.Y * atlas.Size.Y / mapHeight)));
        DrawRect(view, new Color("FFF0B5", 0.15f));
        DrawRect(view, new Color("FFF0B5"), filled: false, width: 2);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            dragging = button.Pressed && AtlasRect().HasPoint(button.Position);
            if (dragging)
            {
                var point = ToTilePoint(button.Position);
                dragOffset = visibleTiles.HasPoint(point) ? point - visibleTiles.GetCenter() : Vector2.Zero;
                CenterRequested?.Invoke(point - dragOffset);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion && dragging)
        {
            CenterRequested?.Invoke(ToTilePoint(motion.Position) - dragOffset);
            AcceptEvent();
        }
    }

    private Vector2 ToTilePoint(Vector2 position)
    {
        var atlas = AtlasRect();
        return new Vector2(
            Math.Clamp((position.X - atlas.Position.X) * mapWidth / atlas.Size.X, 0, mapWidth),
            Math.Clamp((position.Y - atlas.Position.Y) * mapHeight / atlas.Size.Y, 0, mapHeight));
    }

    private Rect2 AtlasRect()
    {
        var available = Size - new Vector2(12, 12);
        var scale = Math.Min(available.X / mapWidth, available.Y / mapHeight);
        var size = new Vector2(mapWidth * scale, mapHeight * scale);
        return new Rect2((Size - size) / 2, size);
    }
}
