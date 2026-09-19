using System.Net.Http;
using AgentWorld.GodotClient.Protocol;
using Godot;

namespace AgentWorld.GodotClient;

public partial class Main : Control
{
    private const int TileSize = 42;

    private readonly System.Net.Http.HttpClient httpClient = new();
    private readonly WorldObservationSession session = new();
    private readonly Label statusLabel = new();
    private readonly GridContainer worldGrid = new();
    private readonly Label actorLabel = new();
    private readonly Label resourceLabel = new();
    private readonly RichTextLabel eventLog = new();
    private bool isRefreshing;

    public override void _Ready()
    {
        BuildLayout();
        _ = RefreshAsync();

        var timer = new Godot.Timer
        {
            WaitTime = 1,
            Autostart = true,
        };
        timer.Timeout += () => _ = RefreshAsync();
        AddChild(timer);
    }

    public override void _ExitTree()
    {
        httpClient.Dispose();
        base._ExitTree();
    }

    private async Task RefreshAsync()
    {
        if (isRefreshing)
        {
            return;
        }

        isRefreshing = true;
        try
        {
            var observation = await new WorldObservationClient(httpClient).ReconnectAsync(
                ResolveWorldUri(),
                session.EventCursor,
                CancellationToken.None);
            if (!session.TryAccept(observation, out var failure))
            {
                ShowHeldState(failure);
                return;
            }

            Render(observation.Baseline.Snapshot, observation.Baseline.Events.Events);
            statusLabel.Text = $"connected · protocol {observation.Handshake.Protocol.Major}.{observation.Handshake.Protocol.Minor} · tick {observation.Baseline.Snapshot.WorldTick} · read-only";
            statusLabel.Modulate = new Color("B9E8C5");
        }
        catch (Exception exception)
        {
            ShowHeldState(exception.Message);
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = new Label { Text = "AGENTWORLD · GODOT OBSERVER" };
        title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(title);
        statusLabel.Text = "connecting to authoritative world…";
        root.AddChild(statusLabel);

        var content = new HBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 16);
        root.AddChild(content);

        worldGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        worldGrid.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        worldGrid.AddThemeConstantOverride("h_separation", 2);
        worldGrid.AddThemeConstantOverride("v_separation", 2);
        var mapPanel = NewPanel("World snapshot", worldGrid);
        mapPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.AddChild(mapPanel);

        var details = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(330, 0),
        };
        details.AddThemeConstantOverride("separation", 12);
        details.AddChild(NewPanel("Actor", actorLabel));
        details.AddChild(NewPanel("Resources", resourceLabel));
        var eventPanel = NewPanel("Ordered event history", eventLog);
        eventLog.FitContent = false;
        eventLog.CustomMinimumSize = new Vector2(0, 220);
        eventLog.SizeFlagsVertical = SizeFlags.ExpandFill;
        details.AddChild(eventPanel);
        content.AddChild(details);
    }

    private void Render(WorldSnapshot snapshot, IReadOnlyList<WorldEvent> appendedEvents)
    {
        RenderMap(snapshot);
        actorLabel.Text = $"{snapshot.Actor.Id}\nposition {snapshot.Actor.Position.X}, {snapshot.Actor.Position.Y}\nhunger {snapshot.Actor.HungerBasisPoints} bp\nenergy {snapshot.Actor.EnergyBasisPoints} bp\nfood {snapshot.Actor.FoodItems} · wood {snapshot.Actor.WoodItems}";
        resourceLabel.Text = string.Join(
            '\n',
            snapshot.Resources.Select(resource => $"{resource.Id}: {resource.State}"));

        foreach (var worldEvent in appendedEvents)
        {
            eventLog.AppendText($"[color=#B9E8C5]#{worldEvent.EventId} · tick {worldEvent.WorldTick}[/color]\n{worldEvent.Kind}: {worldEvent.Detail}\n\n");
        }
    }

    private void RenderMap(WorldSnapshot snapshot)
    {
        foreach (var child in worldGrid.GetChildren())
        {
            child.QueueFree();
        }

        worldGrid.Columns = snapshot.Tiles.Max(tile => tile.X) + 1;
        var objects = snapshot.Objects.ToDictionary(item => PositionKey(item.Position));
        var resources = snapshot.Resources.ToDictionary(item => PositionKey(item.Position));
        var actorPosition = PositionKey(snapshot.Actor.Position);
        foreach (var tile in snapshot.Tiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X))
        {
            var key = $"{tile.X},{tile.Y}";
            var cell = new ColorRect
            {
                Color = TerrainColor(tile.Terrain),
                CustomMinimumSize = new Vector2(TileSize, TileSize),
                TooltipText = $"{tile.Terrain} at {key}",
            };
            var marker = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            marker.AddThemeFontSizeOverride("font_size", 20);
            marker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            marker.Text = key == actorPosition ? "✦" : resources.ContainsKey(key) ? "●" : objects.ContainsKey(key) ? "◆" : string.Empty;
            cell.AddChild(marker);
            worldGrid.AddChild(cell);
        }
    }

    private static PanelContainer NewPanel(string title, Control? content = null)
    {
        var panel = new PanelContainer();
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(heading);
        if (content is not null)
        {
            body.AddChild(content);
        }

        margin.AddChild(body);
        panel.AddChild(margin);
        return panel;
    }

    private static Uri ResolveWorldUri()
    {
        var configuredUrl = ProjectSettings.GetSetting("agentworld/world_url", "http://127.0.0.1:5188").AsString();
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--world-url=", StringComparison.Ordinal));
        var candidate = argument is null ? configuredUrl : argument["--world-url=".Length..];
        return Uri.TryCreate(candidate, UriKind.Absolute, out var worldUri)
            ? worldUri
            : throw new InvalidOperationException("World URL must be an absolute HTTP(S) URL.");
    }

    private void ShowHeldState(string reason)
    {
        var heldTick = session.Current?.Baseline.Snapshot.WorldTick;
        statusLabel.Text = heldTick is null
            ? $"disconnected · {reason}"
            : $"disconnected · holding tick {heldTick} · {reason}";
        statusLabel.Modulate = new Color("F0B6A6");
    }

    private static string PositionKey(WorldPosition position) => $"{position.X},{position.Y}";

    private static Color TerrainColor(string terrain) => terrain switch
    {
        "meadow" => new Color("5F8F5B"),
        "water" => new Color("4B7FA7"),
        "mountain" => new Color("756D68"),
        _ => new Color("9B5463"),
    };
}
