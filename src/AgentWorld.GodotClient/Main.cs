using AgentWorld.GodotClient.ClientState;
using AgentWorld.GodotClient.Pairing;
using AgentWorld.GodotClient.UI;
using Godot;

namespace AgentWorld.GodotClient;

/// <summary>
/// The deliberately practical Phase 2 owner client. It renders only signed,
/// server-issued world projections; all control buttons submit a one-use
/// device-key proof to the server and never mutate a local simulation copy.
/// </summary>
public partial class Main : Control
{
    private const int TileSize = 46;
    private const int RefreshSeconds = 1;

    private readonly System.Net.Http.HttpClient httpClient = new();
    private readonly OwnerWorldApi ownerApi;
    private readonly OwnerWorldObservationSession observationSession = new();
    private readonly OwnerDeviceRegistrationStore registrationStore = new();
    private readonly OwnerPendingSubmissionStore pendingSubmissionStore = new(
        ProjectSettings.GlobalizePath("user://owner-pending-submission.json"));
    private readonly Dictionary<long, OwnerWorldEvent> knownEvents = [];

    private readonly Label statusLabel = new();
    private readonly PanelContainer connectionPanel = new();
    private readonly Button connectionSettingsButton = new();
    private readonly LineEdit worldUrlInput = new();
    private readonly Button connectButton = new();
    private readonly PanelContainer pairingPanel = new();
    private readonly Label pairingInstructionLabel = new();
    private readonly Label pairingCodeLabel = new();
    private readonly Label pairingIdLabel = new();
    private readonly Label pairingExpiryLabel = new();
    private readonly Button pairButton = new();
    private readonly Button forgetRegistrationButton = new();

    private readonly GridContainer worldGrid = new();
    private readonly Control mapCanvas = new();
    private readonly Control entityLayer = new();
    private readonly PanelContainer selectedInhabitantCard = new();
    private readonly Label selectedActorNameLabel = new();
    private readonly Label selectedActorSummaryLabel = new();
    private readonly Button clearSelectionButton = new();
    private readonly ItemList inhabitantList = new();
    private readonly RichTextLabel inhabitantDetails = new();
    private readonly RichTextLabel worldDetails = new();
    private readonly RichTextLabel eventLog = new();
    private readonly Button rosterToggleButton = new();
    private readonly PanelContainer rosterPanel = new();

    private readonly Button pauseButton = new();
    private readonly Button resumeButton = new();
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
    private OwnerPendingSubmission? pendingSubmission;
    private string? selectedInhabitantId;
    private bool isRefreshing;
    private bool isPairingOperation;
    private bool isOwnerAction;
    private bool registeredEndpointInvalid;

    public Main()
    {
        ownerApi = new OwnerWorldApi(httpClient);
    }

    public override void _Ready()
    {
        BuildLayout();
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
                pairingInstructionLabel.Text = "This saved device registration has no valid pinned server endpoint. Forget the local registration, then pair this Windows key again at the intended HTTPS host.";
                SetStatus("saved owner endpoint is invalid · re-pair required", good: false);
                RefreshControlAvailability();
                return;
            }

            worldUrlInput.Text = storedWorldUri.AbsoluteUri;
            LoadPendingSubmission();

            pairingPanel.Hide();
            connectionPanel.Hide();
            connectionSettingsButton.Text = "Server settings";
            SetStatus("paired device loaded · requesting signed owner observation", good: true);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            pairingPanel.Show();
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
        pairedDeviceList.Clear();
        pendingSubmission = null;
        _ = pendingSubmissionStore.TryForget();
        RenderPendingSubmission();
        knownEvents.Clear();
        pairingPanel.Show();
        connectionPanel.Show();
        connectionSettingsButton.Text = "Hide server settings";
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

            Render(reconnect.Baseline.Snapshot, reconnect.Baseline.Events.Events);
            SetStatus(
                $"paired owner · protocol {reconnect.Handshake.Protocol.Major}.{reconnect.Handshake.Protocol.Minor} · tick {reconnect.Baseline.Snapshot.WorldTick} · signed observation",
                good: true);
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
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 10);
        var title = new Label { Text = "AGENTWORLD" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        connectionSettingsButton.Text = "Server settings";
        connectionSettingsButton.Pressed += ToggleConnectionSettings;
        titleRow.AddChild(connectionSettingsButton);
        root.AddChild(titleRow);
        statusLabel.Text = "initializing Windows owner device…";
        root.AddChild(statusLabel);

        BuildConnectionPanel();
        root.AddChild(connectionPanel);
        BuildPairingPanel();
        root.AddChild(pairingPanel);

        var content = new HBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 12);
        root.AddChild(content);

        BuildWorldColumn(content);
        BuildInspectorColumn(content);
        BuildOwnerColumn(content);
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
        AddPanelContents(connectionPanel, "Server connection", body);
    }

    private void ToggleConnectionSettings()
    {
        connectionPanel.Visible = !connectionPanel.Visible;
        connectionSettingsButton.Text = connectionPanel.Visible ? "Hide server settings" : "Server settings";
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

    private void BuildWorldColumn(HBoxContainer content)
    {
        mapCanvas.CustomMinimumSize = new Vector2(520, 420);
        mapCanvas.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mapCanvas.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        worldGrid.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        worldGrid.AddThemeConstantOverride("h_separation", 2);
        worldGrid.AddThemeConstantOverride("v_separation", 2);
        worldGrid.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapCanvas.AddChild(worldGrid);

        entityLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        entityLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapCanvas.AddChild(entityLayer);
        BuildSelectedInhabitantCard();

        var panel = NewPanel("World map · click a character to inspect and direct them", mapCanvas);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(panel);
    }

    private void BuildInspectorColumn(HBoxContainer content)
    {
        var body = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        inhabitantList.CustomMinimumSize = new Vector2(0, 110);
        inhabitantList.ItemSelected += index => SelectInhabitantFromList(index);
        rosterToggleButton.Text = "Show inhabitant roster";
        rosterToggleButton.Pressed += () =>
        {
            rosterPanel.Visible = !rosterPanel.Visible;
            rosterToggleButton.Text = rosterPanel.Visible ? "Hide inhabitant roster" : "Show inhabitant roster";
        };
        body.AddChild(rosterToggleButton);
        rosterPanel.AddChild(inhabitantList);
        rosterPanel.Hide();
        body.AddChild(rosterPanel);

        ConfigureTextPanel(inhabitantDetails, 300);
        body.AddChild(NewPanel("Selected inhabitant", inhabitantDetails));
        var panel = NewPanel("Inhabitant", body);
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(panel);
    }

    private void BuildOwnerColumn(HBoxContainer content)
    {
        var body = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);

        var controlRow = new HBoxContainer();
        pauseButton.Text = "Pause world";
        pauseButton.Pressed += () => _ = SetPausedAsync(paused: true);
        controlRow.AddChild(pauseButton);
        resumeButton.Text = "Resume world";
        resumeButton.Pressed += () => _ = SetPausedAsync(paused: false);
        controlRow.AddChild(resumeButton);
        body.AddChild(NewPanel("World", controlRow));

        developerToggleButton.Text = "Developer tools";
        developerToggleButton.Pressed += () =>
        {
            developerScroll.Visible = !developerScroll.Visible;
            developerToggleButton.Text = developerScroll.Visible ? "Hide developer tools" : "Developer tools";
        };
        body.AddChild(developerToggleButton);

        developerScroll.CustomMinimumSize = new Vector2(0, 420);
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

        ConfigureTextPanel(worldDetails, 180);
        developerBody.AddChild(NewPanel("World projection details", worldDetails));
        ConfigureTextPanel(eventLog, 210);
        developerBody.AddChild(NewPanel("Ordered event history", eventLog));
        developerScroll.Hide();
        body.AddChild(developerScroll);
        content.AddChild(body);
        UpdateAuthoringHint();
    }

    private void BuildSelectedInhabitantCard()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        selectedActorNameLabel.AddThemeFontSizeOverride("font_size", 18);
        body.AddChild(selectedActorNameLabel);
        selectedActorSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(selectedActorSummaryLabel);

        clearSelectionButton.Text = "Clear selection";
        clearSelectionButton.Pressed += ClearInhabitantSelection;
        body.AddChild(clearSelectionButton);

        var instructionHeading = new Label { Text = "Give them a direction" };
        instructionHeading.AddThemeFontSizeOverride("font_size", 13);
        body.AddChild(instructionHeading);
        instructionKind.AddItem("Suggestion", 0);
        instructionKind.AddItem("Must-do", 1);
        body.AddChild(instructionKind);
        instructionText.PlaceholderText = "What should they do?";
        body.AddChild(instructionText);
        submitInstructionButton.Text = "Instruct inhabitant";
        submitInstructionButton.Pressed += () => _ = SubmitInstructionAsync();
        body.AddChild(submitInstructionButton);

        AddPanelContents(selectedInhabitantCard, body);
        selectedInhabitantCard.CustomMinimumSize = new Vector2(250, 0);
        selectedInhabitantCard.ZIndex = 20;
        selectedInhabitantCard.Hide();
        mapCanvas.AddChild(selectedInhabitantCard);
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

        RenderInhabitantList(snapshot);
        RenderMap(snapshot);
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

        if (snapshot.Tiles.Count == 0)
        {
            return;
        }

        worldGrid.Columns = snapshot.Tiles.Max(tile => tile.X) + 1;
        var objects = snapshot.Objects.ToDictionary(item => PositionKey(item.Position));
        var resources = snapshot.Resources.ToDictionary(item => PositionKey(item.Position));
        var drafts = snapshot.Inhabitants
            .Where(inhabitant => inhabitant.IsDraft)
            .ToDictionary(inhabitant => PositionKey(inhabitant.Position));
        foreach (var tile in snapshot.Tiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X))
        {
            var key = $"{tile.X},{tile.Y}";
            var cell = new Button
            {
                Text = drafts.ContainsKey(key) ? "◇" : resources.ContainsKey(key) ? "•" : objects.ContainsKey(key) ? "◆" : string.Empty,
                CustomMinimumSize = new Vector2(TileSize, TileSize),
                TooltipText = $"{tile.Terrain} at {key}",
            };
            cell.AddThemeStyleboxOverride("normal", TileStyle(TerrainColor(tile.Terrain)));
            cell.AddThemeStyleboxOverride("hover", TileStyle(TerrainColor(tile.Terrain).Lightened(0.15f)));
            if (drafts.TryGetValue(key, out var draft))
            {
                cell.TooltipText = $"{draft.DisplayName} · authoring draft at {key}";
            }

            worldGrid.AddChild(cell);
        }

        foreach (var group in snapshot.Inhabitants
            .GroupBy(inhabitant => PositionKey(inhabitant.Position)))
        {
            var occupants = group.ToArray();
            for (var index = 0; index < occupants.Length; index++)
            {
                var inhabitant = occupants[index];
                var offsetX = (index % 2) * 30 + 7;
                var offsetY = (index / 2) * 30 + 7;
                var actorButton = new Button
                {
                    Text = ActorInitial(inhabitant.DisplayName),
                    TooltipText = $"{inhabitant.DisplayName} · {Pretty(inhabitant.Lifecycle)}",
                    Position = new Vector2(inhabitant.Position.X * TileSize + offsetX, inhabitant.Position.Y * TileSize + offsetY),
                    CustomMinimumSize = new Vector2(50, 50),
                    ZIndex = 10,
                };
                actorButton.AddThemeColorOverride("font_color", Colors.White);
                actorButton.AddThemeFontSizeOverride("font_size", 18);
                actorButton.AddThemeStyleboxOverride(
                    "normal",
                    ActorStyle(string.Equals(inhabitant.Id, selectedInhabitantId, StringComparison.Ordinal)));
                actorButton.AddThemeStyleboxOverride("hover", ActorStyle(selected: true));
                actorButton.Pressed += () => SelectInhabitant(inhabitant.Id);
                entityLayer.AddChild(actorButton);
            }
        }

        CallDeferred(nameof(PositionSelectedInhabitantCard));
    }

    private void RenderInhabitantList(OwnerWorldSnapshot snapshot)
    {
        var previousSelection = selectedInhabitantId;
        var selectionFound = false;
        inhabitantList.Clear();
        for (var index = 0; index < snapshot.Inhabitants.Count; index++)
        {
            var inhabitant = snapshot.Inhabitants[index];
            inhabitantList.AddItem($"{inhabitant.DisplayName} · {inhabitant.Lifecycle}");
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
            inhabitantDetails.AppendText("Select an inhabitant to inspect its server projection.");
            return;
        }

        var inventory = inhabitant.Inventory.Count == 0
            ? "none"
            : string.Join(", ", inhabitant.Inventory.Select(item => $"{item.Kind}: {item.Quantity}"));
        var currentActivity = string.IsNullOrWhiteSpace(inhabitant.Route.Status)
            ? "wandering"
            : Pretty(inhabitant.Route.Status);
        inhabitantDetails.AppendText(
            $"{inhabitant.DisplayName}\n" +
            $"{Pretty(inhabitant.Lifecycle)} · {currentActivity}\n\n" +
            $"Current position\n{inhabitant.Position.X}, {inhabitant.Position.Y}\n\n" +
            $"Needs\n" +
            $"Hunger: {NeedPercent(inhabitant.HungerBasisPoints)}%\n" +
            $"Energy: {NeedPercent(inhabitant.EnergyBasisPoints)}%\n\n" +
            $"Carrying\n{inventory}\n\n" +
            $"This is the friendly view. Technical decision traces and map digests live in Developer tools.");
    }

    private void RenderSelectedInhabitantCard(OwnerWorldSnapshot snapshot)
    {
        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (inhabitant is null)
        {
            selectedInhabitantCard.Hide();
            return;
        }

        selectedActorNameLabel.Text = inhabitant.DisplayName;
        selectedActorSummaryLabel.Text =
            $"{Pretty(inhabitant.Lifecycle)} · {Pretty(inhabitant.Route.Status)}\n" +
            $"Hunger {NeedPercent(inhabitant.HungerBasisPoints)}% · Energy {NeedPercent(inhabitant.EnergyBasisPoints)}%";
        selectedInhabitantCard.Show();
        PositionSelectedInhabitantCard();
    }

    private void PositionSelectedInhabitantCard()
    {
        if (observationSession.Current?.Baseline.Snapshot is not { } snapshot ||
            snapshot.Inhabitants.FirstOrDefault(item => string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal)) is not { } inhabitant)
        {
            selectedInhabitantCard.Hide();
            return;
        }

        var preferred = new Vector2(
            inhabitant.Position.X * TileSize + TileSize + 12,
            inhabitant.Position.Y * TileSize + 8);
        var maximum = mapCanvas.Size - selectedInhabitantCard.Size - new Vector2(8, 8);
        selectedInhabitantCard.Position = new Vector2(
            Mathf.Clamp(preferred.X, 8, Mathf.Max(8, maximum.X)),
            Mathf.Clamp(preferred.Y, 8, Mathf.Max(8, maximum.Y)));
    }

    private void RenderWorldDetails(OwnerWorldSnapshot snapshot)
    {
        worldDetails.Clear();
        var authoring = snapshot.Authoring;
        var instructions = snapshot.Instructions.Count == 0
            ? "none"
            : string.Join("\n", snapshot.Instructions.Select(instruction =>
                $"#{instruction.SubmissionSequence} {instruction.Kind} → {instruction.TargetInhabitantId}: {instruction.Text} [{instruction.State}]"));
        if (authoring is null)
        {
            worldDetails.AppendText($"tick {snapshot.WorldTick}\nworld {snapshot.WorldId}\nNo authoring projection returned.");
            return;
        }

        worldDetails.AppendText(
            $"tick {snapshot.WorldTick} · revision {authoring.Revision} · epoch {authoring.RunEpoch}\n" +
            $"state: {(authoring.IsPaused ? "PAUSED — authoring allowed" : "RUNNING — authoring disabled")}\n" +
            $"weather/season: {authoring.Weather} / {authoring.Season}\n" +
            $"current topology: {authoring.CurrentMapManifestDigest}\n" +
            $"initial fixture topology: {authoring.InitialMapManifestDigest}\n" +
            $"approved assets: {(authoring.ApprovedAssetReferences.Count == 0 ? "none" : string.Join(", ", authoring.ApprovedAssetReferences))}\n\n" +
            $"queued instructions:\n{instructions}");
    }

    private void RenderEventLog()
    {
        eventLog.Clear();
        foreach (var worldEvent in knownEvents.Values.OrderBy(item => item.EventId).TakeLast(80))
        {
            eventLog.AppendText($"#{worldEvent.EventId} · tick {worldEvent.WorldTick}\n{worldEvent.Kind}: {worldEvent.Detail}\n\n");
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
        pauseButton.Disabled = actionDisabled || paused;
        resumeButton.Disabled = actionDisabled || !paused;
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

    private static PanelContainer NewPanel(string title, Control content)
    {
        var panel = new PanelContainer();
        AddPanelContents(panel, title, content);
        return panel;
    }

    private static void AddPanelContents(PanelContainer panel, Control content) => AddPanelContents(panel, string.Empty, content);

    private static void AddPanelContents(PanelContainer panel, string title, Control content)
    {
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

    private static StyleBoxFlat TileStyle(Color color) => new()
    {
        BgColor = color,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = color.Darkened(0.25f),
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
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
        statusLabel.Text = text;
        statusLabel.Modulate = new Color(good ? "B9E8C5" : "F0B6A6");
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

    private static string PositionKey(OwnerWorldPosition position) => $"{position.X},{position.Y}";

    private static string ActorInitial(string displayName)
    {
        var trimmed = displayName.Trim();
        return string.IsNullOrEmpty(trimmed) ? "?" : trimmed[..1].ToUpperInvariant();
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
