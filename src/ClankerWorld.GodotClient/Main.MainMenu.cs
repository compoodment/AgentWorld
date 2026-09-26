using Godot;
using ClankerWorld.GodotClient.UI;
using System.Globalization;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Control mainMenuOverlay = new();
    private readonly PanelContainer mainMenuCard = new();
    private readonly Label mainMenuStatus = new();
    private readonly Button mainMenuContinueButton = new();
    private readonly Button mainMenuNewButton = new();
    private readonly Button mainMenuConnectButton = new();
    private readonly Button mainMenuLoadButton = new();
    private readonly Button menuQuitToMainButton = new();
    private readonly Button menuCreationButton = new();
    private readonly Button menuSaveWorldButton = new();
    private readonly Control worldMenuOverlay = new();
    private readonly PanelContainer worldMenuCard = new();
    private readonly Label worldMenuHeading = new();
    private readonly Label worldMenuStatus = new();
    private readonly LineEdit worldNameInput = new();
    private readonly LineEdit worldSeedInput = new();
    private readonly OptionButton worldSizeChoice = new();
    private readonly OptionButton worldWaterChoice = new();
    private readonly CheckBox worldWrapChoice = new();
    private readonly ItemList worldSelectionList = new();
    private readonly Button worldCreateButton = new();
    private readonly Button worldSelectButton = new();
    private CatalogWorld[] listedWorlds = [];
    private bool worldMenuBusy;
    private readonly ConfirmationDialog quitToMenuConfirmation = new();
    private bool isInWorld;
    private bool returnToMainMenu;
    private bool resumeWorldOnContinue;

    private void BuildMainMenu()
    {
        mainMenuOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mainMenuOverlay.MouseFilter = MouseFilterEnum.Stop;
        mainMenuOverlay.ZIndex = 180;
        AddChild(mainMenuOverlay);

        var background = new ColorRect
        {
            Color = new Color("0D151C"),
            MouseFilter = MouseFilterEnum.Stop,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mainMenuOverlay.AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mainMenuOverlay.AddChild(center);
        center.AddChild(mainMenuCard);

        var body = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
        body.AddThemeConstantOverride("separation", 12);
        var title = new Label { Text = "CLANKERWORLD", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 35);
        title.AddThemeColorOverride("font_color", new Color("F4F0E3"));
        body.AddChild(title);
        body.AddChild(new Label
        {
            Text = "A world shaped by the people who live in it",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color("AFC4BA"),
        });

        mainMenuStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        mainMenuStatus.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(mainMenuStatus);

        mainMenuContinueButton.Text = "Continue";
        StyleButton(mainMenuContinueButton, primary: true);
        mainMenuContinueButton.Pressed += () => _ = EnterWorldAsync();
        body.AddChild(mainMenuContinueButton);

        mainMenuNewButton.Text = "New World";
        StyleButton(mainMenuNewButton);
        mainMenuNewButton.Pressed += () => OpenWorldMenu(create: true);
        body.AddChild(mainMenuNewButton);

        mainMenuLoadButton.Text = "Load World";
        mainMenuLoadButton.TooltipText = "Choose a world. Named checkpoints remain inside each world's pause menu.";
        StyleButton(mainMenuLoadButton);
        mainMenuLoadButton.Pressed += () => OpenWorldMenu(create: false);
        body.AddChild(mainMenuLoadButton);

        var settings = new Button { Text = "Settings" };
        StyleButton(settings);
        settings.Pressed += OpenMainMenuSettings;
        body.AddChild(settings);

        mainMenuConnectButton.Text = "Connect / Pair development host";
        StyleButton(mainMenuConnectButton);
        mainMenuConnectButton.Pressed += () => _ = OpenMainMenuConnectionAsync();
        body.AddChild(mainMenuConnectButton);

        var quit = new Button { Text = "Quit Game" };
        StyleButton(quit);
        quit.Pressed += () => quitGameConfirmation.PopupCentered(new Vector2I(440, 170));
        body.AddChild(quit);

        AddPanelContents(mainMenuCard, body);
        mainMenuCard.CustomMinimumSize = new Vector2(440, 0);

        quitToMenuConfirmation.Title = "Quit to Main Menu?";
        quitToMenuConfirmation.DialogText = "Leave this world and return to the Main Menu? The simulation will remain paused until you continue it.";
        quitToMenuConfirmation.Confirmed += QuitToMainMenu;
        AddChild(quitToMenuConfirmation);
        BuildWorldMenu();
        RefreshMainMenuAvailability();
    }

    private void ShowMainMenu()
    {
        isInWorld = false;
        mainMenuOverlay.Show();
        RefreshMainMenuAvailability();
    }

    private void RefreshMainMenuAvailability()
    {
        var paired = !registeredEndpointInvalid && registration is not null && deviceKey is not null;
        mainMenuContinueButton.Disabled = !paired;
        mainMenuNewButton.Disabled = !paired;
        mainMenuLoadButton.Disabled = !paired;
        mainMenuConnectButton.Visible = !paired;
        mainMenuStatus.Text = paired
            ? "Continue your current world, create another, or load a different world."
            : "Connect or pair this device to the private development world. No model key is needed to open the game.";
    }

    private async Task EnterWorldAsync()
    {
        if (registration is null || deviceKey is null || registeredEndpointInvalid) return;
        mainMenuContinueButton.Disabled = true;
        var previousRefreshCount = successfulRefreshCount;
        await RefreshAsync();
        if (successfulRefreshCount == previousRefreshCount)
        {
            RefreshMainMenuAvailability();
            mainMenuStatus.Text = "Could not reach the development world. Check its connection and try Continue again.";
            return;
        }

        mainMenuOverlay.Hide();
        isInWorld = true;
        if (resumeWorldOnContinue)
        {
            resumeWorldOnContinue = false;
            await SetPausedAsync(paused: false);
        }
    }

    private void OpenMainMenuSettings()
    {
        returnToMainMenu = true;
        mainMenuOverlay.Hide();
        menuHeadingLabel.Text = "Game Settings";
        menuResumeButton.Text = "Back to Main Menu";
        SetWorldMenuActionsVisible(false);
        gameMenuPanel.Show();
        menuShade.Show();
        ShowSettingsSection(worldSpecific: false);
        ApplyResponsiveLayout();
    }

    private async Task OpenMainMenuConnectionAsync()
    {
        OpenMenuForSetup();
        if (registration is null && pendingPairing is null)
            await StartPairingAsync();
    }

    private void QuitToMainMenu()
    {
        // Opening the pause menu already committed a pause on the host. Stop
        // owner polling while the title screen is open, so no model work runs.
        if (observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused != true)
        {
            SetStatus("Wait for the host to confirm the pause before leaving this world.", good: false);
            return;
        }
        resumeWorldOnContinue = menuPausedWorld;
        CloseGameMenu();
        ShowMainMenu();
    }

    private void SetWorldMenuActionsVisible(bool visible)
    {
        menuSaveWorldButton.Visible = visible;
        worldSettingsButton.Visible = visible;
        menuCreationButton.Visible = visible;
        developerToggleButton.Visible = visible;
        menuQuitToMainButton.Visible = visible;
    }

    private void BuildWorldMenu()
    {
        worldMenuOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        worldMenuOverlay.MouseFilter = MouseFilterEnum.Stop;
        worldMenuOverlay.ZIndex = 210;
        AddChild(worldMenuOverlay);
        var shade = new ColorRect { Color = new Color("071015E0"), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        worldMenuOverlay.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        worldMenuOverlay.AddChild(center);
        center.AddChild(worldMenuCard);

        var body = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        body.AddThemeConstantOverride("separation", 8);
        worldMenuHeading.AddThemeFontSizeOverride("font_size", 24);
        body.AddChild(worldMenuHeading);
        worldMenuStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(worldMenuStatus);
        worldNameInput.PlaceholderText = "World name";
        worldNameInput.MaxLength = 80;
        body.AddChild(worldNameInput);
        worldSeedInput.PlaceholderText = "Generation seed";
        worldSeedInput.MaxLength = 100;
        var seedRow = new HBoxContainer();
        worldSeedInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        seedRow.AddChild(worldSeedInput);
        var reroll = new Button { Text = "Reroll seed" };
        StyleButton(reroll);
        reroll.Pressed += () => worldSeedInput.Text = Guid.NewGuid().ToString("N")[..12];
        seedRow.AddChild(reroll);
        body.AddChild(seedRow);
        worldSizeChoice.AddItem("Small · 256 × 128", 0);
        worldSizeChoice.AddItem("Medium · 512 × 256", 1);
        body.AddChild(worldSizeChoice);
        worldWaterChoice.AddItem("Less water · 35%", 35);
        worldWaterChoice.AddItem("Balanced water · 45%", 45);
        worldWaterChoice.AddItem("More water · 55%", 55);
        worldWaterChoice.Select(1);
        body.AddChild(worldWaterChoice);
        worldWrapChoice.Text = "Wrap east/west";
        worldWrapChoice.ButtonPressed = true;
        body.AddChild(worldWrapChoice);
        worldSelectionList.CustomMinimumSize = new Vector2(0, 240);
        worldSelectionList.ItemSelected += _ => worldSelectButton.Disabled = false;
        worldSelectionList.Hide();
        body.AddChild(worldSelectionList);
        worldCreateButton.Text = "Create World";
        StyleButton(worldCreateButton, primary: true);
        worldCreateButton.Pressed += () => _ = CreateSelectedWorldAsync();
        body.AddChild(worldCreateButton);
        worldSelectButton.Text = "Open World";
        StyleButton(worldSelectButton, primary: true);
        worldSelectButton.Pressed += () => _ = SelectListedWorldAsync();
        worldSelectButton.Hide();
        body.AddChild(worldSelectButton);
        var back = new Button { Text = "Back" };
        StyleButton(back);
        back.Pressed += () => { if (!worldMenuBusy) worldMenuOverlay.Hide(); };
        body.AddChild(back);
        AddPanelContents(worldMenuCard, body);
        worldMenuCard.CustomMinimumSize = new Vector2(480, 0);
        worldMenuOverlay.Hide();
    }

    private void OpenWorldMenu(bool create)
    {
        if (registration is null || deviceKey is null || registeredEndpointInvalid) return;
        worldMenuHeading.Text = create ? "New World" : "Load World";
        worldMenuStatus.Text = create
            ? "Choose a seed and size. The new world opens paused at its empty camp; add four founders before starting time."
            : "Choose a world. The current world is saved before switching.";
        worldNameInput.Visible = create;
        worldSeedInput.GetParent<Control>().Visible = create;
        worldSizeChoice.Visible = create;
        worldWaterChoice.Visible = create;
        worldWrapChoice.Visible = create;
        worldCreateButton.Visible = create;
        worldSelectionList.Visible = !create;
        worldSelectButton.Visible = !create;
        worldSelectButton.Disabled = true;
        worldNameInput.Text = "New World";
        worldSeedInput.Text = Guid.NewGuid().ToString("N")[..12];
        worldMenuOverlay.Show();
        if (!create) _ = RefreshWorldListAsync();
    }

    private async Task RefreshWorldListAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        worldMenuStatus.Text = "Loading worlds…";
        try
        {
            var catalog = await ownerApi.ListWorldsAsync(ResolveWorldUri(), authority,
                deviceId, signer, CancellationToken.None);
            listedWorlds = catalog.Worlds.OrderByDescending(world => world.Id == catalog.ActiveId)
                .ThenByDescending(world => world.UpdatedUtc).ToArray();
            worldSelectionList.Clear();
            foreach (var world in listedWorlds)
                worldSelectionList.AddItem(world.Name +
                    (world.Id == catalog.ActiveId ? " · current" : "") +
                    " · " + world.UpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
            worldMenuStatus.Text = listedWorlds.Length == 0 ? "No worlds yet." :
                "Choose a world. Opening it leaves the current one paused and saved.";
        }
        catch (Exception exception)
        {
            worldMenuStatus.Text = "Could not list worlds: " + FriendlyFailure(exception);
        }
    }

    private async Task CreateSelectedWorldAsync()
    {
        if (worldMenuBusy || !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var name = worldNameInput.Text.Trim();
        var seed = worldSeedInput.Text.Trim();
        if (name.Length is < 1 or > 80 || seed.Length is < 1 or > 100 ||
            name.Any(char.IsControl) || seed.Any(char.IsControl))
        {
            worldMenuStatus.Text = "Enter a world name (1–80 characters) and seed (1–100 characters).";
            return;
        }
        worldMenuBusy = true;
        worldCreateButton.Disabled = true;
        worldMenuStatus.Text = "Generating world…";
        try
        {
            await ownerApi.SetPausedAsync(ResolveWorldUri(), authority, deviceId, true,
                signer, CancellationToken.None);
            var action = new OwnerWorldCreationAction(name, seed,
                worldSizeChoice.GetSelectedId() == 1 ? "Medium" : "Small",
                worldWaterChoice.GetSelectedId(), worldWrapChoice.ButtonPressed);
            await ownerApi.CreateWorldAsync(ResolveWorldUri(), authority, deviceId,
                action, signer, CancellationToken.None);
            observationSession.ResetAfterLoad();
            resumeWorldOnContinue = false;
            worldMenuOverlay.Hide();
            await EnterWorldAsync();
        }
        catch (Exception exception)
        {
            worldMenuStatus.Text = "Could not create world: " + FriendlyFailure(exception);
        }
        finally
        {
            worldMenuBusy = false;
            worldCreateButton.Disabled = false;
        }
    }

    private async Task SelectListedWorldAsync()
    {
        if (worldMenuBusy || worldSelectionList.GetSelectedItems() is not { Length: 1 } selected ||
            selected[0] < 0 || selected[0] >= listedWorlds.Length ||
            !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        worldMenuBusy = true;
        worldSelectButton.Disabled = true;
        worldMenuStatus.Text = "Opening world…";
        try
        {
            await ownerApi.SetPausedAsync(ResolveWorldUri(), authority, deviceId, true,
                signer, CancellationToken.None);
            await ownerApi.SelectWorldAsync(ResolveWorldUri(), authority, deviceId,
                listedWorlds[selected[0]].Id, signer, CancellationToken.None);
            observationSession.ResetAfterLoad();
            resumeWorldOnContinue = false;
            worldMenuOverlay.Hide();
            await EnterWorldAsync();
        }
        catch (Exception exception)
        {
            worldMenuStatus.Text = "Could not open world: " + FriendlyFailure(exception);
        }
        finally
        {
            worldMenuBusy = false;
            worldSelectButton.Disabled = false;
        }
    }
}
