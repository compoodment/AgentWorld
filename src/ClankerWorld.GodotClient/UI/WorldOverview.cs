using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>
/// A data-drawn atlas of the current world, not a second set of terrain art.
/// Its bright rectangle is the part visible in the main world view.
/// </summary>
public partial class WorldOverview : Control
{
    private Texture2D? atlasTexture;
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
        TextureFilter = TextureFilterEnum.Nearest;
        CustomMinimumSize = new Vector2(230, 130);
        TooltipText = "Click to jump; drag the bright camera rectangle to move the world view.";
        Resized += QueueRedraw;
    }

    public void SetWorld(WorldTerrainMap world)
    {
        mapWidth = world.Width;
        mapHeight = world.Height;
        // The overview is data art, not a second sprite set. Each atlas pixel
        // summarizes its part of the world, so redraw cost is bounded by the
        // atlas resolution instead of millions of canvas rectangles.
        var atlasWidth = Math.Min(mapWidth, 256);
        var atlasHeight = Math.Min(mapHeight, 128);
        var votes = new int[checked(atlasWidth * atlasHeight * 11)];
        for (var y = 0; y < mapHeight; y++)
        {
            var atlasY = y * atlasHeight / mapHeight;
            for (var x = 0; x < mapWidth; x++)
            {
                var atlasX = x * atlasWidth / mapWidth;
                votes[((atlasY * atlasWidth + atlasX) * 11) + world.At(x, y)]++;
            }
        }
        var image = Image.CreateEmpty(atlasWidth, atlasHeight, false, Image.Format.Rgba8);
        for (var y = 0; y < atlasHeight; y++)
        {
            for (var x = 0; x < atlasWidth; x++)
            {
                var offset = (y * atlasWidth + x) * 11;
                var dominant = 0;
                for (var kind = 1; kind < 11; kind++)
                    if (votes[offset + kind] > votes[offset + dominant]) dominant = kind;
                image.SetPixel(x, y, WorldTerrainMap.ColorFor((byte)dominant));
            }
        }
        atlasTexture = ImageTexture.CreateFromImage(image);
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
        if (atlasTexture is not null) DrawTextureRect(atlasTexture, atlas, tile: false);

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
