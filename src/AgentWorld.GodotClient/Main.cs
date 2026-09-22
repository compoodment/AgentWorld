using AgentWorld.GodotClient.ClientState;
using AgentWorld.GodotClient.Pairing;
using AgentWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace AgentWorld.GodotClient;

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
    private readonly Dictionary<long, OwnerWorldEvent> knownEvents = [];
    private readonly Dictionary<string, OwnerWorldPosition> renderedInhabitantPositions =
        new(StringComparer.Ordinal);

    private readonly Label statusLabel = new();
    private readonly PanelContainer statusToast = new();
    private readonly PanelContainer connectionPanel = new();
    private readonly Button connectionSettingsButton = new();
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
    private readonly Label clockLabel = new();
    private readonly Label climateLabel = new();
    private readonly Button inhabitantsButton = new();
    private readonly Button eventsButton = new();
    private readonly Button settlementButton = new();
    private readonly Button menuButton = new();
    private readonly GridContainer worldGrid = new();
    private readonly Control mapCanvas = new();
    private readonly Control mapStage = new();
    private readonly Control objectLayer = new();
    private readonly Control entityLayer = new();
    private readonly Label rosterSummaryLabel = new();
    private readonly PanelContainer selectedInhabitantCard = new();
    private readonly Label selectedActorNameLabel = new();
    private readonly Label selectedActorSummaryLabel = new();
    private readonly Button clearSelectionButton = new();
    private readonly ItemList inhabitantList = new();
    private readonly RichTextLabel inhabitantDetails = new();
    private readonly RichTextLabel inhabitantSocialDetails = new();
    private readonly RichTextLabel worldDetails = new();
    private readonly RichTextLabel eventLog = new();
    private readonly PanelContainer rosterPanel = new();
    private readonly PanelContainer eventsPanel = new();
    private readonly PanelContainer settlementPanel = new();
    private readonly PanelContainer gameMenuPanel = new();
    private readonly PanelContainer settingsPanel = new();
    private readonly ColorRect menuShade = new();
    private readonly Label menuHeadingLabel = new();
    private readonly Button menuResumeButton = new();
    private readonly CheckBox fullscreenToggle = new();
    private readonly OptionButton resolutionChoice = new();

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
    private int currentTileSize = DefaultTileSize;

    public Main()
    {
        ownerApi = new OwnerWorldApi(httpClient);
    }

    public override void _Ready()
    {
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
                    settingsPanel.Visible = settingsVisible;
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
                            throw new InvalidOperationException($"Menu escaped its centered bounds: window={size}, settings={settingsVisible}, selected={selected}, menu={menu}, bounds={bounds}");
                        }
                        settlementPanel.Show();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        if (!mapCanvas.GetGlobalRect().Encloses(settlementPanel.GetGlobalRect()))
                        {
                            throw new InvalidOperationException($"Settlement panel escaped the world viewport: window={size}");
                        }
                        settlementPanel.Hide();
                    }
                }
            }
            GD.Print("UI layout checks passed: centered menu with/without selection and settings, and settlement panel at three window sizes.");
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
        clockLabel.Text = "Connecting…";
        clockLabel.AddThemeFontSizeOverride("font_size", 20);
        clockLabel.AddThemeColorOverride("font_color", new Color("F4F0E3"));
        topBar.AddChild(clockLabel);

        climateLabel.Text = string.Empty;
        climateLabel.Modulate = new Color("AFC4BA");
        climateLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        topBar.AddChild(climateLabel);

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

    private void ToggleConnectionSettings()
    {
        settingsPanel.Visible = !settingsPanel.Visible;
        if (!settingsPanel.Visible)
        {
            cognitionApiKeyInput.Text = string.Empty;
        }

        developerScroll.Hide();
        developerToggleButton.Text = "Developer tools";
        if (settingsPanel.Visible && registration is not null)
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
        AddPanelContents(eventsPanel, "Recent events", eventLog);
        eventsPanel.CustomMinimumSize = new Vector2(390, 360);
        eventsPanel.ZIndex = 80;
        eventsPanel.Hide();
        content.AddChild(eventsPanel);

        ConfigureTextPanel(worldDetails, 320);
        AddPanelContents(settlementPanel, "Settlement · stores and projects", worldDetails);
        settlementPanel.CustomMinimumSize = new Vector2(420, 380);
        settlementPanel.ZIndex = 80;
        settlementPanel.Hide();
        content.AddChild(settlementPanel);
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

        var menuActions = new HBoxContainer();
        menuActions.AddThemeConstantOverride("separation", 6);
        menuResumeButton.Text = "Resume";
        StyleButton(menuResumeButton, primary: true);
        menuResumeButton.Pressed += () => _ = CloseGameMenuAsync();
        menuActions.AddChild(menuResumeButton);

        connectionSettingsButton.Text = "Settings";
        StyleButton(connectionSettingsButton);
        connectionSettingsButton.Pressed += ToggleConnectionSettings;
        menuActions.AddChild(connectionSettingsButton);

        developerToggleButton.Text = "Developer tools";
        StyleButton(developerToggleButton);
        developerToggleButton.Pressed += () =>
        {
            developerScroll.Visible = !developerScroll.Visible;
            settingsPanel.Hide();
            ApplyResponsiveLayout();
        };
        menuActions.AddChild(developerToggleButton);

        var quitButton = new Button { Text = "Quit" };
        StyleButton(quitButton);
        quitButton.Pressed += () => GetTree().Quit();
        menuActions.AddChild(quitButton);
        body.AddChild(menuActions);

        var settingsBody = new VBoxContainer();
        settingsBody.AddThemeConstantOverride("separation", 8);
        fullscreenToggle.Text = "Fullscreen";
        fullscreenToggle.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        fullscreenToggle.Toggled += SetFullscreen;
        settingsBody.AddChild(fullscreenToggle);

        resolutionChoice.AddItem("1280 × 720");
        resolutionChoice.AddItem("1600 × 900");
        resolutionChoice.AddItem("1920 × 1080");
        resolutionChoice.Selected = 0;
        resolutionChoice.ItemSelected += SetWindowResolution;
        settingsBody.AddChild(resolutionChoice);

        BuildCognitionSettingsPanel();
        settingsBody.AddChild(cognitionSettingsPanel);

        BuildConnectionPanel();
        settingsBody.AddChild(connectionPanel);
        BuildPairingPanel();
        settingsBody.AddChild(pairingPanel);
        var settingsScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 380),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        settingsBody.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        settingsScroll.AddChild(settingsBody);
        AddPanelContents(settingsPanel, "Settings", settingsScroll);
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
        settlementPanel.Hide();
        eventsPanel.Hide();
        rosterPanel.Visible = show;
    }

    private void ToggleEvents()
    {
        var show = !eventsPanel.Visible;
        settlementPanel.Hide();
        rosterPanel.Hide();
        eventsPanel.Visible = show;
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
        settingsPanel.Show();
        developerScroll.Hide();
        ApplyResponsiveLayout();
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
        RenderInhabitantDetails(snapshot);
        RenderSelectedInhabitantCard(snapshot);
        RenderWorldDetails(snapshot);
        RenderEventLog();
        RefreshControlAvailability();
    }

    private void RenderMap(OwnerWorldSnapshot snapshot)
    {
        foreach (var child in worldGrid.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var child in entityLayer.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var child in objectLayer.GetChildren())
        {
            child.QueueFree();
        }

        if (snapshot.Tiles.Count == 0)
        {
            return;
        }

        var mapWidth = snapshot.Tiles.Max(tile => tile.X) + 1;
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
            cell.AddThemeStyleboxOverride("normal", TileStyle(TerrainColor(tile.Terrain)));
            cell.AddThemeStyleboxOverride("hover", TileStyle(TerrainColor(tile.Terrain).Lightened(0.15f)));
            worldGrid.AddChild(cell);
        }

        foreach (var resource in snapshot.Resources)
        {
            AddMapObjectVisual(
                resource.Position,
                ResourceGlyph(resource.Kind),
                ResourceMarker(resource.Kind),
                $"{Pretty(resource.Kind)} resource");
        }

        foreach (var mapObject in snapshot.Objects)
        {
            AddMapObjectVisual(
                mapObject.Position,
                ObjectGlyph(mapObject.Kind),
                ObjectMarker(mapObject.Kind),
                Pretty(mapObject.Kind));
        }

        foreach (var group in snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft)
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
            .Where(inhabitant => !inhabitant.IsDraft)
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
        OwnerWorldPosition position,
        string glyph,
        string label,
        string tooltip)
    {
        var stride = currentTileSize + TileGap;
        var visual = new Label
        {
            Text = $"{glyph}\n{label}",
            Position = new Vector2(position.X * stride + 4, position.Y * stride + 4),
            Size = new Vector2(currentTileSize - 8, currentTileSize - 8),
            TooltipText = tooltip,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 5,
        };
        visual.AddThemeFontSizeOverride("font_size", 12);
        visual.AddThemeColorOverride("font_color", new Color("E8F0D8"));
        visual.AddThemeColorOverride("font_shadow_color", new Color("18211D"));
        visual.AddThemeConstantOverride("shadow_offset_x", 1);
        visual.AddThemeConstantOverride("shadow_offset_y", 1);
        objectLayer.AddChild(visual);
    }

    private void RenderWorldHud(OwnerWorldSnapshot snapshot)
    {
        var paused = snapshot.Authoring?.IsPaused == true;
        clockLabel.Text = GameUiText.FormatWorldClock(snapshot.WorldTick);
        climateLabel.Text = snapshot.Authoring is { } authoring
            ? $"{Pretty(authoring.Season)} · {Pretty(authoring.Weather)}"
            : string.Empty;
        pauseButton.Text = paused ? "Play" : "Pause";
        pauseButton.TooltipText = paused ? "Resume the world" : "Pause the world";
        menuResumeButton.Text = menuPausedWorld ? "Resume" : "Close menu";
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
        rosterSummaryLabel.Text = inhabitants.Length == 0
            ? "No one lives here yet."
            : inhabitants.Length == 1 ? "1 inhabitant" : $"{inhabitants.Length} inhabitants";

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
            selectedInhabitantCard.Hide();
            return;
        }

        selectedActorNameLabel.Text = inhabitant.DisplayName;
        selectedActorSummaryLabel.Text = Pretty(inhabitant.Lifecycle);
        var intention = inhabitant.PublicIntention is { } publicIntention
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
        var activity = decision is null
            ? "No decision yet"
            : $"{Pretty(decision.Provider)}{(decision.FellBack ? " (fallback)" : "")} · {GameUiText.HumanizeIdentifier(decision.CandidateId)}";
        var projectText = inhabitant.Project is { } project
            ? $"{project.Label} · {Pretty(project.Stage)} · {project.WorkDone}/{project.WorkRequired}" +
                (project.Blocker is null ? "" : $"\n{project.Blocker}")
            : "No settlement project";
        var socialNotes = inhabitant.SocialNotes.Count == 0 ? "" : "\n" + string.Join("\n", inhabitant.SocialNotes);
        var condition = inhabitant.Survival is { } survival
            ? $"Warmth {survival.WarmthBasisPoints / 100}% · Illness {survival.IllnessBasisPoints / 100}%" +
                $" · Diet {survival.NutritionBasisPoints / 100}%\n" +
                $"{(survival.HasClothing ? "Clothed" : "No warm clothing")} · {(survival.HasTool ? "Tool equipped" : "Working by hand")}\n" : "";
        inhabitantSocialDetails.Text = $"{condition}{(inhabitant.Project is null ? intention : projectText)}\n{relationships}{socialNotes}\n{activity}";
        inhabitantSocialDetails.TooltipText = decision is null ? "" :
            $"Last accepted decision\nRole: {decision.Role ?? "not reported"}\nModel: {decision.Model ?? "not reported"}\nConfidence: {decision.Confidence:P0}\n" +
            $"Latency: {decision.LatencyMilliseconds?.ToString(CultureInfo.CurrentCulture) ?? "—"} ms\n" +
            $"Tokens in/out: {decision.InputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}/{decision.OutputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}";
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
        worldDetails.Text = $"{(authoring.IsPaused ? "Paused" : "Playing")} · {GameUiText.FormatWorldClock(snapshot.WorldTick)}\n" +
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
            eventLog.AppendText(
                $"{GameUiText.FormatWorldClock(worldEvent.WorldTick)}\n" +
                $"{DescribeWorldEvent(worldEvent, snapshot)}\n\n");
        }
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
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var paused = snapshot?.Authoring?.IsPaused == true;
        var selected = snapshot?.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        var actionDisabled = !paired || isOwnerAction || pendingSubmission is not null;
        worldUrlInput.Editable = registration is null && pendingPairing is null && !isPairingOperation && !isOwnerAction && !isRefreshing;
        connectButton.Disabled = registeredEndpointInvalid || pendingPairing is not null || isPairingOperation || isOwnerAction || isRefreshing;
        pairAgainButton.Visible = registration is not null;
        pairAgainButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
        pauseButton.Disabled = actionDisabled || snapshot is null;
        submitInstructionButton.Disabled = actionDisabled || selected is null || selected.IsDraft;
        submitAuthoringButton.Disabled = actionDisabled || !paused;
        authoringKind.Disabled = actionDisabled || !paused;
        authoringId.Editable = !actionDisabled && paused;
        authoringValue.Editable = !actionDisabled && paused;
        authoringSecondaryValue.Editable = !actionDisabled && paused;
        authoringX.Editable = !actionDisabled && paused;
        authoringY.Editable = !actionDisabled && paused;
        authoringRenewable.Disabled = actionDisabled || !paused;
        instructionKind.Disabled = actionDisabled;
        instructionText.Editable = !actionDisabled;
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

        climateLabel.Visible = Size.X >= 820;

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
        eventsPanel.Position = new Vector2(
            Math.Max(14, viewport.X - Math.Max(eventsPanel.Size.X, eventsPanel.CustomMinimumSize.X) - 14),
            14);

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
        currentTileSize = Math.Clamp(fittedTileSize, 12, 220);

        var stageSize = new Vector2(
            (mapWidth * currentTileSize) + ((mapWidth - 1) * TileGap),
            (mapHeight * currentTileSize) + ((mapHeight - 1) * TileGap));
        mapStage.Size = stageSize;
        mapStage.Position = new Vector2(
            Math.Max(0, (mapCanvas.Size.X - stageSize.X) / 2),
            Math.Max(0, (mapCanvas.Size.Y - stageSize.Y) / 2));
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
        .GetSetting("agentworld/world_url", "http://127.0.0.1:5188")
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
            "inhabitant_removed" => $"{NameAt(0)} is no longer in the world.",
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

    private static Color TerrainColor(string terrain) => terrain switch
    {
        "meadow" => new Color("5F8F5B"),
        "water" => new Color("4B7FA7"),
        "mountain" => new Color("756D68"),
        _ => new Color("9B5463"),
    };
}
