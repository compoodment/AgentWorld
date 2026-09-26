using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The deliberately practical Phase 2 owner client. It renders only signed,
/// server-issued world projections; all control buttons submit a one-use
/// device-key proof to the server and never mutate a local simulation copy.
/// </summary>
public partial class Main : Control
{
    private const int DefaultTileSize = 96;
    private const int TileGap = 2;
    private const int RefreshSeconds = 1;

    private readonly System.Net.Http.HttpClient httpClient = new();
    private readonly OwnerWorldApi ownerApi;
    private readonly OwnerWorldObservationSession observationSession = new();
    private readonly OwnerDeviceRegistrationStore registrationStore = new();
    private readonly OwnerPendingSubmissionStore pendingSubmissionStore = new(
        ProjectSettings.GlobalizePath("user://owner-pending-submission.json"));
    private readonly GameDisplayPreferencesStore displayPreferencesStore = new(
        ProjectSettings.GlobalizePath("user://game-display-preferences.json"));
    private readonly Dictionary<long, OwnerWorldEvent> knownEvents = [];
    private readonly Dictionary<string, OwnerWorldPosition> renderedInhabitantPositions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> mapObjectVisuals = new(StringComparer.Ordinal);

    private readonly Label statusLabel = new();
    private readonly PanelContainer statusToast = new();
    private readonly PanelContainer eventNoticePanel = new();
    private readonly Button eventNoticeButton = new();
    private readonly Godot.Timer eventNoticeTimer = new();
    private readonly PanelContainer connectionPanel = new();
    private readonly Button gameSettingsButton = new();
    private readonly Button worldSettingsButton = new();
    private readonly Button gameSettingsCategoryButton = new();
    private readonly Button worldSettingsCategoryButton = new();
    private readonly VBoxContainer gameSettingsContent = new();
    private readonly VBoxContainer worldSettingsContent = new();
    private readonly LineEdit worldUrlInput = new();
    private readonly Button connectButton = new();
    private readonly Button pairAgainButton = new();
    private readonly PanelContainer cognitionSettingsPanel = new();
    private readonly OptionButton cognitionRoleChoice = new();
    private readonly OptionButton cognitionTargetChoice = new();
    private readonly OptionButton cognitionProviderChoice = new();
    private readonly LineEdit cognitionModelInput = new();
    private readonly LineEdit cognitionApiKeyInput = new();
    private readonly Label cognitionConfigurationStatus = new();
    private readonly Label cognitionCredentialHint = new();
    private readonly Button saveCognitionProviderButton = new();
    private readonly Button forgetCognitionCredentialButton = new();
    private readonly Button refreshCognitionProviderButton = new();
    private readonly PanelContainer pairingPanel = new();
    private readonly Label pairingInstructionLabel = new();
    private readonly Label pairingCodeLabel = new();
    private readonly Label pairingIdLabel = new();
    private readonly Label pairingExpiryLabel = new();
    private readonly Button pairButton = new();
    private readonly Button forgetRegistrationButton = new();

    private readonly HBoxContainer topBar = new();
    private readonly Button mapButton = new();
    private readonly Button worldInfoButton = new();
    private readonly Label clockLabel = new();
    private readonly Label climateLabel = new();
    private readonly Button inhabitantsButton = new();
    private readonly Button eventsButton = new();
    private readonly Button settlementButton = new();
    private readonly Button menuButton = new();
    private readonly GridContainer worldGrid = new();
    private readonly Control mapCanvas = new();
    private readonly Control mapStage = new();
    private readonly PanelContainer worldOverviewPanel = new();
    private readonly WorldOverview worldOverview = new();
    private readonly Control objectLayer = new();
    private readonly Control entityLayer = new();
    private readonly Label rosterSummaryLabel = new();
    private readonly PanelContainer selectedInhabitantCard = new();
    private readonly Label selectedActorNameLabel = new();
    private readonly Label selectedActorSummaryLabel = new();
    private readonly Button clearSelectionButton = new();
    private readonly Button familyTreeButton = new();
    private readonly PanelContainer familyTreePanel = new();
    private readonly FamilyTreeView familyTreeView = new();
    private readonly Label familyTreeStatus = new();
    private readonly ItemList inhabitantList = new();
    private readonly RichTextLabel inhabitantDetails = new();
    private readonly RichTextLabel inhabitantSocialDetails = new();
    private readonly RichTextLabel privateThoughtHistory = new();
    private readonly Button memoriesButton = new();
    private readonly PanelContainer memoriesPanel = new();
    private readonly RichTextLabel memoryHistory = new();
    private readonly RichTextLabel worldDetails = new();
    private readonly RichTextLabel worldInfoText = new();
    private readonly RichTextLabel eventLog = new();
    private readonly PanelContainer rosterPanel = new();
    private readonly PanelContainer eventsPanel = new();
    private readonly PanelContainer settlementPanel = new();
    private readonly PanelContainer worldInfoPanel = new();
    private readonly PanelContainer gameMenuPanel = new();
    private readonly PanelContainer settingsPanel = new();
    private readonly ColorRect menuShade = new();
    private readonly Label menuHeadingLabel = new();
    private readonly Button menuResumeButton = new();
    private readonly Button quitGameButton = new();
    private readonly ConfirmationDialog quitGameConfirmation = new();
    private readonly CheckBox fullscreenToggle = new();
    private readonly OptionButton resolutionChoice = new();
    private readonly OptionButton clockFormatChoice = new();
    private readonly OptionButton lifePaceChoice = new();
    private readonly Button applyLifePaceButton = new();
    private int? lastObservedLifePace;
    private string? lastLifePaceWorldId;

    private readonly Button pauseButton = new();
    private readonly OptionButton instructionKind = new();
    private readonly LineEdit instructionText = new();
    private readonly Button submitInstructionButton = new();
    private readonly Label pendingSubmissionLabel = new();
    private readonly Button retryPendingSubmissionButton = new();
    private readonly Button forgetPendingSubmissionButton = new();
    private readonly OptionButton authoringKind = new();
    private readonly LineEdit authoringId = new();
    private readonly LineEdit authoringValue = new();
    private readonly LineEdit authoringSecondaryValue = new();
    private readonly SpinBox authoringX = new();
    private readonly SpinBox authoringY = new();
    private readonly CheckBox authoringRenewable = new();
    private readonly Button submitAuthoringButton = new();
    private readonly Label authoringHintLabel = new();
    private readonly LineEdit pairingApprovalId = new();
    private readonly LineEdit pairingApprovalCode = new();
    private readonly Button approvePairingButton = new();
    private readonly Button refreshDevicesButton = new();
    private readonly ItemList pairedDeviceList = new();
    private readonly LineEdit revokeDeviceId = new();
    private readonly Button revokeDeviceButton = new();
    private readonly Button developerToggleButton = new();
    private readonly ScrollContainer developerScroll = new();
    private readonly VBoxContainer developerBody = new();

    private OwnerDeviceKey? deviceKey;
    private OwnerDeviceRegistration? registration;
    private OwnerPairingStart? pendingPairing;
    private Uri? pendingPairingOrigin;
    private OwnerDevice[] pairedDevices = [];
    private OwnerProviderConfigurationStatus? providerConfiguration;
    private OwnerPendingSubmission? pendingSubmission;
    private string? selectedInhabitantId;
    private bool isRefreshing;
    private bool isPairingOperation;
    private bool isOwnerAction;
    private bool registeredEndpointInvalid;
    private bool menuPausedWorld;
    private OwnerWorldSnapshot? renderedMapSnapshot;
    private int currentTileSize = DefaultTileSize;
    private float cameraZoom = 1;
    private Vector2 cameraCenterTiles;
    private string? cameraWorldId;
    private bool draggingMap;
    private GameDisplayPreferences displayPreferences = new();
    private string? notificationWorldId;
    private long lastNotificationEventId;
    private long? visibleNoticeEventId;

    public Main()
    {
        ownerApi = new OwnerWorldApi(httpClient);
    }

    public override void _Ready()
    {
        displayPreferences = displayPreferencesStore.Load();
        BuildLayout();
        if (OS.GetCmdlineUserArgs().Contains("--ui-smoke-test", StringComparer.Ordinal))
        {
            _ = VerifyMenuLayoutAsync();
            return;
        }
        _ = TryGetCommandLineWorldUrl(out var commandLineUrl);
        worldUrlInput.Text = commandLineUrl ?? ConfiguredWorldUrl();
        _ = InitializeAsync();

        var timer = new Godot.Timer
        {
            WaitTime = RefreshSeconds,
            Autostart = true,
        };
        timer.Timeout += () => _ = PulseAsync();
        AddChild(timer);
    }

    private async Task VerifyMenuLayoutAsync()
    {
        try
        {
            pairingPanel.Hide();
            developerScroll.Hide();
            gameMenuPanel.Show();
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(1024, 768) })
            {
                GetWindow().Size = size;
                foreach (var settingsVisible in new[] { false, true })
                {
                    foreach (var worldSpecific in settingsVisible ? new[] { false, true } : new[] { false })
                    {
                        if (settingsVisible)
                        {
                            ShowSettingsSection(worldSpecific);
                            if (gameSettingsContent.Visible == worldSpecific || worldSettingsContent.Visible != worldSpecific)
                                throw new InvalidOperationException("Game and World Settings must show different controls.");
                        }
                        else settingsPanel.Hide();
                        foreach (var selected in new[] { false, true, false })
                        {
                            selectedInhabitantCard.Visible = selected;
                            for (var frame = 0; frame < 5; frame++)
                            {
                                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            }
                            ApplyResponsiveLayout();
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            var menu = gameMenuPanel.GetGlobalRect();
                            var bounds = gameMenuPanel.GetParent<Control>().GetGlobalRect();
                            if (menu.GetCenter().DistanceTo(bounds.GetCenter()) > 2 || !bounds.Encloses(menu))
                            {
                                throw new InvalidOperationException($"Menu escaped its centered bounds: window={size}, settings={settingsVisible}, world={worldSpecific}, selected={selected}, menu={menu}, bounds={bounds}");
                            }
                            settlementPanel.Show();
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            if (!mapCanvas.GetGlobalRect().Encloses(settlementPanel.GetGlobalRect()))
                            {
                                throw new InvalidOperationException($"Settlement panel escaped the world viewport: window={size}");
                            }
                            settlementPanel.Hide();
                            worldInfoPanel.Show();
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            if (!mapCanvas.GetGlobalRect().Encloses(worldInfoPanel.GetGlobalRect()))
                                throw new InvalidOperationException($"World Info escaped the world viewport: window={size}");
                            worldInfoPanel.Hide();
                        }
                    }
                }
                creationOverlay.Show();
                for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var creationBounds = creationOverlay.GetGlobalRect();
                if (!creationBounds.Encloses(creationPanel.GetGlobalRect()) ||
                    creationBounds.GetCenter().DistanceTo(creationPanel.GetGlobalRect().GetCenter()) > 2)
                    throw new InvalidOperationException($"Creation workbench escaped its centered bounds at {size}.");
                creationOverlay.Hide();
            }
            gameMenuPanel.Hide();
            menuShade.Hide();
            selectedInhabitantCard.Hide();
            var sampleResource = new OwnerWorldResource("wood", "construction", new(1, 1), false, "available", 8, 12, 0, 0, "spring");
            var sample = new OwnerWorldSnapshot("ui-test", 0, "ui-map", Enumerable.Range(0, 16)
                .Select(index => new OwnerWorldTile(index % 4, index / 4, "meadow")).ToArray(), [], [sampleResource], null, 0)
            {
                PlacedBuildings = [new("test-hall", "test-definition", new(0, 2), 0, "Test hall", ["shelter"], 2, 1)],
                ContentPackages = [new("owner-building-ui-test", "1.0.0", "sha256:test", "proposed", null, null, null, null,
                    "sha256:manifest", "Mira's shelter study", "builder-test")],
            };
            RenderDesignPackages(sample);
            if (designPackages.ItemCount != 1 || !designPackages.GetItemText(0).Contains("proposed by builder-test", StringComparison.Ordinal))
                throw new InvalidOperationException("Creation workbench must show inhabitant proposal provenance.");
            RenderMap(sample);
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var marker = mapObjectVisuals["resource:wood"];
            var identity = marker.GetInstanceId();
            var entered = false;
            marker.MouseEntered += () => entered = true;
            GetViewport().PushInput(new InputEventMouseMotion { Position = marker.GetGlobalRect().GetCenter(), GlobalPosition = marker.GetGlobalRect().GetCenter() }, inLocalCoords: true);
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderMap(sample with { Resources = [sampleResource with { Quantity = 7 }] });
            if (!entered || marker.MouseFilter == MouseFilterEnum.Ignore || marker.GetInstanceId() != identity ||
                !marker.TooltipText.Contains("7/12", StringComparison.Ordinal) || !marker.Text.Contains("7/12", StringComparison.Ordinal))
                throw new InvalidOperationException($"Resource hover/update failed: entered={entered}, filter={marker.MouseFilter}, stable={marker.GetInstanceId() == identity}, text={marker.Text}, rect={marker.GetGlobalRect()}, hovered={GetViewport().GuiGetHoveredControl()?.GetPath()}.");
            var builtMarker = mapObjectVisuals["building:test-hall"];
            if (!builtMarker.Text.Contains("Test hall", StringComparison.Ordinal) || builtMarker.Size.X <= builtMarker.Size.Y)
                throw new InvalidOperationException("Built structures must render their name and multi-tile footprint.");
            RenderMap(sample with { Resources = [], PlacedBuildings = [] });
            if (mapObjectVisuals.ContainsKey("resource:wood")) throw new InvalidOperationException("Removed resource marker was retained.");
            if (mapObjectVisuals.ContainsKey("building:test-hall")) throw new InvalidOperationException("Removed building marker was retained.");
            var smallMapTileSize = currentTileSize;
            HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
            if (currentTileSize <= smallMapTileSize)
                throw new InvalidOperationException("Mouse-wheel zoom must work even when the small starter map reaches its fitted tile-size cap.");
            RenderMap(sample with
            {
                WorldId = "ui-navigation",
                Tiles = Enumerable.Range(0, 192)
                    .Select(index => new OwnerWorldTile(index % 16, index / 16, "meadow")).ToArray(),
                Resources = [],
                PlacedBuildings = [],
            });
            mapButton.EmitSignal(BaseButton.SignalName.Pressed);
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!worldOverviewPanel.Visible || worldOverview.VisibleTiles.Size.Y <= 0)
                throw new InvalidOperationException("The top-left map button did not open a camera-aware world overview.");
            var fittedTileSize = currentTileSize;
            var fittedViewHeight = worldOverview.VisibleTiles.Size.Y;
            for (var index = 0; index < 2; index++)
                HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
            if (currentTileSize <= fittedTileSize || worldOverview.VisibleTiles.Size.Y >= fittedViewHeight)
                throw new InvalidOperationException($"Mouse-wheel zoom did not narrow the visible world area: tile={fittedTileSize}->{currentTileSize}, view={fittedViewHeight}->{worldOverview.VisibleTiles.Size.Y}.");
            var beforeOverviewClick = mapStage.Position;
            worldOverview._GuiInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = new Vector2(worldOverview.Size.X / 2, 12),
            });
            if (mapStage.Position.DistanceTo(beforeOverviewClick) < 1)
                throw new InvalidOperationException("Clicking the overview did not move the world camera.");
            var beforeOverviewDrag = mapStage.Position;
            worldOverview._GuiInput(new InputEventMouseMotion
            {
                Position = new Vector2(worldOverview.Size.X / 2, worldOverview.Size.Y - 12),
            });
            worldOverview._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            if (mapStage.Position.DistanceTo(beforeOverviewDrag) < 1)
                throw new InvalidOperationException("Dragging the overview did not move the world camera.");
            var beforeMiddleDrag = mapStage.Position;
            HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
            HandleMapInput(new InputEventMouseMotion { Relative = new Vector2(0, 60) });
            HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });
            if (mapStage.Position.DistanceTo(beforeMiddleDrag) < 1)
                throw new InvalidOperationException("Middle-drag did not pan the world camera.");
            GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
            var beforeKeyboardPan = mapStage.Position;
            _UnhandledKeyInput(new InputEventKey { Keycode = Key.S, Pressed = true });
            if (mapStage.Position.DistanceTo(beforeKeyboardPan) < 1)
                throw new InvalidOperationException("Keyboard panning did not move the world camera.");
            var eventDestination = cameraCenterTiles.X < 8 ? new OwnerWorldPosition(15, 11) : new OwnerWorldPosition(0, 0);
            knownEvents[100] = new OwnerWorldEvent(100, 1, "food_consumed", "founder-scout", eventDestination);
            RenderEventLog();
            var beforeEventJump = cameraCenterTiles;
            eventLog.EmitSignal(RichTextLabel.SignalName.MetaClicked, "100");
            if (cameraCenterTiles.DistanceTo(beforeEventJump) < 0.5f)
                throw new InvalidOperationException("Clicking a located event did not move the world camera.");
            var formerPosition = new OwnerWorldPosition(2, 2);
            var deceased = new OwnerWorldInhabitant("archived-mira", "Mira", "dead", formerPosition,
                5_000, 5_000, [], [new("age-band", "elder"), new("death-tick", "1")],
                new OwnerWorldRoute("deceased", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(formerPosition, [formerPosition], [formerPosition]), false)
            {
                RecentPrivateThoughts = [new OwnerWorldPrivateThought(1, "I hope Rowan remembers our garden.")],
                RecentMemories = [new OwnerWorldAgentMemory(1, "living-parent", "Rowan",
                    "I hid the garden tools where Rowan cannot see them.", "private")],
            };
            var historicalSnapshot = sample with
            {
                WorldId = "ui-deceased",
                Inhabitants = [deceased],
                Resources = [],
                PlacedBuildings = [],
            };
            RenderMap(historicalSnapshot);
            RenderInhabitantList(historicalSnapshot);
            selectedInhabitantId = deceased.Id;
            RenderSelectedInhabitantCard(historicalSnapshot);
            if (entityLayer.GetChildren().Any(child => !child.IsQueuedForDeletion()) ||
                inhabitantList.ItemCount != 1 || !rosterSummaryLabel.Text.Contains("1 deceased", StringComparison.Ordinal) ||
                !selectedInhabitantCard.Visible || !selectedActorSummaryLabel.Text.Contains("Dead", StringComparison.Ordinal))
                throw new InvalidOperationException("A deceased inhabitant must remain inspectable without appearing as a living map actor.");
            if (!privateThoughtHistory.Text.Contains("I hope Rowan remembers our garden.", StringComparison.Ordinal) ||
                !privateThoughtHistory.Text.Contains("historical", StringComparison.Ordinal))
                throw new InvalidOperationException("Deceased profiles must retain their saved private thoughts without generating new ones.");
            memoriesButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!memoriesPanel.Visible ||
                !memoryHistory.Text.Contains("I hid the garden tools", StringComparison.Ordinal) ||
                inhabitantSocialDetails.Text.Contains("I hid the garden tools", StringComparison.Ordinal))
                throw new InvalidOperationException("Historical private memories must be inspectable separately from public social notes.");
            memoriesPanel.Hide();
            var originalPreferences = displayPreferences;
            displayPreferences = displayPreferences with { NotifyDeaths = true };
            notificationWorldId = historicalSnapshot.WorldId;
            lastNotificationEventId = 100;
            cameraZoom = 4;
            RenderMap(historicalSnapshot);
            var noticeDestination = cameraCenterTiles.X < 8
                ? new OwnerWorldPosition(15, 11) : new OwnerWorldPosition(0, 0);
            var deathNotice = new OwnerWorldEvent(101, 2, "inhabitant_removed", deceased.Id,
                noticeDestination);
            knownEvents[101] = deathNotice;
            ShowImportantEventNotice(historicalSnapshot, [deathNotice]);
            if (!eventNoticePanel.Visible || !eventNoticeButton.Text.Contains("Mira died", StringComparison.Ordinal))
                throw new InvalidOperationException("An out-of-view death must produce an optional notification.");
            var beforeNoticeJump = cameraCenterTiles;
            eventNoticeButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (eventNoticePanel.Visible || cameraCenterTiles.DistanceTo(beforeNoticeJump) < 0.5f)
                throw new InvalidOperationException("The event notification must jump to its location.");
            displayPreferences = displayPreferences with { NotifyDeaths = false };
            var suppressedNotice = deathNotice with { EventId = 102 };
            knownEvents[102] = suppressedNotice;
            ShowImportantEventNotice(historicalSnapshot, [suppressedNotice]);
            if (eventNoticePanel.Visible || !knownEvents.ContainsKey(102))
                throw new InvalidOperationException("Disabled pop-ups must stay quiet without removing the event log entry.");
            displayPreferences = originalPreferences;
            var parentPosition = new OwnerWorldPosition(1, 1);
            var parent = new OwnerWorldInhabitant("living-parent", "Rowan", "active", parentPosition,
                7_000, 8_000, [], [new("age-band", "adult")],
                new OwnerWorldRoute("idle", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(parentPosition, [parentPosition], [parentPosition]), false)
            {
                Relationships =
                [
                    new OwnerWorldInhabitantRelationship("birth:test", deceased.Id,
                        "biological_parentage", "accepted", "family", 1, "parent"),
                    new OwnerWorldInhabitantRelationship("partner:test", "living-partner",
                        "partnership", "accepted", "family", 1, "partner"),
                ],
            };
            var partner = new OwnerWorldInhabitant("living-partner", "Ilya", "active", parentPosition,
                7_000, 8_000, [], [new("age-band", "adult")],
                new OwnerWorldRoute("idle", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(parentPosition, [parentPosition], [parentPosition]), false)
            {
                Relationships = [new OwnerWorldInhabitantRelationship("partner:test", parent.Id,
                    "partnership", "accepted", "family", 1, "partner")],
            };
            var child = deceased with
            {
                Relationships = [new OwnerWorldInhabitantRelationship("birth:test", parent.Id,
                    "biological_parentage", "accepted", "family", 1, "child")],
            };
            ShowFamilyTree(historicalSnapshot with { Inhabitants = [parent, child, partner] }, child.Id);
            if (!familyTreePanel.Visible || familyTreeView.ParentEdgeCount != 1 || familyTreeView.PartnerEdgeCount != 1 ||
                !familyTreeView.VisiblePersonIds.Contains(parent.Id) ||
                !familyTreeView.VisiblePersonIds.Contains(child.Id) ||
                !familyTreeView.VisiblePersonIds.Contains(partner.Id))
                throw new InvalidOperationException("Family tree must show ancestry, partnerships and deceased profiles.");
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ApplyResponsiveLayout();
            if (!mapCanvas.GetGlobalRect().Encloses(familyTreePanel.GetGlobalRect()))
                throw new InvalidOperationException("Family tree panel must fit within the world view.");
            familyTreeView.GetChildren().OfType<Button>().Single(button => button.Text.StartsWith(parent.DisplayName, StringComparison.Ordinal))
                .EmitSignal(BaseButton.SignalName.Pressed);
            if (selectedInhabitantId != parent.Id || familyTreePanel.Visible)
                throw new InvalidOperationException("Selecting a relative must open that person's agent profile.");
            familyTreeView.SetPeople("roommates-only", [parent with { Relationships = [] }, child with { Relationships = [] }, partner with { Relationships = [] }], child.Id);
            if (familyTreeView.VisiblePersonIds.Count != 1 || familyTreeView.ParentEdgeCount != 0)
                throw new InvalidOperationException("Household membership must not create a family link.");
            familyTreePanel.Hide();
            quitGameButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!quitGameConfirmation.Visible)
                throw new InvalidOperationException("Quit Game must ask for confirmation before exiting.");
            quitGameConfirmation.Hide();
            GD.Print("UI checks passed: menus/workbench, confirmed quit, settlement panel, resource hover, building footprints, zoom, middle-drag, WASD, overview navigation, event jumps, event pop-ups, private thoughts, memories, deceased inspection and family tree.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.Message);
            GetTree().Quit(1);
        }
    }

    public override void _ExitTree()
    {
        deviceKey?.Dispose();
        httpClient.Dispose();
        base._ExitTree();
    }

    private async Task InitializeAsync()
    {
        try
        {
            deviceKey = OwnerDeviceKey.OpenOrCreate();
            registration = registrationStore.TryLoad(deviceKey.PublicKeyFingerprint);
            if (registration is null)
            {
                pairingPanel.Show();
                OpenMenuForSetup();
                await StartPairingAsync();
                return;
            }

            // Once paired, this device is pinned to the server origin that
            // issued the registration. A command-line URL is useful only for
            // a first pairing; it must never silently retarget an owner key.
            if (!WorldServerOrigin.TryResolve(registration.WorldUrl, out var storedWorldUri))
            {
                registeredEndpointInvalid = true;
                pairingPanel.Show();
                OpenMenuForSetup();
                pairingInstructionLabel.Text = "This saved device registration has no valid pinned server endpoint. Forget the local registration, then pair this Windows key again at the intended HTTPS host.";
                SetStatus("saved owner endpoint is invalid · re-pair required", good: false);
                RefreshControlAvailability();
                return;
            }

            worldUrlInput.Text = storedWorldUri.AbsoluteUri;
            LoadPendingSubmission();

            pairingPanel.Hide();
            settingsPanel.Hide();
            CloseGameMenu();
            SetStatus("paired device loaded · requesting signed owner observation", good: true);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            pairingPanel.Show();
            OpenMenuForSetup();
            SetStatus($"owner key unavailable · {FriendlyFailure(exception)}", good: false);
            pairingInstructionLabel.Text = "This client needs the Windows current-user key store. It does not create a portable private-key file.";
            pairButton.Disabled = true;
        }
    }

    private async Task PulseAsync()
    {
        if (pendingPairing is not null)
        {
            await PollPairingAsync();
            return;
        }

        if (registration is not null && !registeredEndpointInvalid)
        {
            await RefreshAsync();
        }
    }

    private async Task StartPairingAsync()
    {
        if (isPairingOperation || pendingPairing is not null || deviceKey is null)
        {
            return;
        }

        isPairingOperation = true;
        RefreshControlAvailability();
        try
        {
            var origin = ResolveWorldUri();
            pendingPairing = await ownerApi.StartPairingAsync(
                origin,
                deviceKey,
                CancellationToken.None);
            pendingPairingOrigin = origin;
            pairingPanel.Show();
            pairingInstructionLabel.Text = "Give the host the pairing ID and short comparison code below. The host approves it on its private loopback listener; this code is not a password.";
            pairingCodeLabel.Text = pendingPairing.PairingCode;
            pairingIdLabel.Text = pendingPairing.PairingId;
            pairingExpiryLabel.Text = $"expires {pendingPairing.ExpiresAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}";
            pairButton.Text = "Start fresh pairing";
            SetStatus("waiting for private host approval", good: true);
        }
        catch (Exception exception)
        {
            SetStatus($"could not start device pairing · {FriendlyFailure(exception)}", good: false);
        }
        finally
        {
            isPairingOperation = false;
            RefreshControlAvailability();
        }
    }

    private async Task PollPairingAsync()
    {
        if (isPairingOperation || pendingPairing is null || deviceKey is null)
        {
            return;
        }

        isPairingOperation = true;
        try
        {
            var status = await ownerApi.GetPairingStatusAsync(
                ResolveWorldUri(),
                pendingPairing.PairingId,
                CancellationToken.None);
            if (!Equals(status.Authority, pendingPairing.Authority) ||
                !string.Equals(status.DeviceId, pendingPairing.DeviceId, StringComparison.Ordinal) ||
                !string.Equals(status.PublicKeyFingerprint, deviceKey.PublicKeyFingerprint, StringComparison.Ordinal))
            {
                pendingPairingOrigin = null;
                pendingPairing = null;
                SetStatus("pairing status does not match this device and server · start a fresh pairing", good: false);
                return;
            }

            switch (status.State)
            {
                case OwnerPairingState.Pending:
                    pairingInstructionLabel.Text = "Waiting for host approval. Give the host the pairing ID and comparison code exactly as shown.";
                    break;
                case OwnerPairingState.Approved:
                    pairingInstructionLabel.Text = "Host approval received. Proving possession of this Windows device key…";
                    await ActivatePendingPairingAsync();
                    break;
                case OwnerPairingState.Active:
                    // The process can be interrupted after a successful
                    // server activation but before its non-secret local
                    // registration is flushed. Recover only when the active
                    // record remains bound to this exact Windows key.
                    if (string.Equals(status.DeviceId, pendingPairing.DeviceId, StringComparison.Ordinal) &&
                        string.Equals(status.PublicKeyFingerprint, deviceKey.PublicKeyFingerprint, StringComparison.Ordinal) &&
                        Equals(status.Authority, pendingPairing.Authority))
                    {
                        registration = new OwnerDeviceRegistration(
                            status.Authority,
                            status.DeviceId,
                            deviceKey.PublicKeyFingerprint,
                            ResolveWorldUri().AbsoluteUri);
                        registrationStore.Save(registration);
                        LoadPendingSubmission();
                        pendingPairing = null;
                        pendingPairingOrigin = null;
                        pairingPanel.Hide();
                        settingsPanel.Hide();
                        CloseGameMenu();
                        SetStatus("recovered the active device registration · requesting signed owner observation", good: true);
                        await RefreshAsync();
                    }
                    else
                    {
                        pairingInstructionLabel.Text = "This pairing is active but is not bound to this device key. Revoke it at the host before attempting another pairing.";
                        SetStatus("active pairing does not match this device key", good: false);
                    }

                    break;
                case OwnerPairingState.Expired:
                    pairingInstructionLabel.Text = "The comparison code expired. Start a fresh pairing to get a new short code.";
                    pendingPairing = null;
                    pendingPairingOrigin = null;
                    SetStatus("pairing expired", good: false);
                    break;
                default:
                    SetStatus("unknown pairing state returned by server", good: false);
                    break;
            }
        }
        catch (Exception exception)
        {
            SetStatus($"pairing status unavailable · {FriendlyFailure(exception)}", good: false);
        }
        finally
        {
            isPairingOperation = false;
            RefreshControlAvailability();
        }
    }

    private async Task ActivatePendingPairingAsync()
    {
        if (pendingPairing is null || deviceKey is null)
        {
            return;
        }

        var pairing = pendingPairing;
        try
        {
            var device = await ownerApi.ActivatePairingAsync(
                ResolveWorldUri(),
                pairing,
                deviceKey,
                CancellationToken.None);
            registration = new OwnerDeviceRegistration(
                pairing.Authority,
                device.DeviceId,
                deviceKey.PublicKeyFingerprint,
                ResolveWorldUri().AbsoluteUri);
            registrationStore.Save(registration);
            LoadPendingSubmission();
            pendingPairing = null;
            pendingPairingOrigin = null;
            pairingPanel.Hide();
            settingsPanel.Hide();
            CloseGameMenu();
            SetStatus("device paired · requesting signed owner observation", good: true);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"host approved pairing, but key activation failed · {FriendlyFailure(exception)}", good: false);
        }
    }

    private void ForgetLocalRegistration()
    {
        registrationStore.Forget();
        registration = null;
        registeredEndpointInvalid = false;
        pendingPairing = null;
        pendingPairingOrigin = null;
        pairedDevices = [];
        providerConfiguration = null;
        pairedDeviceList.Clear();
        pendingSubmission = null;
        _ = pendingSubmissionStore.TryForget();
        RenderPendingSubmission();
        knownEvents.Clear();
        pairingPanel.Show();
        connectionPanel.Show();
        OpenMenuForSetup();
        pairingCodeLabel.Text = "—";
        pairingIdLabel.Text = "—";
        pairingExpiryLabel.Text = string.Empty;
        pairingInstructionLabel.Text = "Local public registration forgotten. The Windows private key remains in the current-user key store; start a new pairing only if the host allows that key to be paired.";
        SetStatus("local registration forgotten", good: false);
        RefreshControlAvailability();
    }

    private async Task RefreshAsync()
    {
        if (isRefreshing || registeredEndpointInvalid || registration is null || deviceKey is null)
        {
            return;
        }

        isRefreshing = true;
        try
        {
            var requestedCursor = observationSession.EventCursor;
            var reconnect = await ownerApi.ReconnectAsync(
                ResolveWorldUri(),
                registration.Authority,
                registration.DeviceId,
                requestedCursor,
                deviceKey,
                CancellationToken.None);
            if (!observationSession.TryAccept(reconnect, requestedCursor, out var failure))
            {
                ShowHeldState(failure);
                return;
            }

            if (reconnect.Baseline.Events.ResetRequired)
            {
                knownEvents.Clear();
            }
            Render(reconnect.Baseline.Snapshot, reconnect.Baseline.Events.Events);
            if (!isOwnerAction)
            {
                SetStatus(string.Empty, good: true);
            }
        }
        catch (Exception exception)
        {
            ShowHeldState(FriendlyFailure(exception));
        }
        finally
        {
            isRefreshing = false;
            RefreshControlAvailability();
        }
    }

    private async Task SetPausedAsync(bool paused)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            var receipt = await ownerApi.SetPausedAsync(
                ResolveWorldUri(), authority, deviceId, paused, signer, CancellationToken.None);
            return receipt.Changed
                ? $"world {receipt.Operation}d at revision {receipt.Revision}"
                : $"world was already {(paused ? "paused" : "running")}";
        });
    }

    private async Task SubmitInstructionAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current is not { } current)
        {
            SetStatus("wait for a paired observation before sending an instruction", good: false);
            return;
        }

        var selected = current.Baseline.Snapshot.Inhabitants
            .FirstOrDefault(inhabitant => string.Equals(inhabitant.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (selected is null || selected.IsDraft)
        {
            SetStatus("select an active inhabitant before sending an instruction", good: false);
            return;
        }
        if (selected.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant"))
        {
            SetStatus("infants need care from an adult caregiver, not work instructions", good: false);
            return;
        }

        var text = instructionText.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("write an instruction before sending it", good: false);
            return;
        }

        var action = new OwnerInstructionAction(
            $"instruction_{OwnerPairingProtocol.CreateRequestId()}",
            selected.Id,
            instructionKind.GetSelectedId() == 1 ? "must_do" : "suggestive",
            text);
        if (!TryBeginPendingInstruction(action, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await ownerApi.SubmitInstructionAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            completed = true;
            instructionText.Text = string.Empty;
            return $"queued {action.Kind} instruction {receipt.InstructionId}";
        });
        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private async Task SubmitAuthoringAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current?.Baseline.Snapshot.Authoring is not { IsPaused: true })
        {
            SetStatus("paused authoring is disabled until the server reports an atomic paused boundary", good: false);
            return;
        }

        var operation = new OwnerAuthoringOperationAction(
            SelectedAuthoringKind(),
            EmptyToNull(authoringId.Text),
            EmptyToNull(authoringValue.Text),
            EmptyToNull(authoringSecondaryValue.Text),
            checked((int)authoringX.Value),
            checked((int)authoringY.Value),
            authoringRenewable.ButtonPressed);
        var batch = new OwnerAuthoringBatchAction(
            $"authoring_{OwnerPairingProtocol.CreateRequestId()}",
            [operation]);
        if (!TryBeginPendingAuthoring(batch, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await ownerApi.SubmitAuthoringAsync(
                ResolveWorldUri(), authority, deviceId, batch, signer, CancellationToken.None);
            completed = true;
            return receipt.Applied
                ? $"applied {operation.Kind} at revision {receipt.Revision}"
                : $"authoring rejected · {receipt.Failure ?? "unknown validation failure"}";
        });
        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private async Task ApprovePairingAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        var pairingId = pairingApprovalId.Text.Trim();
        var pairingCode = pairingApprovalCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(pairingId) || string.IsNullOrWhiteSpace(pairingCode))
        {
            SetStatus("enter the pending pairing ID and comparison code", good: false);
            return;
        }

        var action = new OwnerPairingApprovalAction(pairingId, pairingCode);
        await RunOwnerActionAsync(async () =>
        {
            var approval = await ownerApi.ApprovePairingAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            pairingApprovalCode.Text = string.Empty;
            return $"approved pending device {approval.DeviceId}; it must still activate its own Windows key";
        });
    }

    private async Task RevokeDeviceAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        var targetDeviceId = revokeDeviceId.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            SetStatus("enter a paired device ID to revoke it", good: false);
            return;
        }

        if (string.Equals(targetDeviceId, deviceId, StringComparison.Ordinal))
        {
            SetStatus("this client will not revoke its own active key; use host-local recovery if that is intentional", good: false);
            return;
        }

        var action = new OwnerDeviceManagementAction(targetDeviceId);
        await RunOwnerActionAsync(async () =>
        {
            var revoked = await ownerApi.RevokeDeviceAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            revokeDeviceId.Text = string.Empty;
            return $"revoked device {revoked.DeviceId}";
        });
        await RefreshDeviceRegistryAsync();
    }

    private async Task RefreshDeviceRegistryAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            pairedDevices = await ownerApi.ListDevicesAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            RenderPairedDevices();
            return $"loaded {pairedDevices.Length} paired device record(s)";
        });
    }

    private void RenderPairedDevices()
    {
        pairedDeviceList.Clear();
        foreach (var device in pairedDevices
            .OrderBy(device => device.State == OwnerDeviceState.Active ? 0 : 1)
            .ThenBy(device => device.DeviceId, StringComparer.Ordinal))
        {
            var self = string.Equals(device.DeviceId, registration?.DeviceId, StringComparison.Ordinal)
                ? " · this Windows device"
                : string.Empty;
            pairedDeviceList.AddItem($"{device.State.ToString().ToLowerInvariant()} · {device.DeviceId}{self}");
            pairedDeviceList.SetItemMetadata(pairedDeviceList.ItemCount - 1, device.DeviceId);
        }
    }

    private async Task SaveLifePaceAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var rate = lifePaceChoice.GetSelectedId();
        await RunOwnerActionAsync(async () =>
        {
            _ = await ownerApi.SetLifePaceAsync(ResolveWorldUri(), authority, deviceId, rate, signer, CancellationToken.None);
            return "life pace saved; current ages preserved, future aging changed";
        });
    }

    private async Task RefreshProviderConfigurationAsync()
    {
        cognitionApiKeyInput.Text = string.Empty;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            cognitionConfigurationStatus.Text = "Pair this device before configuring inhabitant cognition.";
            RenderProviderConfiguration();
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.GetProviderStatusAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            PopulateCognitionTargets();
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            RenderProviderConfiguration();
            return "loaded inhabitant cognition settings";
        });
    }

    private async Task SaveProviderConfigurationAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("pair this device before configuring cognition", good: false);
            return;
        }

        var role = SelectedRoleId();
        var provider = SelectedProviderId();
        var action = new OwnerProviderConfigurationAction(
            role,
            provider,
            provider is "deterministic" or "inherit" ? null : EmptyToNull(cognitionModelInput.Text),
            provider is "deterministic" or "inherit" ? null : EmptyToNull(cognitionApiKeyInput.Text),
            ForgetCredential: false,
            InhabitantId: SelectedCognitionTarget());
        try
        {
            await RunOwnerActionAsync(async () =>
            {
                providerConfiguration = await ownerApi.ConfigureProviderAsync(
                    ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
                return $"{ProviderDisplayName(provider)} will handle {RoleDisplayName(role).ToLowerInvariant()} at the next cognition boundary";
            });
        }
        finally
        {
            cognitionApiKeyInput.Text = string.Empty;
            RenderProviderConfiguration();
        }
    }

    private async Task ForgetProviderCredentialAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("pair this device before changing cognition credentials", good: false);
            return;
        }

        var role = SelectedRoleId();
        var provider = SelectedProviderId();
        if (provider == "deterministic")
        {
            SetStatus("deterministic cognition has no API key", good: false);
            return;
        }

        var action = new OwnerProviderConfigurationAction(
            role,
            provider,
            EmptyToNull(cognitionModelInput.Text),
            null,
            ForgetCredential: true);
        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.ConfigureProviderAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            return $"forgot the saved {ProviderDisplayName(provider)} key";
        });
        cognitionApiKeyInput.Text = string.Empty;
        RenderProviderConfiguration();
    }

    private string SelectedRoleId() => cognitionRoleChoice.Selected == 1 ? "planning" : "routine";

    private string? SelectedCognitionTarget() => cognitionTargetChoice.Selected <= 0
        ? null : cognitionTargetChoice.GetItemMetadata(cognitionTargetChoice.Selected).AsString();

    private void PopulateCognitionTargets()
    {
        var target = SelectedCognitionTarget();
        cognitionTargetChoice.Clear();
        cognitionTargetChoice.AddItem("World defaults");
        foreach (var inhabitant in observationSession.Current?.Baseline.Snapshot.Inhabitants ?? [])
        {
            cognitionTargetChoice.AddItem(inhabitant.DisplayName);
            var index = cognitionTargetChoice.ItemCount - 1;
            cognitionTargetChoice.SetItemMetadata(index, inhabitant.Id);
            if (inhabitant.Id == target)
            {
                cognitionTargetChoice.Select(index);
            }
        }
    }

    private InhabitantProviderAssignment? SelectedAssignment() => providerConfiguration?.Assignments?
        .FirstOrDefault(item => item.InhabitantId == SelectedCognitionTarget() && item.Role == SelectedRoleId());

    private string ActiveProviderForSelectedRole() => SelectedCognitionTarget() is not null
        ? SelectedAssignment()?.Provider ?? "inherit"
        : providerConfiguration is null
        ? "deterministic"
        : SelectedRoleId() == "planning"
            ? providerConfiguration.PlanningProvider
            : providerConfiguration.RoutineProvider;

    private void PopulateProviderChoices(string selectedProvider)
    {
        cognitionProviderChoice.Clear();
        if (SelectedCognitionTarget() is not null)
        {
            AddProviderChoice("Use world default", "inherit");
        }
        AddProviderChoice("Deterministic", "deterministic");
        if (SelectedRoleId() == "routine")
        {
            AddProviderChoice("Jev", "jev");
        }
        else
        {
            AddProviderChoice("OpenAI", "openai");
            AddProviderChoice("Ollama Cloud", "ollama-cloud");
        }

        SelectProviderChoice(selectedProvider);
    }

    private void SelectProviderChoice(string provider)
    {
        for (var index = 0; index < cognitionProviderChoice.ItemCount; index++)
        {
            if (cognitionProviderChoice.GetItemMetadata(index).AsString() == provider)
            {
                cognitionProviderChoice.Select(index);
                return;
            }
        }
        cognitionProviderChoice.Select(0);
    }

    private void AddProviderChoice(string label, string id)
    {
        cognitionProviderChoice.AddItem(label);
        cognitionProviderChoice.SetItemMetadata(cognitionProviderChoice.ItemCount - 1, id);
    }

    private string SelectedProviderId() => cognitionProviderChoice.Selected < 0
        ? "deterministic" : cognitionProviderChoice.GetItemMetadata(cognitionProviderChoice.Selected).AsString();

    private void RenderProviderConfiguration()
    {
        var provider = SelectedProviderId();
        var option = providerConfiguration?.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, provider, StringComparison.Ordinal));
        var hosted = provider is not ("deterministic" or "inherit");
        cognitionModelInput.Visible = hosted;
        cognitionApiKeyInput.Visible = hosted;
        cognitionCredentialHint.Visible = hosted;
        forgetCognitionCredentialButton.Visible = hosted && SelectedCognitionTarget() is null;
        if (hosted && option is not null && !cognitionModelInput.HasFocus())
        {
            cognitionModelInput.Text = SelectedAssignment() is { } assignment && assignment.Provider == provider
                ? assignment.Model ?? option.Model : option.Model;
        }

        cognitionApiKeyInput.PlaceholderText = option?.HasCredential == true
            ? "Leave blank to keep saved key"
            : "API key";
        cognitionCredentialHint.Text = option?.HasCredential == true
            ? "Key saved on host"
            : "No saved key";
        cognitionConfigurationStatus.Text = providerConfiguration is null
            ? "Loading…"
            : $"Routine: {ProviderDisplayName(providerConfiguration.RoutineProvider)} · Planning: {ProviderDisplayName(providerConfiguration.PlanningProvider)}";
        RefreshControlAvailability();
    }

    private static string RoleDisplayName(string role) => role == "planning"
        ? "Planning and work decisions"
        : "Routine survival decisions";

    private static string ProviderDisplayName(string provider) => provider switch
    {
        "jev" => "Jev",
        "openai" => "OpenAI",
        "ollama-cloud" => "Ollama Cloud",
        "inherit" => "World default",
        _ => "Deterministic",
    };

    private static string DefaultProviderModel(string provider) => provider switch
    {
        "jev" => "jev-1.13.0",
        "openai" => "gpt-5-mini",
        "ollama-cloud" => "gpt-oss:120b-cloud",
        _ => string.Empty,
    };

    private bool TryBeginPendingInstruction(
        OwnerInstructionAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("cannot retain an instruction until this paired device has a valid pinned server origin", good: false);
            return false;
        }

        pending = OwnerPendingSubmission.ForInstruction(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryBeginPendingAuthoring(
        OwnerAuthoringBatchAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("cannot retain authoring until this paired device has a valid pinned server origin", good: false);
            return false;
        }

        pending = OwnerPendingSubmission.ForAuthoring(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryRetainPendingSubmission(OwnerPendingSubmission candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (pendingSubmission is not null)
        {
            SetStatus("a prior owner request is awaiting confirmation; retry it or explicitly forget it first", good: false);
            return false;
        }

        if (!pendingSubmissionStore.TrySave(candidate))
        {
            SetStatus("could not retain the owner request locally; retry or forget the existing local retry record first", good: false);
            return false;
        }

        pendingSubmission = candidate;
        RenderPendingSubmission();
        RefreshControlAvailability();
        return true;
    }

    private void LoadPendingSubmission()
    {
        pendingSubmission = TryCreatePendingSubmissionBinding(out var binding)
            ? pendingSubmissionStore.TryLoad(binding)
            : null;
        RenderPendingSubmission();
        RefreshControlAvailability();
    }

    private bool TryCreatePendingSubmissionBinding(out OwnerPendingSubmissionBinding binding)
    {
        binding = null!;
        if (registeredEndpointInvalid || registration is null || deviceKey is null)
        {
            return false;
        }

        try
        {
            binding = OwnerPendingSubmissionBinding.Create(
                registration.Authority,
                registration.DeviceId,
                deviceKey.PublicKeyFingerprint,
                ResolveWorldUri());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task RetryPendingSubmissionAsync()
    {
        var pending = pendingSubmission;
        if (pending is null)
        {
            SetStatus("there is no loaded owner request to retry", good: false);
            return;
        }

        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            !TryCreatePendingSubmissionBinding(out var binding) ||
            !pending.Binding.Matches(binding))
        {
            SetStatus("this retained request is not bound to the current paired device and pinned server; forget it explicitly before making a new request", good: false);
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            if (pending.Instruction is { } instruction)
            {
                var receipt = await ownerApi.SubmitInstructionAsync(
                    ResolveWorldUri(), authority, deviceId, instruction.ToAction(), signer, CancellationToken.None);
                completed = true;
                return $"confirmed {instruction.Kind} instruction {receipt.InstructionId}";
            }

            if (pending.Authoring is { } authoring)
            {
                var receipt = await ownerApi.SubmitAuthoringAsync(
                    ResolveWorldUri(), authority, deviceId, authoring.ToAction(), signer, CancellationToken.None);
                completed = true;
                return receipt.Applied
                    ? $"confirmed authoring batch {receipt.BatchId} at revision {receipt.Revision}"
                    : $"authoring batch rejected · {receipt.Failure ?? "unknown validation failure"}";
            }

            throw new InvalidOperationException("The retained owner request has no supported payload.");
        });

        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private void CompletePendingSubmission(OwnerPendingSubmission completed)
    {
        if (!pendingSubmissionStore.TryClear(completed))
        {
            SetStatus("server confirmed the request, but its local retry record could not be cleared; retry remains safe or forget it after checking the world", good: false);
            return;
        }

        if (ReferenceEquals(pendingSubmission, completed))
        {
            pendingSubmission = null;
        }

        RenderPendingSubmission();
        RefreshControlAvailability();
    }

    private void ForgetPendingSubmission()
    {
        if (!pendingSubmissionStore.TryForget())
        {
            SetStatus("could not discard the local retry record", good: false);
            return;
        }

        pendingSubmission = null;
        RenderPendingSubmission();
        RefreshControlAvailability();
        SetStatus("discarded the local retry record; no server state was changed", good: false);
    }

    private void RenderPendingSubmission()
    {
        pendingSubmissionLabel.Text = pendingSubmission switch
        {
            { Instruction: { } instruction } =>
                $"Retained instruction retry · {instruction.Kind} for {instruction.TargetInhabitantId} · ID {instruction.IdempotencyKey}",
            { Authoring: { } authoring } =>
                $"Retained paused-authoring retry · batch {authoring.BatchId}",
            _ => "No retained owner request. A network failure keeps one instruction or authoring batch here for an exact retry.",
        };
    }

    private async Task RunOwnerActionAsync(Func<Task<string>> action)
    {
        if (isOwnerAction)
        {
            return;
        }

        isOwnerAction = true;
        RefreshControlAvailability();
        try
        {
            SetStatus("submitting one-use signed owner request…", good: true);
            var detail = await action();
            SetStatus(detail, good: true);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            ShowHeldState($"owner request rejected or unavailable · {FriendlyFailure(exception)}");
        }
        finally
        {
            isOwnerAction = false;
            RefreshControlAvailability();
        }
    }

    private bool TryGetOwner(
        out OwnerAuthorityIdentity authority,
        out string deviceId,
        out IOwnerDeviceSigner signer)
    {
        if (!registeredEndpointInvalid && registration is not null && deviceKey is not null)
        {
            authority = registration.Authority;
            deviceId = registration.DeviceId;
            signer = deviceKey;
            return true;
        }

        authority = null!;
        deviceId = string.Empty;
        signer = null!;
        return false;
    }

    private void BuildLayout()
    {
        AddThemeColorOverride("font_color", new Color("E5EFEA"));
        AddThemeFontSizeOverride("font_size", 14);

        var backdrop = new ColorRect
        {
            Color = new Color("0D151C"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        BuildTopBar(root);
        BuildWorldColumn(root);
        BuildInspectorColumn(mapCanvas);
        BuildOwnerColumn(mapCanvas);
        BuildStatusToast(mapCanvas);
        BuildCreationWorkbench();

        Resized += ApplyResponsiveLayout;
        ApplyResponsiveLayout();
    }

    private void BuildTopBar(Control content)
    {
        var chrome = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 60),
        };
        chrome.AddThemeStyleboxOverride("panel", TopBarStyle());
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 9);
        margin.AddThemeConstantOverride("margin_bottom", 9);

        topBar.AddThemeConstantOverride("separation", 8);
        mapButton.Text = "Map";
        mapButton.TooltipText = "Open the world overview; zoom with the mouse wheel and pan with WASD or middle-drag.";
        StyleButton(mapButton);
        mapButton.Pressed += () =>
        {
            var show = !worldOverviewPanel.Visible;
            rosterPanel.Hide();
            settlementPanel.Hide();
            eventsPanel.Hide();
            familyTreePanel.Hide();
            worldInfoPanel.Hide();
            worldOverviewPanel.Visible = show;
        };
        topBar.AddChild(mapButton);

        clockLabel.Text = "Connecting…";
        clockLabel.AddThemeFontSizeOverride("font_size", 20);
        clockLabel.AddThemeColorOverride("font_color", new Color("F4F0E3"));
        topBar.AddChild(clockLabel);

        climateLabel.Text = string.Empty;
        climateLabel.Modulate = new Color("AFC4BA");
        climateLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        topBar.AddChild(climateLabel);

        worldInfoButton.Text = "Info";
        worldInfoButton.TooltipText = "World Info";
        StyleButton(worldInfoButton);
        worldInfoButton.Pressed += ToggleWorldInfo;
        topBar.AddChild(worldInfoButton);

        inhabitantsButton.Text = "Inhabitants";
        StyleButton(inhabitantsButton);
        inhabitantsButton.Pressed += ToggleInhabitants;
        topBar.AddChild(inhabitantsButton);

        settlementButton.Text = "Settlement";
        StyleButton(settlementButton);
        settlementButton.Pressed += () =>
        {
            rosterPanel.Hide();
            eventsPanel.Hide();
            worldOverviewPanel.Hide();
            worldInfoPanel.Hide();
            settlementPanel.Visible = !settlementPanel.Visible;
        };
        topBar.AddChild(settlementButton);

        eventsButton.Text = "Events";
        StyleButton(eventsButton);
        eventsButton.Pressed += ToggleEvents;
        topBar.AddChild(eventsButton);

        pauseButton.Text = "Pause";
        StyleButton(pauseButton, primary: true);
        pauseButton.Pressed += () => _ = TogglePauseAsync();
        topBar.AddChild(pauseButton);

        menuButton.Text = "Menu";
        StyleButton(menuButton);
        menuButton.Pressed += () => _ = ToggleGameMenuAsync();
        topBar.AddChild(menuButton);

        margin.AddChild(topBar);
        chrome.AddChild(margin);
        content.AddChild(chrome);
    }

    private void BuildConnectionPanel()
    {
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        worldUrlInput.PlaceholderText = "https://your-tailnet-host:8443";
        worldUrlInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(worldUrlInput);
        connectButton.Text = "Connect";
        connectButton.Pressed += () => _ = ConnectUsingCurrentUrlAsync();
        body.AddChild(connectButton);
        pairAgainButton.Text = "Pair again";
        pairAgainButton.TooltipText = "Replace this device's saved world registration and pair its Windows key with the current world.";
        pairAgainButton.Visible = false;
        pairAgainButton.Pressed += () => _ = PairAgainAsync();
        body.AddChild(pairAgainButton);
        AddPanelContents(connectionPanel, "World connection", body);
    }

    private void BuildCognitionSettingsPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);

        cognitionTargetChoice.AddItem("World defaults");
        cognitionTargetChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            RenderProviderConfiguration();
        };
        body.AddChild(cognitionTargetChoice);

        cognitionRoleChoice.AddItem("Routine survival");
        cognitionRoleChoice.AddItem("Planning and work");
        cognitionRoleChoice.TooltipText = "Routine handles daily needs. Planning chooses projects. Both roles can use different providers.";
        cognitionRoleChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            var selected = SelectedProviderId();
            var option = providerConfiguration?.Providers.FirstOrDefault(item => item.Provider == selected);
            cognitionModelInput.Text = option?.Model ?? DefaultProviderModel(selected);
            RenderProviderConfiguration();
        };
        var providerRow = new HBoxContainer();
        cognitionRoleChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cognitionProviderChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        providerRow.AddChild(cognitionRoleChoice);

        PopulateProviderChoices("deterministic");
        cognitionProviderChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            var selected = SelectedProviderId();
            var option = providerConfiguration?.Providers.FirstOrDefault(item => item.Provider == selected);
            cognitionModelInput.Text = option?.Model ?? DefaultProviderModel(selected);
            RenderProviderConfiguration();
        };
        providerRow.AddChild(cognitionProviderChoice);
        body.AddChild(providerRow);

        cognitionModelInput.PlaceholderText = "Model ID";
        body.AddChild(cognitionModelInput);

        cognitionApiKeyInput.Secret = true;
        cognitionApiKeyInput.PlaceholderText = "Paste API key";
        body.AddChild(cognitionApiKeyInput);

        cognitionCredentialHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        cognitionCredentialHint.Modulate = new Color("8FA5A7");
        body.AddChild(cognitionCredentialHint);

        cognitionCredentialHint.TooltipText = "Keys travel over paired HTTPS and stay on the host. They are never returned, logged, or included in world saves.";

        cognitionConfigurationStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(cognitionConfigurationStatus);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 6);
        saveCognitionProviderButton.Text = "Apply";
        StyleButton(saveCognitionProviderButton, primary: true);
        saveCognitionProviderButton.Pressed += () => _ = SaveProviderConfigurationAsync();
        buttons.AddChild(saveCognitionProviderButton);
        forgetCognitionCredentialButton.Text = "Remove key";
        StyleButton(forgetCognitionCredentialButton);
        forgetCognitionCredentialButton.Pressed += () => _ = ForgetProviderCredentialAsync();
        buttons.AddChild(forgetCognitionCredentialButton);
        refreshCognitionProviderButton.Text = "Refresh";
        StyleButton(refreshCognitionProviderButton);
        refreshCognitionProviderButton.Pressed += () => _ = RefreshProviderConfigurationAsync();
        buttons.AddChild(refreshCognitionProviderButton);
        body.AddChild(buttons);

        AddPanelContents(cognitionSettingsPanel, "Inhabitant cognition", body);
        RenderProviderConfiguration();
    }

    private void ToggleSettingsSection(bool worldSpecific)
    {
        if (settingsPanel.Visible && worldSettingsContent.Visible == worldSpecific)
        {
            settingsPanel.Hide();
            cognitionApiKeyInput.Text = string.Empty;
            ApplyResponsiveLayout();
            return;
        }

        ShowSettingsSection(worldSpecific);
    }

    private void ShowSettingsSection(bool worldSpecific)
    {
        settingsPanel.Show();
        gameSettingsContent.Visible = !worldSpecific;
        worldSettingsContent.Visible = worldSpecific;
        gameSettingsCategoryButton.Disabled = !worldSpecific;
        worldSettingsCategoryButton.Disabled = worldSpecific || registration is null;
        developerScroll.Hide();
        developerToggleButton.Text = "Developer tools";
        if (worldSpecific && registration is not null)
        {
            _ = RefreshProviderConfigurationAsync();
        }

        ApplyResponsiveLayout();
    }

    private async Task ConnectUsingCurrentUrlAsync()
    {
        if (registeredEndpointInvalid)
        {
            SetStatus("saved paired endpoint is invalid · forget this local registration before pairing again", good: false);
            return;
        }

        try
        {
            _ = ResolveWorldUri();
        }
        catch (Exception exception)
        {
            SetStatus($"world URL is invalid · {FriendlyFailure(exception)}", good: false);
            return;
        }

        if (registration is not null)
        {
            await RefreshAsync();
            return;
        }

        await StartPairingAsync();
    }

    private async Task PairAgainAsync()
    {
        if (isPairingOperation || isOwnerAction || isRefreshing)
        {
            return;
        }

        ForgetLocalRegistration();
        await StartPairingAsync();
    }

    private void BuildPairingPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        var heading = new Label { Text = "PAIR THIS WINDOWS DEVICE" };
        heading.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(heading);
        pairingInstructionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(pairingInstructionLabel);
        var codeRow = new HBoxContainer();
        codeRow.AddChild(new Label { Text = "comparison code:" });
        pairingCodeLabel.AddThemeFontSizeOverride("font_size", 22);
        codeRow.AddChild(pairingCodeLabel);
        body.AddChild(codeRow);
        var idRow = new HBoxContainer();
        idRow.AddChild(new Label { Text = "pairing ID:" });
        pairingIdLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        idRow.AddChild(pairingIdLabel);
        body.AddChild(idRow);
        body.AddChild(pairingExpiryLabel);
        var buttons = new HBoxContainer();
        pairButton.Text = "Start pairing";
        pairButton.Pressed += () => _ = StartPairingAsync();
        buttons.AddChild(pairButton);
        forgetRegistrationButton.Text = "Forget local registration";
        forgetRegistrationButton.Pressed += ForgetLocalRegistration;
        buttons.AddChild(forgetRegistrationButton);
        body.AddChild(buttons);
        AddPanelContents(pairingPanel, body);
        pairingPanel.Hide();
    }

    private void BuildWorldColumn(Control content)
    {
        mapCanvas.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mapCanvas.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        mapCanvas.ClipContents = true;

        var worldBackdrop = new ColorRect
        {
            Color = new Color("101A1E"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        worldBackdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mapCanvas.AddChild(worldBackdrop);

        mapStage.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapCanvas.AddChild(mapStage);

        worldGrid.AddThemeConstantOverride("h_separation", TileGap);
        worldGrid.AddThemeConstantOverride("v_separation", TileGap);
        worldGrid.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapStage.AddChild(worldGrid);

        objectLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        objectLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapStage.AddChild(objectLayer);

        entityLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        entityLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapStage.AddChild(entityLayer);

        BuildSelectedInhabitantCard();
        mapCanvas.AddChild(selectedInhabitantCard);

        worldOverview.CenterRequested += CenterCameraAt;
        AddPanelContents(worldOverviewPanel, "World Map", worldOverview);
        worldOverviewPanel.Position = new Vector2(14, 14);
        worldOverviewPanel.ZIndex = 80;
        worldOverviewPanel.Hide();
        mapCanvas.AddChild(worldOverviewPanel);
        mapCanvas.GuiInput += HandleMapInput;
        content.AddChild(mapCanvas);
    }

    private void BuildInspectorColumn(Control content)
    {
        var rosterBody = new VBoxContainer();
        rosterBody.AddThemeConstantOverride("separation", 6);
        rosterSummaryLabel.Text = "Waiting for the world…";
        rosterSummaryLabel.Modulate = new Color("A7B9B7");
        rosterSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rosterBody.AddChild(rosterSummaryLabel);

        inhabitantList.CustomMinimumSize = new Vector2(300, 260);
        inhabitantList.ItemSelected += index => SelectInhabitantFromList(index);
        inhabitantList.TooltipText = "Choose someone to find them in the world.";
        rosterBody.AddChild(inhabitantList);
        AddPanelContents(rosterPanel, "Inhabitants", rosterBody);
        rosterPanel.CustomMinimumSize = new Vector2(330, 330);
        rosterPanel.ZIndex = 80;
        rosterPanel.Hide();
        content.AddChild(rosterPanel);

        ConfigureTextPanel(eventLog, 300);
        eventLog.MetaClicked += meta => JumpToEvent(meta.AsString());
        eventLog.TooltipText = "Click a located event to jump to where it happened.";
        AddPanelContents(eventsPanel, "Recent events", eventLog);
        eventsPanel.CustomMinimumSize = new Vector2(390, 360);
        eventsPanel.ZIndex = 80;
        eventsPanel.Hide();
        content.AddChild(eventsPanel);

        var familyBody = new VBoxContainer();
        var familyHeading = new HBoxContainer();
        var familyTitle = new Label { Text = "Family Tree", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        familyTitle.AddThemeFontSizeOverride("font_size", 18);
        familyHeading.AddChild(familyTitle);
        var closeFamily = new Button { Text = "×", TooltipText = "Close family tree" };
        StyleButton(closeFamily);
        closeFamily.Pressed += () => familyTreePanel.Hide();
        familyHeading.AddChild(closeFamily);
        familyBody.AddChild(familyHeading);
        familyTreeStatus.Text = "Green: parent–child   ·   Pink: partnership   ·   Click a person to inspect";
        familyBody.AddChild(familyTreeStatus);
        var familyScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        familyScroll.AddChild(familyTreeView);
        familyBody.AddChild(familyScroll);
        familyTreeView.PersonRequested += SelectFromFamilyTree;
        AddPanelContents(familyTreePanel, familyBody);
        familyTreePanel.ZIndex = 85;
        familyTreePanel.Hide();
        content.AddChild(familyTreePanel);

        var memoriesBody = new VBoxContainer();
        var memoriesHeading = new HBoxContainer();
        var memoriesTitle = new Label { Text = "Memories", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        memoriesTitle.AddThemeFontSizeOverride("font_size", 18);
        memoriesHeading.AddChild(memoriesTitle);
        var closeMemories = new Button { Text = "×", TooltipText = "Close memories" };
        StyleButton(closeMemories);
        closeMemories.Pressed += () => memoriesPanel.Hide();
        memoriesHeading.AddChild(closeMemories);
        memoriesBody.AddChild(memoriesHeading);
        ConfigureTextPanel(memoryHistory, 300);
        memoryHistory.TooltipText = "This is the selected agent's saved memory, not the authoritative world event log.";
        memoriesBody.AddChild(memoryHistory);
        AddPanelContents(memoriesPanel, memoriesBody);
        memoriesPanel.ZIndex = 85;
        memoriesPanel.Hide();
        content.AddChild(memoriesPanel);

        ConfigureTextPanel(worldDetails, 320);
        AddPanelContents(settlementPanel, "Settlement · stores and projects", worldDetails);
        settlementPanel.CustomMinimumSize = new Vector2(420, 380);
        settlementPanel.ZIndex = 80;
        settlementPanel.Hide();
        content.AddChild(settlementPanel);

        ConfigureTextPanel(worldInfoText, 220);
        AddPanelContents(worldInfoPanel, "World Info", worldInfoText);
        worldInfoPanel.CustomMinimumSize = new Vector2(365, 280);
        worldInfoPanel.ZIndex = 80;
        worldInfoPanel.Hide();
        content.AddChild(worldInfoPanel);
    }

    private void BuildOwnerColumn(Control content)
    {
        menuShade.Color = new Color(0, 0, 0, 0.46f);
        menuShade.MouseFilter = Control.MouseFilterEnum.Stop;
        menuShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuShade.ZIndex = 90;
        menuShade.Hide();
        content.AddChild(menuShade);

        var body = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(520, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);

        var menuHeading = new HBoxContainer();
        menuHeadingLabel.Text = "Paused";
        menuHeadingLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        menuHeadingLabel.AddThemeFontSizeOverride("font_size", 24);
        menuHeadingLabel.AddThemeColorOverride("font_color", new Color("F4F0E3"));
        menuHeading.AddChild(menuHeadingLabel);
        var closeButton = new Button { Text = "×", TooltipText = "Return to the world" };
        StyleButton(closeButton);
        closeButton.Pressed += () => _ = CloseGameMenuAsync();
        menuHeading.AddChild(closeButton);
        body.AddChild(menuHeading);

        var menuActions = new GridContainer { Columns = 3 };
        menuActions.AddThemeConstantOverride("h_separation", 6);
        menuActions.AddThemeConstantOverride("v_separation", 6);
        menuResumeButton.Text = "Resume";
        StyleButton(menuResumeButton, primary: true);
        menuResumeButton.Pressed += () => _ = CloseGameMenuAsync();
        menuActions.AddChild(menuResumeButton);

        gameSettingsButton.Text = "Game Settings";
        StyleButton(gameSettingsButton);
        gameSettingsButton.Pressed += () => ToggleSettingsSection(worldSpecific: false);
        menuActions.AddChild(gameSettingsButton);

        worldSettingsButton.Text = "World Settings";
        StyleButton(worldSettingsButton);
        worldSettingsButton.Pressed += () => ToggleSettingsSection(worldSpecific: true);
        menuActions.AddChild(worldSettingsButton);

        var creationButton = new Button { Text = "Create" };
        StyleButton(creationButton);
        creationButton.Pressed += () =>
        {
            creationOverlay.Show();
            designStatus.Text = observationSession.Current?.Handshake.ServerCapabilities.Contains("owner-building-design.v1", StringComparer.Ordinal) == true
                ? "Designs require your review before activation."
                : "Connect to a paired host that supports the building workbench.";
            RefreshCreationAvailability();
        };
        menuActions.AddChild(creationButton);

        developerToggleButton.Text = "Developer tools";
        StyleButton(developerToggleButton);
        developerToggleButton.Pressed += () =>
        {
            developerScroll.Visible = !developerScroll.Visible;
            settingsPanel.Hide();
            ApplyResponsiveLayout();
        };
        menuActions.AddChild(developerToggleButton);

        quitGameButton.Text = "Quit Game";
        StyleButton(quitGameButton);
        quitGameButton.Pressed += () => quitGameConfirmation.PopupCentered(new Vector2I(440, 170));
        menuActions.AddChild(quitGameButton);
        quitGameConfirmation.Title = "Quit ClankerWorld?";
        quitGameConfirmation.DialogText = "Quit the game? Your committed world progress remains saved.";
        quitGameConfirmation.Confirmed += () => GetTree().Quit();
        AddChild(quitGameConfirmation);
        body.AddChild(menuActions);

        gameSettingsContent.AddThemeConstantOverride("separation", 8);
        worldSettingsContent.AddThemeConstantOverride("separation", 8);
        fullscreenToggle.Text = "Fullscreen";
        fullscreenToggle.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        fullscreenToggle.Toggled += SetFullscreen;
        gameSettingsContent.AddChild(fullscreenToggle);

        resolutionChoice.AddItem("1280 × 720");
        resolutionChoice.AddItem("1600 × 900");
        resolutionChoice.AddItem("1920 × 1080");
        resolutionChoice.Selected = 0;
        resolutionChoice.ItemSelected += SetWindowResolution;
        gameSettingsContent.AddChild(resolutionChoice);

        var clockFormatRow = new HBoxContainer();
        clockFormatRow.AddChild(new Label { Text = "Time display" });
        clockFormatChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        clockFormatChoice.AddItem("24-hour", 0);
        clockFormatChoice.AddItem("12-hour (AM/PM)", 1);
        clockFormatChoice.Selected = displayPreferences.UseTwelveHourClock ? 1 : 0;
        clockFormatChoice.ItemSelected += SetClockFormat;
        clockFormatRow.AddChild(clockFormatChoice);
        gameSettingsContent.AddChild(clockFormatRow);

        gameSettingsContent.AddChild(new Label { Text = "Out-of-view event pop-ups" });
        AddNotificationPreference("Births", displayPreferences.NotifyBirths,
            enabled => displayPreferences with { NotifyBirths = enabled });
        AddNotificationPreference("Deaths", displayPreferences.NotifyDeaths,
            enabled => displayPreferences with { NotifyDeaths = enabled });
        AddNotificationPreference("Inventions", displayPreferences.NotifyInventions,
            enabled => displayPreferences with { NotifyInventions = enabled });
        AddNotificationPreference("New settlements", displayPreferences.NotifySettlements,
            enabled => displayPreferences with { NotifySettlements = enabled });

        var lifePaceRow = new HBoxContainer();
        lifePaceRow.AddChild(new Label { Text = "Life pace" });
        lifePaceChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lifePaceChoice.AddItem("Calendar", 1);
        lifePaceChoice.AddItem("Generations", 365);
        lifePaceChoice.AddItem("Fast generations", 1_460);
        lifePaceChoice.SetItemTooltip(0, "Original aging: one biological year per 365 world days.");
        lifePaceChoice.SetItemTooltip(1, "One biological year per world day (about 24 active minutes).");
        lifePaceChoice.SetItemTooltip(2, "One biological year per quarter-day (about 6 active minutes).");
        lifePaceChoice.TooltipText = "Changes future biological aging only. Current ages, birth dates, seasons and model-call speed stay unchanged. Faster aging brings elderhood and mortality sooner.";
        lifePaceRow.AddChild(lifePaceChoice);
        applyLifePaceButton.Text = "Apply";
        StyleButton(applyLifePaceButton);
        applyLifePaceButton.Pressed += () => _ = SaveLifePaceAsync();
        lifePaceRow.AddChild(applyLifePaceButton);
        worldSettingsContent.AddChild(lifePaceRow);

        BuildCognitionSettingsPanel();
        worldSettingsContent.AddChild(cognitionSettingsPanel);

        BuildConnectionPanel();
        gameSettingsContent.AddChild(connectionPanel);
        BuildPairingPanel();
        gameSettingsContent.AddChild(pairingPanel);
        var settingsPages = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        settingsPages.AddChild(gameSettingsContent);
        settingsPages.AddChild(worldSettingsContent);
        worldSettingsContent.Hide();
        var settingsScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 340),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        settingsScroll.AddChild(settingsPages);
        var settingsCategories = new VBoxContainer { CustomMinimumSize = new Vector2(130, 0) };
        gameSettingsCategoryButton.Text = "Game";
        StyleButton(gameSettingsCategoryButton);
        gameSettingsCategoryButton.Pressed += () => ShowSettingsSection(worldSpecific: false);
        settingsCategories.AddChild(gameSettingsCategoryButton);
        worldSettingsCategoryButton.Text = "World";
        StyleButton(worldSettingsCategoryButton);
        worldSettingsCategoryButton.Pressed += () => ShowSettingsSection(worldSpecific: true);
        settingsCategories.AddChild(worldSettingsCategoryButton);
        gameSettingsCategoryButton.Disabled = true;
        var settingsLayout = new HBoxContainer();
        settingsLayout.AddThemeConstantOverride("separation", 10);
        settingsLayout.AddChild(settingsCategories);
        settingsLayout.AddChild(settingsScroll);
        AddPanelContents(settingsPanel, "Settings", settingsLayout);
        settingsPanel.Hide();
        body.AddChild(settingsPanel);

        developerScroll.CustomMinimumSize = new Vector2(0, 440);
        developerScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        developerScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        developerBody.AddThemeConstantOverride("separation", 8);
        developerScroll.AddChild(developerBody);

        var retryBody = new VBoxContainer();
        pendingSubmissionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        retryBody.AddChild(pendingSubmissionLabel);
        var retryButtons = new HBoxContainer();
        retryPendingSubmissionButton.Text = "Retry retained request";
        retryPendingSubmissionButton.Pressed += () => _ = RetryPendingSubmissionAsync();
        retryButtons.AddChild(retryPendingSubmissionButton);
        forgetPendingSubmissionButton.Text = "Forget retained request";
        forgetPendingSubmissionButton.Pressed += ForgetPendingSubmission;
        retryButtons.AddChild(forgetPendingSubmissionButton);
        retryBody.AddChild(retryButtons);
        developerBody.AddChild(NewPanel("Response-loss recovery · exact server retry", retryBody));
        RenderPendingSubmission();

        var authoringBody = new VBoxContainer();
        authoringKind.ItemSelected += _ => UpdateAuthoringHint();
        AddAuthoringKinds();
        authoringBody.AddChild(authoringKind);
        authoringId.PlaceholderText = "ID (resource/object/draft/asset as required)";
        authoringBody.AddChild(authoringId);
        authoringValue.PlaceholderText = "Value (terrain, kind, name, weather, digest…)";
        authoringBody.AddChild(authoringValue);
        authoringSecondaryValue.PlaceholderText = "Secondary value (season for set_weather_season)";
        authoringBody.AddChild(authoringSecondaryValue);
        var coordinateRow = new HBoxContainer();
        ConfigureCoordinate(authoringX, "x");
        ConfigureCoordinate(authoringY, "y");
        coordinateRow.AddChild(authoringX);
        coordinateRow.AddChild(authoringY);
        authoringRenewable.Text = "renewable resource";
        coordinateRow.AddChild(authoringRenewable);
        authoringBody.AddChild(coordinateRow);
        authoringHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        authoringBody.AddChild(authoringHintLabel);
        submitAuthoringButton.Text = "Apply one paused authoring operation";
        submitAuthoringButton.Pressed += () => _ = SubmitAuthoringAsync();
        authoringBody.AddChild(submitAuthoringButton);
        developerBody.AddChild(NewPanel("Paused authoring · server validates atomically", authoringBody));

        var deviceManagementBody = new VBoxContainer();
        pairingApprovalId.PlaceholderText = "Pending pairing ID from the new device";
        deviceManagementBody.AddChild(pairingApprovalId);
        pairingApprovalCode.PlaceholderText = "Six-digit comparison code";
        pairingApprovalCode.Secret = true;
        deviceManagementBody.AddChild(pairingApprovalCode);
        approvePairingButton.Text = "Approve paired device";
        approvePairingButton.Pressed += () => _ = ApprovePairingAsync();
        deviceManagementBody.AddChild(approvePairingButton);
        refreshDevicesButton.Text = "Refresh signed device list";
        refreshDevicesButton.Pressed += () => _ = RefreshDeviceRegistryAsync();
        deviceManagementBody.AddChild(refreshDevicesButton);
        pairedDeviceList.CustomMinimumSize = new Vector2(0, 104);
        pairedDeviceList.ItemSelected += index =>
        {
            var deviceId = pairedDeviceList.GetItemMetadata(checked((int)index)).AsString();
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                revokeDeviceId.Text = deviceId;
            }
        };
        deviceManagementBody.AddChild(pairedDeviceList);
        revokeDeviceId.PlaceholderText = "Device ID to revoke";
        deviceManagementBody.AddChild(revokeDeviceId);
        revokeDeviceButton.Text = "Revoke other device";
        revokeDeviceButton.Pressed += () => _ = RevokeDeviceAsync();
        deviceManagementBody.AddChild(revokeDeviceButton);
        developerBody.AddChild(NewPanel("Paired-device management · signed server requests", deviceManagementBody));

        developerScroll.Hide();
        body.AddChild(developerScroll);
        AddPanelContents(gameMenuPanel, body);
        gameMenuPanel.ZIndex = 100;
        gameMenuPanel.Hide();
        var menuCenter = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100,
        };
        menuCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        content.AddChild(menuCenter);
        menuCenter.AddChild(gameMenuPanel);
        UpdateAuthoringHint();
    }

    private void BuildSelectedInhabitantCard()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);

        var heading = new HBoxContainer();
        selectedActorNameLabel.Text = string.Empty;
        selectedActorNameLabel.AddThemeFontSizeOverride("font_size", 18);
        selectedActorNameLabel.AddThemeColorOverride("font_color", new Color("F0F4EC"));
        selectedActorNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(selectedActorNameLabel);
        clearSelectionButton.Text = "×";
        clearSelectionButton.TooltipText = "Close";
        StyleButton(clearSelectionButton);
        clearSelectionButton.Pressed += ClearInhabitantSelection;
        heading.AddChild(clearSelectionButton);
        body.AddChild(heading);

        selectedActorSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectedActorSummaryLabel.Modulate = new Color("A7B9B7");
        body.AddChild(selectedActorSummaryLabel);

        ConfigureTextPanel(inhabitantDetails, 96);
        body.AddChild(inhabitantDetails);

        ConfigureTextPanel(inhabitantSocialDetails, 104);
        body.AddChild(inhabitantSocialDetails);

        ConfigureTextPanel(privateThoughtHistory, 86);
        privateThoughtHistory.TooltipText = "Only you can inspect these in-character thoughts. Other agents do not learn them automatically.";
        body.AddChild(privateThoughtHistory);

        memoriesButton.Text = "Memories";
        memoriesButton.TooltipText = "Inspect this agent's saved memories, including private memories and historical records after death.";
        StyleButton(memoriesButton);
        memoriesButton.Pressed += OpenMemories;
        var historyActions = new HBoxContainer();
        historyActions.AddChild(memoriesButton);

        familyTreeButton.Text = "Family Tree";
        familyTreeButton.TooltipText = "Inspect ancestry and partnerships, including deceased relatives.";
        StyleButton(familyTreeButton);
        familyTreeButton.Pressed += OpenFamilyTree;
        historyActions.AddChild(familyTreeButton);
        body.AddChild(historyActions);

        var instructionHeading = new Label { Text = "Speak to them" };
        instructionHeading.AddThemeFontSizeOverride("font_size", 13);
        instructionHeading.AddThemeColorOverride("font_color", new Color("D8C6A5"));
        body.AddChild(instructionHeading);
        instructionKind.AddItem("Suggestion", 0);
        instructionKind.AddItem("Direct order", 1);
        instructionKind.CustomMinimumSize = new Vector2(0, 32);
        body.AddChild(instructionKind);
        instructionText.PlaceholderText = "Say something…";
        instructionText.CustomMinimumSize = new Vector2(0, 34);
        body.AddChild(instructionText);
        submitInstructionButton.Text = "Send";
        StyleButton(submitInstructionButton, primary: true);
        submitInstructionButton.Pressed += () => _ = SubmitInstructionAsync();
        body.AddChild(submitInstructionButton);

        AddPanelContents(selectedInhabitantCard, body);
        selectedInhabitantCard.CustomMinimumSize = new Vector2(350, 0);
        selectedInhabitantCard.ZIndex = 70;
        selectedInhabitantCard.Hide();
    }

    private void BuildStatusToast(Control content)
    {
        eventNoticeButton.CustomMinimumSize = new Vector2(320, 42);
        eventNoticeButton.TooltipText = "Jump to this event or open the full event log.";
        StyleButton(eventNoticeButton);
        eventNoticeButton.Pressed += OpenEventNotice;
        AddPanelContents(eventNoticePanel, eventNoticeButton);
        eventNoticePanel.ZIndex = 119;
        eventNoticePanel.Hide();
        content.AddChild(eventNoticePanel);
        eventNoticeTimer.OneShot = true;
        eventNoticeTimer.Timeout += () => eventNoticePanel.Hide();
        content.AddChild(eventNoticeTimer);

        statusLabel.Text = "Connecting…";
        statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        statusLabel.CustomMinimumSize = new Vector2(320, 0);
        AddPanelContents(statusToast, statusLabel);
        statusToast.ZIndex = 120;
        statusToast.Hide();
        content.AddChild(statusToast);
    }

    private void ToggleInhabitants()
    {
        var show = !rosterPanel.Visible;
        familyTreePanel.Hide();
        settlementPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        rosterPanel.Visible = show;
    }

    private void ToggleEvents()
    {
        var show = !eventsPanel.Visible;
        familyTreePanel.Hide();
        settlementPanel.Hide();
        rosterPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        eventsPanel.Visible = show;
    }

    private void ToggleWorldInfo()
    {
        var show = !worldInfoPanel.Visible;
        familyTreePanel.Hide();
        settlementPanel.Hide();
        rosterPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Visible = show;
    }

    private void OpenFamilyTree()
    {
        if (selectedInhabitantId is not { } id || observationSession.Current is not { } current)
            return;
        ShowFamilyTree(current.Baseline.Snapshot, id);
    }

    private void ShowFamilyTree(OwnerWorldSnapshot snapshot, string id)
    {
        memoriesPanel.Hide();
        familyTreeView.SetPeople(snapshot.WorldId, snapshot.Inhabitants, id);
        UpdateFamilyTreeStatus();
        rosterPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        settlementPanel.Hide();
        familyTreePanel.Show();
        ApplyResponsiveLayout();
    }

    private void UpdateFamilyTreeStatus()
    {
        familyTreeStatus.Text = familyTreeView.ParentEdgeCount + familyTreeView.PartnerEdgeCount == 0
            ? "No family links recorded yet. Housemates are not automatically relatives."
            : "Green: parent–child   ·   Pink: partnership   ·   Click a person to inspect";
    }

    private void SelectFromFamilyTree(string id)
    {
        familyTreePanel.Hide();
        selectedInhabitantId = id;
        if (observationSession.Current is not { } current) return;
        var snapshot = current.Baseline.Snapshot;
        RenderInhabitantList(snapshot);
        RenderInhabitantDetails(snapshot);
        RenderSelectedInhabitantCard(snapshot);
        RenderMap(snapshot);
    }

    private void OpenMemories()
    {
        if (selectedInhabitantId is null) return;
        familyTreePanel.Hide();
        rosterPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        settlementPanel.Hide();
        memoriesPanel.Show();
        ApplyResponsiveLayout();
    }

    private async Task TogglePauseAsync()
    {
        var paused = observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused == true;
        await SetPausedAsync(!paused);
    }

    private async Task ToggleGameMenuAsync()
    {
        if (gameMenuPanel.Visible)
        {
            await CloseGameMenuAsync();
            return;
        }

        rosterPanel.Hide();
        eventsPanel.Hide();
        familyTreePanel.Hide();
        memoriesPanel.Hide();
        menuHeadingLabel.Text = "Paused";
        settlementPanel.Hide();
        gameMenuPanel.Show();
        menuShade.Show();
        ApplyResponsiveLayout();

        var paused = observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused == true;
        menuPausedWorld = observationSession.Current is not null && !paused;
        if (menuPausedWorld)
        {
            await SetPausedAsync(paused: true);
        }
    }

    private async Task CloseGameMenuAsync()
    {
        var resumeWorld = menuPausedWorld;
        CloseGameMenu();
        if (resumeWorld)
        {
            await SetPausedAsync(paused: false);
        }
    }

    private void CloseGameMenu()
    {
        cognitionApiKeyInput.Text = string.Empty;
        gameMenuPanel.Hide();
        menuShade.Hide();
        settingsPanel.Hide();
        developerScroll.Hide();
        menuPausedWorld = false;
    }

    private void OpenMenuForSetup()
    {
        menuPausedWorld = false;
        menuHeadingLabel.Text = "Set up your world";
        gameMenuPanel.Show();
        menuShade.Show();
        ShowSettingsSection(worldSpecific: false);
    }

    private static void SetFullscreen(bool enabled)
    {
        DisplayServer.WindowSetMode(enabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private void SetWindowResolution(long index)
    {
        if (fullscreenToggle.ButtonPressed)
        {
            return;
        }

        var size = index switch
        {
            1 => new Vector2I(1600, 900),
            2 => new Vector2I(1920, 1080),
            _ => new Vector2I(1280, 720),
        };
        DisplayServer.WindowSetSize(size);
    }

    private void SetClockFormat(long index)
    {
        SaveDisplayPreferences(displayPreferences with { UseTwelveHourClock = index == 1 });
        if (observationSession.Current is { } current)
            Render(current.Baseline.Snapshot, []);
    }

    private void AddNotificationPreference(
        string label, bool selected, Func<bool, GameDisplayPreferences> update)
    {
        var toggle = new CheckBox { Text = label, ButtonPressed = selected };
        toggle.Toggled += enabled => SaveDisplayPreferences(update(enabled));
        gameSettingsContent.AddChild(toggle);
    }

    private void SaveDisplayPreferences(GameDisplayPreferences updated)
    {
        displayPreferences = updated;
        try
        {
            displayPreferencesStore.Save(displayPreferences);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("could not save the game display preferences", good: false);
        }
    }

    private string DisplayWorldClock(long worldTick) =>
        GameUiText.FormatWorldClock(worldTick, displayPreferences.UseTwelveHourClock);

    private void AddAuthoringKinds()
    {
        AddAuthoringKind("set_terrain", "Set terrain — value: meadow, water, or mountain; x/y required");
        AddAuthoringKind("place_resource", "Place resource — ID, value=kind, x/y, renewable required");
        AddAuthoringKind("remove_resource", "Remove resource — ID required");
        AddAuthoringKind("place_object", "Place object — ID, value=kind, x/y required");
        AddAuthoringKind("remove_object", "Remove object — ID required");
        AddAuthoringKind("place_building", "Place building — ID, value=building kind, x/y required");
        AddAuthoringKind("remove_building", "Remove building — ID required");
        AddAuthoringKind("place_plant", "Place plant — ID, value=plant kind, x/y required");
        AddAuthoringKind("remove_plant", "Remove plant — ID required");
        AddAuthoringKind("create_founder_draft", "Create founder draft — ID, value=display name, x/y required");
        AddAuthoringKind("remove_founder_draft", "Remove founder draft — ID required");
        AddAuthoringKind("set_weather", "Set weather — value required");
        AddAuthoringKind("set_season", "Set season — value: spring, summer, autumn, or winter");
        AddAuthoringKind("set_weather_season", "Set weather + season — value=weather, secondary value=season");
        AddAuthoringKind("add_approved_asset_reference", "Add approved asset reference — ID and exact lowercase sha256 digest must already exist in the host catalog");
        AddAuthoringKind("remove_approved_asset_reference", "Remove approved asset reference — ID required");
    }

    private void AddAuthoringKind(string kind, string description)
    {
        authoringKind.AddItem(kind);
        authoringKind.SetItemMetadata(authoringKind.ItemCount - 1, description);
    }

    private void UpdateAuthoringHint()
    {
        if (authoringKind.ItemCount == 0)
        {
            return;
        }

        authoringHintLabel.Text = authoringKind.GetItemMetadata(authoringKind.Selected).AsString();
    }

    private static void ConfigureCoordinate(SpinBox box, string placeholder)
    {
        box.MinValue = 0;
        box.MaxValue = 99;
        box.Step = 1;
        box.CustomMinimumSize = new Vector2(72, 0);
        box.TooltipText = placeholder;
    }

    private void Render(OwnerWorldSnapshot snapshot, IReadOnlyList<OwnerWorldEvent> appendedEvents)
    {
        if (cameraWorldId is not null && cameraWorldId != snapshot.WorldId)
        {
            knownEvents.Clear();
            familyTreePanel.Hide();
        }
        foreach (var worldEvent in appendedEvents)
        {
            knownEvents[worldEvent.EventId] = worldEvent;
        }
        foreach (var expiredId in knownEvents.Keys.OrderByDescending(id => id).Skip(2048).ToArray())
        {
            knownEvents.Remove(expiredId);
        }

        RenderInhabitantList(snapshot);
        RenderMap(snapshot);
        RenderWorldHud(snapshot);
        RenderWorldInfo(snapshot);
        RenderInhabitantDetails(snapshot);
        RenderSelectedInhabitantCard(snapshot);
        if (familyTreePanel.Visible && selectedInhabitantId is { } center)
        {
            familyTreeView.SetPeople(snapshot.WorldId, snapshot.Inhabitants, center);
            UpdateFamilyTreeStatus();
        }
        RenderWorldDetails(snapshot);
        RenderDesignPackages(snapshot);
        RenderEventLog();
        ShowImportantEventNotice(snapshot, appendedEvents);
        RefreshControlAvailability();
    }

    private void ShowImportantEventNotice(
        OwnerWorldSnapshot snapshot, IReadOnlyList<OwnerWorldEvent> appendedEvents)
    {
        if (notificationWorldId != snapshot.WorldId)
        {
            notificationWorldId = snapshot.WorldId;
            lastNotificationEventId = knownEvents.Count == 0 ? 0 : knownEvents.Keys.Max();
            eventNoticePanel.Hide();
            return;
        }

        var newEvents = appendedEvents.Where(item => item.EventId > lastNotificationEventId)
            .OrderBy(item => item.EventId).ToArray();
        if (newEvents.Length == 0) return;
        lastNotificationEventId = newEvents[^1].EventId;
        var visible = worldOverview.VisibleTiles;
        var important = newEvents.LastOrDefault(item =>
            GameUiText.NotificationCategory(item.Kind) is { } category &&
            displayPreferences.AllowsNotification(category) &&
            (item.Position is null || !visible.HasPoint(new Vector2(item.Position.X + 0.5f, item.Position.Y + 0.5f))));
        if (important is null) return;

        visibleNoticeEventId = important.EventId;
        var description = DescribeWorldEvent(important, snapshot);
        eventNoticeButton.Text = (description.Length > 110 ? description[..107] + "…" : description) +
            (important.Position is null ? " · Open log" : " · Jump");
        eventNoticePanel.Show();
        eventNoticeTimer.Start(7);
        ApplyResponsiveLayout();
    }

    private void OpenEventNotice()
    {
        eventNoticePanel.Hide();
        eventNoticeTimer.Stop();
        if (visibleNoticeEventId is not { } id || !knownEvents.TryGetValue(id, out var worldEvent)) return;
        if (worldEvent.Position is not null)
            JumpToEvent(id.ToString(CultureInfo.InvariantCulture));
        else if (!eventsPanel.Visible)
            ToggleEvents();
    }

    private void RenderMap(OwnerWorldSnapshot snapshot)
    {
        renderedMapSnapshot = snapshot;
        foreach (var child in worldGrid.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var child in entityLayer.GetChildren())
        {
            child.QueueFree();
        }
        var objectIds = snapshot.Resources.Select(resource => "resource:" + resource.Id)
            .Concat(snapshot.Objects.Select(item => "object:" + item.Id))
            .Concat(snapshot.PlacedBuildings.Select(item => "building:" + item.InstanceId)).ToHashSet(StringComparer.Ordinal);
        foreach (var id in mapObjectVisuals.Keys.Where(id => !objectIds.Contains(id)).ToArray())
        {
            mapObjectVisuals[id].QueueFree();
            mapObjectVisuals.Remove(id);
        }

        if (snapshot.Tiles.Count == 0)
        {
            return;
        }

        var mapWidth = snapshot.Tiles.Max(tile => tile.X) + 1;
        var mapHeight = snapshot.Tiles.Max(tile => tile.Y) + 1;
        if (!string.Equals(cameraWorldId, snapshot.WorldId, StringComparison.Ordinal))
        {
            cameraWorldId = snapshot.WorldId;
            cameraZoom = 1;
            cameraCenterTiles = new Vector2(mapWidth / 2f, mapHeight / 2f);
        }
        worldOverview.SetWorld(snapshot.Tiles, mapWidth, mapHeight);
        UpdateMapGeometry(snapshot);
        worldGrid.Columns = mapWidth;
        foreach (var tile in snapshot.Tiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X))
        {
            var key = $"{tile.X},{tile.Y}";
            var cell = new Button
            {
                Text = TerrainMarker(tile.Terrain),
                CustomMinimumSize = new Vector2(currentTileSize, currentTileSize),
                TooltipText = $"{tile.Terrain} at {key}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            cell.AddThemeFontSizeOverride("font_size", 13);
            cell.AddThemeColorOverride("font_color", new Color("E6F0E8"));
            cell.AddThemeStyleboxOverride("normal", TileStyle(WorldMapPalette.TerrainColor(tile.Terrain)));
            cell.AddThemeStyleboxOverride("hover", TileStyle(WorldMapPalette.TerrainColor(tile.Terrain).Lightened(0.15f)));
            worldGrid.AddChild(cell);
        }

        foreach (var resource in snapshot.Resources)
        {
            AddMapObjectVisual(
                "resource:" + resource.Id,
                resource.Position,
                ResourceGlyph(resource.Kind),
                ResourceMarker(resource.Kind) + (resource.Quantity is null ? "" : " " + GameUiText.ResourceQuantity(resource.Kind, resource.Quantity, resource.Capacity)),
                GameUiText.ResourceTooltip(resource));
        }

        foreach (var mapObject in snapshot.Objects)
        {
            AddMapObjectVisual(
                "object:" + mapObject.Id,
                mapObject.Position,
                ObjectGlyph(mapObject.Kind),
                ObjectMarker(mapObject.Kind),
                Pretty(mapObject.Kind));
        }

        foreach (var building in snapshot.PlacedBuildings)
        {
            var tags = building.Tags ?? [];
            var kind = tags.Contains("shelter", StringComparer.Ordinal) ? "shelter" :
                tags.Any(tag => tag is "warmth" or "cooking") ? "campfire" : "building";
            var name = building.DisplayName ?? "Building";
            AddMapObjectVisual("building:" + building.InstanceId, building.Position, ObjectGlyph(kind), name,
                $"{name}\nBuilt · {building.Width} × {building.Height} tiles", building.Width, building.Height);
        }

        foreach (var group in snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft && string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase))
            .GroupBy(inhabitant => PositionKey(inhabitant.Position)))
        {
            var occupants = group.ToArray();
            for (var index = 0; index < occupants.Length; index++)
            {
                var inhabitant = occupants[index];
                var stride = currentTileSize + TileGap;
                var actorSize = Math.Clamp(currentTileSize * 0.5f, 52, 78);
                var offsetX = ((currentTileSize - actorSize) / 2) + ((index % 2) * 22);
                var offsetY = ((currentTileSize - actorSize) / 2) + ((index / 2) * 22);
                var targetPosition = new Vector2(
                    inhabitant.Position.X * stride + offsetX,
                    inhabitant.Position.Y * stride + offsetY);
                var activity = ActivityGlyph(inhabitant.PublicIntention?.CandidateId);
                var actorButton = new Button
                {
                    Text = $"● {activity}\n{ActorLabel(inhabitant.DisplayName)}",
                    TooltipText = $"{inhabitant.DisplayName} · {Pretty(inhabitant.Lifecycle)} · " +
                        (inhabitant.PublicIntention?.Summary ?? "taking in the world"),
                    Position = renderedInhabitantPositions.TryGetValue(inhabitant.Id, out var previousPosition)
                        ? new Vector2(
                            previousPosition.X * stride + offsetX,
                            previousPosition.Y * stride + offsetY)
                        : targetPosition,
                    CustomMinimumSize = new Vector2(actorSize, actorSize),
                    MouseFilter = Control.MouseFilterEnum.Pass,
                    ZIndex = 10,
                };
                actorButton.AddThemeColorOverride("font_color", Colors.White);
                actorButton.AddThemeFontSizeOverride("font_size", 15);
                actorButton.AddThemeStyleboxOverride(
                    "normal",
                    ActorStyle(string.Equals(inhabitant.Id, selectedInhabitantId, StringComparison.Ordinal)));
                actorButton.AddThemeStyleboxOverride("hover", ActorStyle(selected: true));
                actorButton.Pressed += () => SelectInhabitant(inhabitant.Id);
                entityLayer.AddChild(actorButton);
                if (actorButton.Position != targetPosition)
                {
                    CreateTween()
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.InOut)
                        .TweenProperty(actorButton, "position", targetPosition, 0.62);
                }

                renderedInhabitantPositions[inhabitant.Id] = inhabitant.Position;
            }
        }

        var visibleInhabitantIds = snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft && string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase))
            .Select(inhabitant => inhabitant.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var removedId in renderedInhabitantPositions.Keys
                     .Where(id => !visibleInhabitantIds.Contains(id))
                     .ToArray())
        {
            renderedInhabitantPositions.Remove(removedId);
        }

        PositionSelectedInhabitantCard(snapshot);
    }

    private void AddMapObjectVisual(
        string id,
        OwnerWorldPosition position,
        string glyph,
        string label,
        string tooltip,
        int width = 1,
        int height = 1)
    {
        var stride = currentTileSize + TileGap;
        if (!mapObjectVisuals.TryGetValue(id, out var visual))
        {
            visual = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Pass,
                ZIndex = 5,
                ClipText = true,
            };
            visual.AddThemeFontSizeOverride("font_size", 12);
            visual.AddThemeColorOverride("font_color", new Color("E8F0D8"));
            visual.AddThemeColorOverride("font_shadow_color", new Color("18211D"));
            visual.AddThemeConstantOverride("shadow_offset_x", 1);
            visual.AddThemeConstantOverride("shadow_offset_y", 1);
            objectLayer.AddChild(visual);
            mapObjectVisuals.Add(id, visual);
        }
        visual.Text = $"{glyph}\n{label}";
        visual.Position = new Vector2(position.X * stride + 4, position.Y * stride + 4);
        visual.Size = new Vector2(stride * Math.Clamp(width, 1, 32) - TileGap - 8,
            stride * Math.Clamp(height, 1, 32) - TileGap - 8);
        visual.TooltipText = tooltip;
    }

    private void RenderWorldHud(OwnerWorldSnapshot snapshot)
    {
        var paused = snapshot.Authoring?.IsPaused == true;
        clockLabel.Text = DisplayWorldClock(snapshot.WorldTick);
        inhabitantsButton.Text = $"Agents {LivingPopulation(snapshot)}";
        inhabitantsButton.TooltipText = "Living agents · open the inhabitant list";
        climateLabel.Text = snapshot.Authoring is { } authoring
            ? $"{Pretty(authoring.Season)} · {Pretty(authoring.Weather)}"
            : string.Empty;
        pauseButton.Text = paused ? "Play" : "Pause";
        pauseButton.TooltipText = paused ? "Resume the world" : "Pause the world";
        menuResumeButton.Text = menuPausedWorld ? "Resume" : "Close menu";
    }

    private static int LivingPopulation(OwnerWorldSnapshot snapshot) => snapshot.Inhabitants.Count(inhabitant =>
        !inhabitant.IsDraft && string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase));

    private void RenderWorldInfo(OwnerWorldSnapshot snapshot)
    {
        var width = snapshot.Tiles.Count == 0 ? 0 : snapshot.Tiles.Max(tile => tile.X) + 1;
        var height = snapshot.Tiles.Count == 0 ? 0 : snapshot.Tiles.Max(tile => tile.Y) + 1;
        var localWeather = snapshot.Authoring is { } authoring
            ? $"{Pretty(authoring.Season)} · {Pretty(authoring.Weather)}"
            : "Not reported";
        worldInfoText.Text =
            $"Date and time: {DisplayWorldClock(snapshot.WorldTick)}\n" +
            $"Living agents: {LivingPopulation(snapshot)}\n" +
            $"Map: {width} × {height} tiles\n" +
            $"Buildings: {snapshot.PlacedBuildings.Count}\n" +
            $"Resource sites: {snapshot.Resources.Count}\n" +
            $"Local season and weather: {localWeather}";
    }

    private void RenderInhabitantList(OwnerWorldSnapshot snapshot)
    {
        var previousSelection = selectedInhabitantId;
        var selectionFound = false;
        inhabitantList.Clear();
        var inhabitants = snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft)
            .OrderBy(inhabitant => inhabitant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var living = inhabitants.Count(inhabitant => string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase));
        var deceased = inhabitants.Length - living;
        rosterSummaryLabel.Text = inhabitants.Length == 0
            ? "No one lives here yet."
            : deceased == 0 ? $"{living} living" : $"{living} living · {deceased} deceased";

        for (var index = 0; index < inhabitants.Length; index++)
        {
            var inhabitant = inhabitants[index];
            inhabitantList.AddItem($"{inhabitant.DisplayName}   ·   {Pretty(inhabitant.Lifecycle)}");
            inhabitantList.SetItemMetadata(index, inhabitant.Id);
            if (string.Equals(inhabitant.Id, previousSelection, StringComparison.Ordinal))
            {
                selectionFound = true;
                inhabitantList.Select(index);
            }
        }

        if (!selectionFound)
        {
            selectedInhabitantId = null;
            inhabitantList.DeselectAll();
        }
    }

    private void RenderInhabitantDetails(OwnerWorldSnapshot snapshot)
    {
        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        inhabitantDetails.Clear();
        if (inhabitant is null)
        {
            return;
        }

        if (string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase))
        {
            inhabitantDetails.AppendText("Deceased · historical record; no current activity or carried inventory.");
            return;
        }

        var inventory = inhabitant.Inventory.Count == 0
            ? "none"
            : string.Join(", ", inhabitant.Inventory.Select(item => $"{item.Kind}: {item.Quantity}"));
        var currentActivity = string.IsNullOrWhiteSpace(inhabitant.Route.Status)
            ? "wandering"
            : Pretty(inhabitant.Route.Status);
        var destination = inhabitant.Route.Destination is { } routeDestination
            ? $" toward {routeDestination.X}, {routeDestination.Y}"
            : string.Empty;
        inhabitantDetails.AppendText(
            $"{currentActivity}{destination}\n" +
            $"Hunger {NeedPercent(inhabitant.HungerBasisPoints)}%  ·  Energy {NeedPercent(inhabitant.EnergyBasisPoints)}%\n" +
            $"Carrying {inventory}");
    }

    private void RenderSelectedInhabitantCard(OwnerWorldSnapshot snapshot)
    {
        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (inhabitant is null)
        {
            selectedActorNameLabel.Text = string.Empty;
            selectedActorSummaryLabel.Text = string.Empty;
            inhabitantSocialDetails.Clear();
            privateThoughtHistory.Clear();
            memoryHistory.Clear();
            memoriesPanel.Hide();
            selectedInhabitantCard.Hide();
            return;
        }

        selectedActorNameLabel.Text = inhabitant.DisplayName;
        var ageBand = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "age-band")?.Detail;
        var ageYears = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "age-years")?.Detail;
        var deathTick = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "death-tick")?.Detail;
        var deathCause = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "death-cause")?.Detail;
        var isDeceased = string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase);
        var waitingForDecision = inhabitant.DecisionFactors.Any(factor => factor.Key == "decision-pending");
        selectedActorSummaryLabel.Text = Pretty(inhabitant.Lifecycle) + (ageBand is null ? "" : " · " + Pretty(ageBand)) +
            (ageYears is null ? "" : " · " + ageYears + " years") +
            (deathTick is not null && long.TryParse(deathTick, CultureInfo.InvariantCulture, out var finalTick)
                ? $" · {DisplayWorldClock(finalTick)}" : "");
        var intention = isDeceased
            ? $"Life ended{(deathCause is null ? "" : " · " + Pretty(deathCause))}. No current thoughts or activity."
            : waitingForDecision
            ? "Decision pending."
            : inhabitant.PublicIntention is { } publicIntention
            ? $"Wants to {GameUiText.HumanizeIdentifier(publicIntention.Summary).ToLowerInvariant()}."
            : "Taking in their surroundings.";
        var relationships = inhabitant.Relationships.Count == 0
            ? "No close relationships yet."
            : string.Join(
                "; ",
                inhabitant.Relationships.Select(relationship =>
                {
                    var other = snapshot.Inhabitants.FirstOrDefault(item => item.Id == relationship.OtherPartyId)?.DisplayName
                        ?? Pretty(relationship.OtherPartyId);
                    return $"{Pretty(relationship.Type)} with {other} · {Pretty(relationship.State)}";
                }));
        inhabitantSocialDetails.Clear();
        var decision = snapshot.Cognition?.Decisions?.FirstOrDefault(item => item.InhabitantId == inhabitant.Id);
        var activity = waitingForDecision ? "Decision pending" : decision is null
            ? "No decision yet"
            : $"{Pretty(decision.Provider)}{(decision.FellBack ? " (fallback)" : "")} · {GameUiText.HumanizeIdentifier(decision.CandidateId)}";
        var projectText = inhabitant.Project is { } project
            ? $"{project.Label} · {Pretty(project.Stage)} · {project.WorkDone}/{project.WorkRequired}" +
                (project.Blocker is null ? "" : $"\n{project.Blocker}")
            : "No settlement project";
        var socialNotes = inhabitant.SocialNotes.Count == 0 ? "" : "\n" + string.Join("\n", inhabitant.SocialNotes);
        var standing = inhabitant.SocialStanding.Count == 0 ? "" : "\n" + string.Join(" · ",
            inhabitant.SocialStanding.Select(item => $"Trust in {item.SubjectName} {item.Trust}/10"));
        var condition = inhabitant.Survival is { } survival
            ? $"{(isDeceased ? "At death · " : "")}Warmth {survival.WarmthBasisPoints / 100}% · Illness {survival.IllnessBasisPoints / 100}%" +
                $" · Diet {survival.NutritionBasisPoints / 100}%\n" +
                $"{(survival.HasClothing ? "Clothed" : "No warm clothing")} · {(survival.HasTool ? "Tool equipped" : "Working by hand")}\n" : "";
        var role = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "role")?.Detail;
        var learning = inhabitant.Lesson is { } lesson
            ? $"\nLearning {Pretty(lesson.Role)} with {lesson.TeacherName} · {Pretty(lesson.Stage)} · {lesson.Progress}/{lesson.Required}" : "";
        if (inhabitant.Proficiency is { } practice)
            learning += $"\nPractice · Building {practice.Building}/30 · Farming {practice.Farming}/30 · Crafting {practice.Crafting}/30";
        inhabitantSocialDetails.Text = $"{condition}{(role is null ? "" : Pretty(role) + "\n")}{(inhabitant.Project is null ? intention : projectText)}{learning}\n{relationships}{standing}{socialNotes}\n{activity}";
        var thoughtHeading = isDeceased ? "Private thoughts · historical" : "Private thoughts";
        privateThoughtHistory.Text = inhabitant.RecentPrivateThoughts.Count == 0
            ? thoughtHeading + "\nNone recorded yet."
            : thoughtHeading + "\n" + string.Join("\n", inhabitant.RecentPrivateThoughts
                .Reverse().Select(thought => $"{DisplayWorldClock(thought.WorldTick)}  {thought.Text}"));
        memoryHistory.Text = inhabitant.RecentMemories.Count == 0
            ? "No saved memories for this agent yet."
            : string.Join("\n\n", inhabitant.RecentMemories.Select(memory =>
                $"{DisplayWorldClock(memory.WorldTick)} · {Pretty(memory.Visibility)} · about {memory.SubjectName}\n{memory.Summary}"));
        inhabitantSocialDetails.TooltipText = decision is null ? "" :
            $"Last accepted decision\nRole: {decision.Role ?? "not reported"}\nModel: {decision.Model ?? "not reported"}\nConfidence: {decision.Confidence:P0}\n" +
            $"Latency: {decision.LatencyMilliseconds?.ToString(CultureInfo.CurrentCulture) ?? "—"} ms\n" +
            $"Tokens in/out: {decision.InputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}/{decision.OutputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}";
        if (inhabitant.Proficiency is not null)
            inhabitantSocialDetails.TooltipText += "\nPractice: each completed project earns one point in its domain, up to 30. Every 10 points adds one work per preparation step. Materials, permissions and crop growth time are unchanged.";
        selectedInhabitantCard.Show();
        PositionSelectedInhabitantCard(snapshot);
    }

    private void RenderWorldDetails(OwnerWorldSnapshot snapshot)
    {
        worldDetails.Clear();
        var authoring = snapshot.Authoring;
        var instructions = snapshot.Instructions.Count == 0
            ? "none"
            : string.Join("\n", snapshot.Instructions.Select(instruction =>
                $"#{instruction.SubmissionSequence} {instruction.Kind} → {instruction.TargetInhabitantId}: {instruction.Text} [{instruction.State}]"));
        var cognition = snapshot.Cognition is null
            ? "not reported"
            : $"{snapshot.Cognition.Provider} · " +
              $"{snapshot.Cognition.CurrentCandidateId ?? "no current intention"}";
        var content = snapshot.ContentPackages.Count == 0
            ? "none"
            : string.Join(", ", snapshot.ContentPackages.Select(package =>
                $"{package.PackageId} {package.Version} [{Pretty(package.Lifecycle)}]"));
        var systems = snapshot.WorldSystems is not { } worldSystems
            ? "not reported"
            : $"{Pretty(worldSystems.Season)} / {Pretty(worldSystems.Weather)} · " +
              $"{worldSystems.EcologyResourceCount} ecology · {worldSystems.FactionCount} factions · " +
              $"{worldSystems.CurrencyAccountCount} wallets · {worldSystems.CultureCount} cultures · " +
              $"{worldSystems.ChunkCount} chunks · {worldSystems.BuildingDefinitionCount} buildings · " +
              $"{worldSystems.RecipeDefinitionCount} recipes";
        if (authoring is null)
        {
            worldDetails.AppendText($"tick {snapshot.WorldTick}\nworld {snapshot.WorldId}\nNo authoring projection returned.");
            return;
        }

        var stores = snapshot.Stockpiles.Count == 0 ? "No shared stores" : string.Join("\n", snapshot.Stockpiles.Select(stockpile =>
            $"{stockpile.Name}: " + (stockpile.Items.Count == 0 ? "empty" : string.Join(" · ", stockpile.Items.Select(item => $"{Pretty(item.Kind)} {item.Quantity}")))));
        var projects = snapshot.Inhabitants.Where(person => person.Project is not null).Select(person =>
            $"{person.DisplayName}: {person.Project!.Label} · {Pretty(person.Project.Stage)}" +
            (person.Project.Blocker is null ? "" : $"\n  {person.Project.Blocker}"));
        worldDetails.Text = $"{(authoring.IsPaused ? "Paused" : "Playing")} · {DisplayWorldClock(snapshot.WorldTick)}\n" +
            $"{Pretty(authoring.Season)} · {Pretty(authoring.Weather)}\n\nShared stores\n{stores}\n\nProjects\n{string.Join("\n", projects)}\n\nSocial activity\n" +
            string.Join("\n", snapshot.Inhabitants.SelectMany(person => person.SocialNotes.Take(2).Select(note => $"{person.DisplayName}: {note}")));
        if (snapshot.Council is { } council)
        {
            worldDetails.Text += $"\n\nHousehold council\nSteward: {council.StewardName ?? "awaiting a contributor"}\n" +
                (council.FoodPolicy == "essential_first" ? "Food reserve: hungry members first" : "Shared food: open access") +
                (council.ProposedPolicy is null ? "" : $"\nVote: {Pretty(council.ProposedPolicy)} · {council.Approvals} yes / {council.Rejections} no / {council.Voters} voters");
        }
        worldDetails.TooltipText =
            $"tick {snapshot.WorldTick} · revision {authoring.Revision} · epoch {authoring.RunEpoch}\n" +
            $"state: {(authoring.IsPaused ? "PAUSED — authoring allowed" : "RUNNING — authoring disabled")}\n" +
            $"weather/season: {authoring.Weather} / {authoring.Season}\n" +
            $"current topology: {authoring.CurrentMapManifestDigest}\n" +
            $"initial fixture topology: {authoring.InitialMapManifestDigest}\n" +
            $"cognition: {cognition}\n" +
            $"richer systems: {systems}\n" +
            $"content packages: {content}\n" +
            $"approved assets: {(authoring.ApprovedAssetReferences.Count == 0 ? "none" : string.Join(", ", authoring.ApprovedAssetReferences))}\n\n" +
            $"queued instructions:\n{instructions}";
    }

    private void RenderEventLog()
    {
        eventLog.Clear();
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var events = knownEvents.Values
            .Where(worldEvent => GameUiText.IsPlayerFacingEvent(worldEvent.Kind))
            .OrderByDescending(worldEvent => worldEvent.EventId)
            .Take(30)
            .ToArray();
        if (events.Length == 0)
        {
            eventLog.AppendText("Nothing notable has happened yet.");
            return;
        }

        foreach (var worldEvent in events)
        {
            var line = $"{DisplayWorldClock(worldEvent.WorldTick)}\n" +
                $"{DescribeWorldEvent(worldEvent, snapshot)}";
            if (worldEvent.Position is not null)
            {
                eventLog.PushMeta(worldEvent.EventId.ToString(CultureInfo.InvariantCulture));
                eventLog.AddText(line + " ↗");
                eventLog.Pop();
            }
            else eventLog.AddText(line);
            eventLog.AddText("\n\n");
        }
    }

    private void JumpToEvent(string eventId)
    {
        if (!long.TryParse(eventId, CultureInfo.InvariantCulture, out var id) ||
            !knownEvents.TryGetValue(id, out var worldEvent) ||
            worldEvent.Position is not { } position)
            return;
        CenterCameraAt(new Vector2(position.X + 0.5f, position.Y + 0.5f));
        eventsPanel.Hide();
    }

    private void SelectInhabitantFromList(long index)
    {
        if (index < 0 || index >= inhabitantList.ItemCount)
        {
            return;
        }

        var inhabitantId = inhabitantList.GetItemMetadata((int)index).AsString();
        if (string.Equals(inhabitantId, selectedInhabitantId, StringComparison.Ordinal))
        {
            ClearInhabitantSelection();
            return;
        }

        selectedInhabitantId = inhabitantId;
        rosterPanel.Hide();
        if (observationSession.Current is { } current)
        {
            RenderInhabitantDetails(current.Baseline.Snapshot);
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
        }
    }

    private void SelectInhabitant(string inhabitantId)
    {
        if (string.Equals(inhabitantId, selectedInhabitantId, StringComparison.Ordinal))
        {
            ClearInhabitantSelection();
            return;
        }

        selectedInhabitantId = inhabitantId;
        for (var index = 0; index < inhabitantList.ItemCount; index++)
        {
            if (string.Equals(inhabitantList.GetItemMetadata(index).AsString(), inhabitantId, StringComparison.Ordinal))
            {
                inhabitantList.Select(index);
                break;
            }
        }

        if (observationSession.Current is { } current)
        {
            RenderInhabitantDetails(current.Baseline.Snapshot);
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
        }
    }

    private void ClearInhabitantSelection()
    {
        familyTreePanel.Hide();
        memoriesPanel.Hide();
        selectedInhabitantId = null;
        inhabitantList.DeselectAll();
        if (observationSession.Current is { } current)
        {
            RenderInhabitantDetails(current.Baseline.Snapshot);
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
        }
    }

    private void RefreshControlAvailability()
    {
        var paired = !registeredEndpointInvalid && registration is not null && deviceKey is not null;
        worldSettingsButton.Disabled = !paired;
        worldSettingsCategoryButton.Disabled = !paired || worldSettingsContent.Visible;
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var paused = snapshot?.Authoring?.IsPaused == true;
        var selected = snapshot?.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        var actionDisabled = !paired || isOwnerAction || pendingSubmission is not null;
        var supportsLifePace = snapshot?.LifePaceRate is not null;
        applyLifePaceButton.Disabled = actionDisabled || !paused || !supportsLifePace;
        lifePaceChoice.Disabled = actionDisabled || !paused || !supportsLifePace;
        applyLifePaceButton.TooltipText = !supportsLifePace ? "This host does not support life pacing." :
            !paused ? "Pause the world before changing life pace." : "Apply future aging speed; existing ages are preserved.";
        if (snapshot?.LifePaceRate is { } rate && (lastObservedLifePace != rate || lastLifePaceWorldId != snapshot.WorldId))
        {
            lifePaceChoice.Select(lifePaceChoice.GetItemIndex(rate));
            lastObservedLifePace = rate;
            lastLifePaceWorldId = snapshot.WorldId;
        }
        worldUrlInput.Editable = registration is null && pendingPairing is null && !isPairingOperation && !isOwnerAction && !isRefreshing;
        connectButton.Disabled = registeredEndpointInvalid || pendingPairing is not null || isPairingOperation || isOwnerAction || isRefreshing;
        pairAgainButton.Visible = registration is not null;
        pairAgainButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
        pauseButton.Disabled = actionDisabled || snapshot is null;
        var infantSelected = selected?.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant") == true;
        var deceasedSelected = selected?.Lifecycle == "dead";
        submitInstructionButton.Disabled = actionDisabled || selected is null || selected.IsDraft || infantSelected || deceasedSelected;
        submitInstructionButton.TooltipText = deceasedSelected ? "Historical profiles cannot receive instructions." :
            infantSelected ? "Direct care through an adult caregiver." : "Send an instruction to this inhabitant.";
        submitAuthoringButton.Disabled = actionDisabled || !paused;
        authoringKind.Disabled = actionDisabled || !paused;
        authoringId.Editable = !actionDisabled && paused;
        authoringValue.Editable = !actionDisabled && paused;
        authoringSecondaryValue.Editable = !actionDisabled && paused;
        authoringX.Editable = !actionDisabled && paused;
        authoringY.Editable = !actionDisabled && paused;
        authoringRenewable.Disabled = actionDisabled || !paused;
        instructionKind.Disabled = actionDisabled || deceasedSelected;
        instructionText.Editable = !actionDisabled && !deceasedSelected;
        retryPendingSubmissionButton.Disabled = !paired || isOwnerAction || pendingSubmission is null;
        forgetPendingSubmissionButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
        pairingApprovalId.Editable = !actionDisabled;
        pairingApprovalCode.Editable = !actionDisabled;
        approvePairingButton.Disabled = actionDisabled;
        refreshDevicesButton.Disabled = actionDisabled;
        revokeDeviceId.Editable = !actionDisabled;
        revokeDeviceButton.Disabled = actionDisabled;
        cognitionRoleChoice.Disabled = actionDisabled;
        cognitionProviderChoice.Disabled = actionDisabled;
        cognitionModelInput.Editable = !actionDisabled && SelectedProviderId() != "deterministic";
        cognitionApiKeyInput.Editable = !actionDisabled && SelectedProviderId() != "deterministic";
        saveCognitionProviderButton.Disabled = actionDisabled;
        refreshCognitionProviderButton.Disabled = actionDisabled;
        var selectedProvider = SelectedProviderId();
        var selectedProviderStatus = providerConfiguration?.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, selectedProvider, StringComparison.Ordinal));
        forgetCognitionCredentialButton.Disabled = actionDisabled || selectedProvider == "deterministic" ||
            selectedProviderStatus?.HasCredential != true;
        // A public key can have only one pending server pairing. Keep the
        // visible comparison value stable until it expires or activates.
        pairButton.Disabled = isPairingOperation || deviceKey is null || pendingPairing is not null || registration is not null;
        forgetRegistrationButton.Disabled = isPairingOperation || registration is null;
        RefreshCreationAvailability();
    }

    private string SelectedAuthoringKind() => authoringKind.GetItemText(authoringKind.Selected);

    private static string? EmptyToNull(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string Positions(IReadOnlyList<OwnerWorldPosition> positions) => positions.Count == 0
        ? "none"
        : string.Join(", ", positions.Select(position => $"{position.X},{position.Y}"));

    private static void ConfigureTextPanel(RichTextLabel label, float minimumHeight)
    {
        label.BbcodeEnabled = false;
        label.FitContent = false;
        label.CustomMinimumSize = new Vector2(0, minimumHeight);
        label.ScrollActive = true;
    }

    private void ApplyResponsiveLayout()
    {
        var viewport = mapCanvas.Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }

        climateLabel.Visible = Size.X >= 1100;

        if (observationSession.Current?.Baseline.Snapshot is { Tiles.Count: > 0 } snapshot)
        {
            var previousTileSize = currentTileSize;
            UpdateMapGeometry(snapshot);
            if (previousTileSize != currentTileSize)
            {
                RenderMap(snapshot);
            }
            else
            {
                PositionSelectedInhabitantCard(snapshot);
            }
        }

        rosterPanel.Position = new Vector2(14, 14);
        settlementPanel.Position = new Vector2(14, 14);
        worldInfoPanel.Position = new Vector2(14, 14);
        eventsPanel.Position = new Vector2(
            Math.Max(14, viewport.X - Math.Max(eventsPanel.Size.X, eventsPanel.CustomMinimumSize.X) - 14),
            14);
        var noticeSize = eventNoticePanel.GetCombinedMinimumSize();
        eventNoticePanel.Position = new Vector2(
            Math.Max(14, (viewport.X - noticeSize.X) / 2), 14);
        var familySize = new Vector2(Math.Clamp(viewport.X - 28, 320, 840),
            Math.Clamp(viewport.Y - 28, 280, 600));
        familyTreePanel.Size = familySize;
        familyTreePanel.Position = new Vector2(
            Math.Max(14, (viewport.X - familySize.X) / 2),
            Math.Max(14, (viewport.Y - familySize.Y) / 2));
        var memoriesSize = new Vector2(Math.Clamp(viewport.X - 28, 320, 600),
            Math.Clamp(viewport.Y - 28, 280, 430));
        memoriesPanel.Size = memoriesSize;
        memoriesPanel.Position = new Vector2(
            Math.Max(14, (viewport.X - memoriesSize.X) / 2),
            Math.Max(14, (viewport.Y - memoriesSize.Y) / 2));

        var menuWidth = Math.Min(560, Math.Max(320, viewport.X - 28));
        gameMenuPanel.CustomMinimumSize = new Vector2(menuWidth, 0);

        var toastSize = statusToast.GetCombinedMinimumSize();
        statusToast.Position = new Vector2(
            Math.Max(14, (viewport.X - toastSize.X) / 2),
            Math.Max(14, viewport.Y - toastSize.Y - 18));
    }

    private void UpdateMapGeometry(OwnerWorldSnapshot snapshot)
    {
        if (snapshot.Tiles.Count == 0 || mapCanvas.Size.X <= 0 || mapCanvas.Size.Y <= 0)
        {
            return;
        }

        var mapWidth = snapshot.Tiles.Max(tile => tile.X) + 1;
        var mapHeight = snapshot.Tiles.Max(tile => tile.Y) + 1;
        var availableWidth = Math.Max(1, mapCanvas.Size.X - 36 - ((mapWidth - 1) * TileGap));
        var availableHeight = Math.Max(1, mapCanvas.Size.Y - 36 - ((mapHeight - 1) * TileGap));
        var fittedTileSize = (int)Math.Floor(Math.Min(availableWidth / mapWidth, availableHeight / mapHeight));
        var baseTileSize = Math.Clamp(fittedTileSize, 12, 220);
        currentTileSize = Math.Clamp((int)MathF.Round(baseTileSize * cameraZoom), 12, 880);

        var stageSize = new Vector2(
            (mapWidth * currentTileSize) + ((mapWidth - 1) * TileGap),
            (mapHeight * currentTileSize) + ((mapHeight - 1) * TileGap));
        mapStage.Size = stageSize;
        var stride = currentTileSize + TileGap;
        mapStage.Position = new Vector2(
            CameraAxis(cameraCenterTiles.X, stageSize.X, mapCanvas.Size.X, stride),
            CameraAxis(cameraCenterTiles.Y, stageSize.Y, mapCanvas.Size.Y, stride));
        cameraCenterTiles = new Vector2(
            (mapCanvas.Size.X / 2 - mapStage.Position.X) / stride,
            (mapCanvas.Size.Y / 2 - mapStage.Position.Y) / stride);
        RefreshOverviewViewport(mapWidth, mapHeight, stride);
    }

    private static float CameraAxis(float centerTile, float stagePixels, float viewportPixels, float stride) =>
        stagePixels <= viewportPixels
            ? (viewportPixels - stagePixels) / 2
            : Math.Clamp((viewportPixels / 2) - (centerTile * stride), viewportPixels - stagePixels, 0);

    private void RefreshOverviewViewport(int mapWidth, int mapHeight, float stride)
    {
        var left = Math.Clamp(-mapStage.Position.X / stride, 0, mapWidth);
        var top = Math.Clamp(-mapStage.Position.Y / stride, 0, mapHeight);
        var right = Math.Clamp((mapCanvas.Size.X - mapStage.Position.X) / stride, 0, mapWidth);
        var bottom = Math.Clamp((mapCanvas.Size.Y - mapStage.Position.Y) / stride, 0, mapHeight);
        worldOverview.SetVisibleTiles(new Rect2(left, top, right - left, bottom - top));
    }

    private void CenterCameraAt(Vector2 tileCenter)
    {
        if (renderedMapSnapshot is not { Tiles.Count: > 0 } snapshot)
        {
            return;
        }

        cameraCenterTiles = tileCenter;
        UpdateMapGeometry(snapshot);
        PositionSelectedInhabitantCard(snapshot);
    }

    private void PanCamera(Vector2 deltaTiles)
    {
        if (renderedMapSnapshot is not { Tiles.Count: > 0 })
        {
            return;
        }

        CenterCameraAt(cameraCenterTiles + deltaTiles);
    }

    private void HandleMapInput(InputEvent @event)
    {
        if (gameMenuPanel.Visible || creationOverlay.Visible ||
            renderedMapSnapshot is not { Tiles.Count: > 0 } snapshot)
        {
            return;
        }

        if (@event is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Middle)
            {
                draggingMap = mouse.Pressed;
                mapCanvas.AcceptEvent();
            }
            else if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var nextZoom = Math.Clamp(cameraZoom * (mouse.ButtonIndex == MouseButton.WheelUp ? 1.25f : 0.8f), 1, 4);
                if (Math.Abs(nextZoom - cameraZoom) > 0.001f)
                {
                    cameraZoom = nextZoom;
                    RenderMap(snapshot);
                    PositionSelectedInhabitantCard(snapshot);
                }
                mapCanvas.AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion && draggingMap)
        {
            PanCamera(-motion.Relative / (currentTileSize + TileGap));
            mapCanvas.AcceptEvent();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key || gameMenuPanel.Visible ||
            creationOverlay.Visible || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
        {
            return;
        }

        var direction = key.Keycode switch
        {
            Key.W or Key.Up => new Vector2(0, -1),
            Key.A or Key.Left => new Vector2(-1, 0),
            Key.S or Key.Down => new Vector2(0, 1),
            Key.D or Key.Right => new Vector2(1, 0),
            _ => Vector2.Zero,
        };
        if (direction != Vector2.Zero)
        {
            PanCamera(direction * 1.5f);
            GetViewport().SetInputAsHandled();
        }
    }

    private void PositionSelectedInhabitantCard(OwnerWorldSnapshot snapshot)
    {
        if (!selectedInhabitantCard.Visible || mapCanvas.Size.X <= 0 || mapCanvas.Size.Y <= 0)
        {
            return;
        }

        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (inhabitant is null)
        {
            return;
        }

        var cardWidth = Math.Min(370, Math.Max(300, mapCanvas.Size.X - 24));
        selectedInhabitantCard.CustomMinimumSize = new Vector2(cardWidth, 0);
        var cardSize = selectedInhabitantCard.GetCombinedMinimumSize();
        if (string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase))
        {
            selectedInhabitantCard.Position = new Vector2(Math.Max(12, mapCanvas.Size.X - cardWidth - 12), 12);
            return;
        }
        var stride = currentTileSize + TileGap;
        var actorCenter = mapStage.Position + new Vector2(
            (inhabitant.Position.X * stride) + (currentTileSize / 2f),
            (inhabitant.Position.Y * stride) + (currentTileSize / 2f));
        var x = Math.Clamp(actorCenter.X - (cardWidth / 2), 12, Math.Max(12, mapCanvas.Size.X - cardWidth - 12));
        var y = actorCenter.Y - (currentTileSize / 2f) - cardSize.Y - 12;
        if (y < 12)
        {
            y = actorCenter.Y + (currentTileSize / 2f) + 12;
        }

        y = Math.Clamp(y, 12, Math.Max(12, mapCanvas.Size.Y - cardSize.Y - 12));
        selectedInhabitantCard.Position = new Vector2(x, y);
    }

    private static PanelContainer NewPanel(string title, Control content)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        AddPanelContents(panel, title, content);
        return panel;
    }

    private static void AddPanelContents(PanelContainer panel, Control content) => AddPanelContents(panel, string.Empty, content);

    private static void AddPanelContents(PanelContainer panel, string title, Control content)
    {
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 7);
        if (!string.IsNullOrWhiteSpace(title))
        {
            var heading = new Label { Text = title };
            heading.AddThemeFontSizeOverride("font_size", 15);
            body.AddChild(heading);
        }

        body.AddChild(content);
        margin.AddChild(body);
        panel.AddChild(margin);
    }

    private static HBoxContainer MetricRow(string caption, Label value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var label = new Label
        {
            Text = caption,
            CustomMinimumSize = new Vector2(82, 0),
        };
        label.Modulate = new Color("8FA5A7");
        row.AddChild(label);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(value);
        return row;
    }

    private static void StyleButton(Button button, bool primary = false)
    {
        button.CustomMinimumSize = new Vector2(0, 34);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(
            primary ? new Color("2C706B") : new Color("20343B"),
            primary ? new Color("80CDBA") : new Color("49656A")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(
            primary ? new Color("38877E") : new Color("2B464D"),
            new Color("B0DFCE")));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(
            primary ? new Color("225A58") : new Color("182A31"),
            new Color("D8C6A5")));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(
            new Color("17232A"),
            new Color("2A3A40")));
        button.AddThemeColorOverride("font_color", new Color("E5EFEA"));
        button.AddThemeColorOverride("font_hover_color", new Color("FFFFFF"));
        button.AddThemeColorOverride("font_pressed_color", new Color("FFFFFF"));
        button.AddThemeColorOverride("font_disabled_color", new Color("718486"));
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = border,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 7,
        ContentMarginBottom = 7,
    };

    private static StyleBoxFlat InnerPanelStyle() => new()
    {
        BgColor = new Color("111D24"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = new Color("263D44"),
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    private static StyleBoxFlat TileStyle(Color color) => new()
    {
        BgColor = color,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = color.Darkened(0.3f),
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color("192631"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = new Color("345363"),
        CornerRadiusTopLeft = 10,
        CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10,
        CornerRadiusBottomRight = 10,
    };

    private static StyleBoxFlat TopBarStyle() => new()
    {
        BgColor = new Color("162127"),
        BorderWidthBottom = 1,
        BorderColor = new Color("314A4A"),
        ContentMarginLeft = 0,
        ContentMarginRight = 0,
        ContentMarginTop = 0,
        ContentMarginBottom = 0,
    };

    private Uri ResolveWorldUri()
    {
        if (!WorldServerOrigin.TryResolve(worldUrlInput.Text, out var configuredWorldUri))
        {
            throw new InvalidOperationException("World URL must be an absolute HTTPS origin (or loopback HTTP for local development).");
        }

        if (registration is null)
        {
            if (pendingPairingOrigin is not null)
            {
                if (!WorldServerOrigin.Same(configuredWorldUri, pendingPairingOrigin))
                {
                    throw new InvalidOperationException("This pending pairing is pinned to the server that created it. Wait for it to expire or forget the local registration before changing servers.");
                }

                return pendingPairingOrigin;
            }

            return configuredWorldUri;
        }

        if (!WorldServerOrigin.TryResolve(registration.WorldUrl, out var pinnedWorldUri) ||
            !WorldServerOrigin.Same(configuredWorldUri, pinnedWorldUri))
        {
            throw new InvalidOperationException("This paired device is pinned to its original server origin. Forget the local registration before pairing it with a different server.");
        }

        return pinnedWorldUri;
    }

    private static string ConfiguredWorldUrl() => ProjectSettings
        .GetSetting("clankerworld/world_url", "http://127.0.0.1:5188")
        .AsString();

    private static bool TryGetCommandLineWorldUrl(out string? worldUrl)
    {
        var argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--world-url=", StringComparison.Ordinal));
        worldUrl = argument is null ? null : argument["--world-url=".Length..];
        return !string.IsNullOrWhiteSpace(worldUrl);
    }

    private void SetStatus(string text, bool good)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            statusToast.Hide();
            return;
        }

        statusLabel.Text = text;
        statusLabel.Modulate = new Color(good ? "B9E8C5" : "F0B6A6");
        if (creationOverlay.Visible)
        {
            designStatus.Text = text;
            designStatus.Modulate = statusLabel.Modulate;
        }
        statusToast.Show();
        ApplyResponsiveLayout();
    }

    private void ShowHeldState(string reason)
    {
        var heldTick = observationSession.Current?.Baseline.Snapshot.WorldTick;
        SetStatus(
            heldTick is null
                ? $"disconnected · {reason}"
                : $"disconnected · holding accepted tick {heldTick} · {reason}",
            good: false);
    }

    private static string FriendlyFailure(Exception exception) => exception switch
    {
        System.Net.Http.HttpRequestException requestException when requestException.StatusCode is not null =>
            $"HTTP {(int)requestException.StatusCode.Value} {requestException.StatusCode.Value}",
        _ => exception.Message,
    };

    private static string DescribeWorldEvent(OwnerWorldEvent worldEvent, OwnerWorldSnapshot? snapshot)
    {
        var parts = worldEvent.Detail.Split(':', StringSplitOptions.RemoveEmptyEntries);
        string NameAt(int index)
        {
            if (index >= parts.Length)
            {
                return "Someone";
            }

            return snapshot?.Inhabitants.FirstOrDefault(inhabitant => inhabitant.Id == parts[index])?.DisplayName
                ?? GameUiText.HumanizeIdentifier(parts[index]);
        }

        string ThingAt(int index) => index < parts.Length
            ? GameUiText.HumanizeIdentifier(parts[index])
            : "something new";

        return worldEvent.Kind switch
        {
            "world_created" => "A new world has begun.",
            "weather_changed" when parts.Length >= 2 => $"The weather changed to {ThingAt(1)}.",
            "building_placed" => $"{ThingAt(1)} was built.",
            "build_started" => $"Work began on {ThingAt(1)}.",
            "build_completed" => $"{ThingAt(1)} is ready.",
            "recipe_started" => $"Work began on {ThingAt(1)}.",
            "recipe_completed" => $"{ThingAt(1)} was finished.",
            "food_harvested" => $"{NameAt(0)} gathered food.",
            "food_consumed" => $"{NameAt(0)} ate.",
            "inhabitant_slept" => $"{NameAt(0)} slept.",
            "child_born" => $"{NameAt(0)} was born.",
            "inhabitant_removed" => $"{NameAt(0)} died.",
            "inhabitant_building_proposed" => $"{NameAt(0)} proposed a new building design.",
            "settlement_founded" => "A new settlement was founded.",
            "paused" => "The world was paused.",
            "resumed" => "The world resumed.",
            _ => $"{GameUiText.HumanizeIdentifier(worldEvent.Kind)}.",
        };
    }

    private static string PositionKey(OwnerWorldPosition position) => $"{position.X},{position.Y}";

    private static string ActorLabel(string displayName)
    {
        var trimmed = displayName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "?";
        }

        return trimmed.Length <= 8 ? trimmed : $"{trimmed[..7]}…";
    }

    private static string ActivityGlyph(string? candidateId) => candidateId switch
    {
        "seek_food" => "→",
        "harvest_food" => "✦",
        "consume_food" => "♥",
        "sleep" => "z",
        not null when candidateId.StartsWith("build:", StringComparison.Ordinal) => "◆",
        "safe_idle" => "·",
        _ => "○",
    };

    private static string TerrainMarker(string terrain) => terrain switch
    {
        "water" => "≈",
        "mountain" => "▲",
        _ => string.Empty,
    };

    private static string ResourceMarker(string kind) => kind switch
    {
        "food" => "FOOD",
        "construction" => "WOOD",
        "stone" => "STONE",
        "fiber" => "FIBER",
        "seed" => "SEEDS",
        _ => ShortMarker(kind),
    };

    private static string ResourceGlyph(string kind) => kind switch
    {
        "food" => "●",
        "construction" => "▰",
        "stone" => "⬟",
        "fiber" => "♧",
        "seed" => "✦",
        _ => "◆",
    };

    private static string ObjectMarker(string kind) => kind switch
    {
        "bedroll" => "REST",
        "campfire" => "FIRE",
        "shelter" => "HOME",
        "tree" => "TREE",
        _ => ShortMarker(kind),
    };

    private static string ObjectGlyph(string kind) => kind switch
    {
        "bedroll" => "▰",
        "campfire" => "✦",
        "shelter" => "⌂",
        "tree" => "♣",
        _ => "■",
    };

    private static string ShortMarker(string value)
    {
        var compact = value.Trim().Replace('_', ' ');
        return compact.Length <= 6 ? compact.ToUpperInvariant() : $"{compact[..5].ToUpperInvariant()}…";
    }

    private static string Pretty(string value) => string.IsNullOrWhiteSpace(value)
        ? "unknown"
        : string.Join(' ', value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static int NeedPercent(int basisPoints) => Math.Clamp(basisPoints / 100, 0, 100);

    private static StyleBoxFlat ActorStyle(bool selected) => new()
    {
        BgColor = selected ? new Color("D98B53") : new Color("B86F48"),
        BorderWidthLeft = selected ? 4 : 2,
        BorderWidthTop = selected ? 4 : 2,
        BorderWidthRight = selected ? 4 : 2,
        BorderWidthBottom = selected ? 4 : 2,
        BorderColor = selected ? new Color("FFF0B5") : new Color("F4C78A"),
        CornerRadiusTopLeft = 18,
        CornerRadiusTopRight = 18,
        CornerRadiusBottomLeft = 18,
        CornerRadiusBottomRight = 18,
    };

}
