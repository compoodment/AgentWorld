using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Control manualSaveOverlay = new();
    private readonly PanelContainer manualSaveCard = new();
    private readonly Label manualSaveHeading = new();
    private readonly Label manualSaveStatus = new();
    private readonly LineEdit manualSaveName = new();
    private readonly Button manualSaveCreateButton = new();
    private readonly ItemList manualSaveList = new();
    private readonly Button manualSaveLoadButton = new();
    private readonly ConfirmationDialog manualSaveLoadConfirmation = new();
    private ManualWorldSave[] listedManualSaves = [];
    private bool manualSaveLoadMode;

    private void BuildManualSavesPanel()
    {
        manualSaveOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        manualSaveOverlay.MouseFilter = MouseFilterEnum.Stop;
        manualSaveOverlay.ZIndex = 220;
        AddChild(manualSaveOverlay);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.78f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        manualSaveOverlay.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        manualSaveOverlay.AddChild(center);
        center.AddChild(manualSaveCard);
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
        body.AddThemeConstantOverride("separation", 10);
        manualSaveHeading.AddThemeFontSizeOverride("font_size", 24);
        body.AddChild(manualSaveHeading);
        manualSaveStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(manualSaveStatus);
        manualSaveName.PlaceholderText = "Name this save";
        manualSaveName.MaxLength = 80;
        body.AddChild(manualSaveName);
        manualSaveCreateButton.Text = "Save World";
        StyleButton(manualSaveCreateButton, primary: true);
        manualSaveCreateButton.Pressed += () => _ = CreateManualSaveAsync();
        body.AddChild(manualSaveCreateButton);
        manualSaveList.CustomMinimumSize = new Vector2(0, 250);
        manualSaveList.ItemSelected += _ => manualSaveLoadButton.Disabled = false;
        body.AddChild(manualSaveList);
        manualSaveLoadButton.Text = "Load selected save";
        StyleButton(manualSaveLoadButton, primary: true);
        manualSaveLoadButton.Pressed += ConfirmManualSaveLoad;
        body.AddChild(manualSaveLoadButton);
        var close = new Button { Text = "Back" };
        StyleButton(close);
        close.Pressed += () => manualSaveOverlay.Hide();
        body.AddChild(close);
        AddPanelContents(manualSaveCard, body);
        manualSaveCard.CustomMinimumSize = new Vector2(470, 0);
        manualSaveLoadConfirmation.Title = "Load this save?";
        manualSaveLoadConfirmation.Confirmed += () => _ = LoadSelectedManualSaveAsync();
        AddChild(manualSaveLoadConfirmation);
        manualSaveOverlay.Hide();
    }

    private async Task OpenManualSavesAsync(bool loadMode)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        if (!loadMode && observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused != true)
        {
            SetStatus("Wait for the world to pause before saving.", good: false);
            return;
        }
        manualSaveLoadMode = loadMode;
        manualSaveHeading.Text = loadMode ? "Load Save" : "Save World";
        manualSaveStatus.Text = loadMode
            ? "Choose a named checkpoint. Your current state will be saved before loading it."
            : "Create a named checkpoint of this paused world.";
        manualSaveName.Visible = !loadMode;
        manualSaveCreateButton.Visible = !loadMode;
        manualSaveList.Visible = loadMode;
        manualSaveLoadButton.Visible = loadMode;
        manualSaveLoadButton.Disabled = true;
        manualSaveOverlay.Show();
        if (!loadMode) return;
        try
        {
            listedManualSaves = await ownerApi.ListManualSavesAsync(ResolveWorldUri(), authority,
                deviceId, signer, CancellationToken.None);
            manualSaveList.Clear();
            foreach (var save in listedManualSaves)
                manualSaveList.AddItem($"{save.Name} · tick {save.WorldTick} · {save.CreatedUtc.ToLocalTime():g}");
            if (listedManualSaves.Length == 0)
                manualSaveStatus.Text = "No named saves yet. Continue the world and use Pause Menu → Save World.";
        }
        catch (Exception exception)
        {
            manualSaveStatus.Text = "Could not list saves: " + FriendlyFailure(exception);
        }
    }

    private async Task CreateManualSaveAsync()
    {
        var name = manualSaveName.Text.Trim();
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
        {
            manualSaveStatus.Text = "Choose a name of 1–80 printable characters.";
            return;
        }
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            var saved = await ownerApi.CreateManualSaveAsync(ResolveWorldUri(), authority,
                deviceId, name, signer, CancellationToken.None);
            manualSaveOverlay.Hide();
            manualSaveName.Text = string.Empty;
            return $"Saved world at tick {saved.WorldTick}.";
        });
    }

    private void ConfirmManualSaveLoad()
    {
        if (!manualSaveLoadMode || manualSaveList.GetSelectedItems() is not { Length: 1 } selected ||
            selected[0] < 0 || selected[0] >= listedManualSaves.Length) return;
        manualSaveLoadConfirmation.DialogText = $"Load ‘{listedManualSaves[selected[0]].Name}’? The current world will be saved first, and the loaded world will remain paused.";
        manualSaveLoadConfirmation.PopupCentered(new Vector2I(480, 180));
    }

    private async Task LoadSelectedManualSaveAsync()
    {
        if (manualSaveList.GetSelectedItems() is not { Length: 1 } selected ||
            selected[0] < 0 || selected[0] >= listedManualSaves.Length ||
            !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var save = listedManualSaves[selected[0]];
        await RunOwnerActionAsync(async () =>
        {
            await ownerApi.SetPausedAsync(ResolveWorldUri(), authority, deviceId, true,
                signer, CancellationToken.None);
            // A lost response cannot tell us whether the host committed the
            // rewind. Reconnect from zero either way, instead of rejecting a
            // valid older world as a regressing observation.
            observationSession.ResetAfterLoad();
            knownEvents.Clear();
            var loaded = await ownerApi.LoadManualSaveAsync(ResolveWorldUri(), authority,
                deviceId, save.Id, signer, CancellationToken.None);
            selectedInhabitantId = null;
            renderedMapSnapshot = null;
            manualSaveOverlay.Hide();
            mainMenuOverlay.Hide();
            isInWorld = true;
            resumeWorldOnContinue = false;
            menuPausedWorld = false;
            return $"Loaded {save.Name} at tick {loaded.WorldTick}; the previous state is saved too.";
        });
    }
}
