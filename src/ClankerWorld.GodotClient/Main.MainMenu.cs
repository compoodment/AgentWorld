using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Control mainMenuOverlay = new();
    private readonly PanelContainer mainMenuCard = new();
    private readonly Label mainMenuStatus = new();
    private readonly Button mainMenuContinueButton = new();
    private readonly Button mainMenuConnectButton = new();
    private readonly Button mainMenuLoadButton = new();
    private readonly Button menuQuitToMainButton = new();
    private readonly Button menuCreationButton = new();
    private readonly Button menuSaveWorldButton = new();
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

        var newWorld = new Button { Text = "New World", Disabled = true };
        newWorld.TooltipText = "The world-size, climate and preview flow is under construction.";
        StyleButton(newWorld);
        body.AddChild(newWorld);

        mainMenuLoadButton.Text = "Load Save";
        mainMenuLoadButton.TooltipText = "Load a named checkpoint of the current world. The current state is preserved first.";
        StyleButton(mainMenuLoadButton);
        mainMenuLoadButton.Pressed += () => _ = OpenManualSavesAsync(loadMode: true);
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
        mainMenuLoadButton.Disabled = !paired;
        mainMenuConnectButton.Visible = !paired;
        mainMenuStatus.Text = paired
            ? "Continue the current development world. World generation and multiple saves are being built."
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
}
