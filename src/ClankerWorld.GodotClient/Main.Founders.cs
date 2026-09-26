using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Button founderSetupButton = new();
    private readonly Button addAgentButton = new();
    private readonly Button startWorldButton = new();
    private readonly PanelContainer founderSetupPanel = new();
    private readonly Label founderSetupHint = new();
    private readonly OptionButton founderProviderChoice = new();
    private readonly OptionButton founderCredentialChoice = new();
    private readonly LineEdit founderModelInput = new();
    private readonly LineEdit founderKeyLabelInput = new();
    private readonly LineEdit founderApiKeyInput = new();
    private bool placingAddedAgent;

    private void BuildFounderSetupPanel(Control canvas)
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 7);
        founderSetupHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        founderSetupHint.CustomMinimumSize = new Vector2(320, 0);
        body.AddChild(founderSetupHint);

        founderProviderChoice.AddItem("OpenAI");
        founderProviderChoice.SetItemMetadata(0, "openai");
        founderProviderChoice.AddItem("Ollama Cloud");
        founderProviderChoice.SetItemMetadata(1, "ollama-cloud");
        founderProviderChoice.ItemSelected += _ =>
        {
            founderModelInput.Text = DefaultProviderModel(SelectedFounderProvider());
            PopulateFounderCredentials();
        };
        body.AddChild(founderProviderChoice);

        founderModelInput.PlaceholderText = "Model ID for this agent";
        founderModelInput.Text = DefaultProviderModel("openai");
        body.AddChild(founderModelInput);

        founderCredentialChoice.ItemSelected += _ => RenderFounderCredentialInputs();
        body.AddChild(founderCredentialChoice);
        founderKeyLabelInput.PlaceholderText = "Name this key (for example, Personal account)";
        body.AddChild(founderKeyLabelInput);
        founderApiKeyInput.Secret = true;
        founderApiKeyInput.PlaceholderText = "Paste API key";
        body.AddChild(founderApiKeyInput);

        var close = new Button { Text = "Close" };
        StyleButton(close);
        close.Pressed += () =>
        {
            founderApiKeyInput.Text = string.Empty;
            founderSetupPanel.Hide();
        };
        body.AddChild(close);
        AddPanelContents(founderSetupPanel, "Add an agent", body);
        founderSetupPanel.Position = new Vector2(350, 14);
        founderSetupPanel.ZIndex = 90;
        founderSetupPanel.Hide();
        canvas.AddChild(founderSetupPanel);
        PopulateFounderCredentials();
    }

    private string SelectedFounderProvider() => founderProviderChoice.GetItemMetadata(founderProviderChoice.Selected).AsString();

    private string SelectedFounderCredential() => founderCredentialChoice.GetItemMetadata(founderCredentialChoice.Selected).AsString();

    private void PopulateFounderCredentials()
    {
        founderCredentialChoice.Clear();
        founderCredentialChoice.AddItem("Provider default key");
        founderCredentialChoice.SetItemMetadata(0, "default");
        foreach (var slot in providerConfiguration?.CredentialSlots ?? [])
        {
            if (slot.Provider != SelectedFounderProvider()) continue;
            founderCredentialChoice.AddItem(slot.Label);
            founderCredentialChoice.SetItemMetadata(founderCredentialChoice.ItemCount - 1, slot.Id);
        }
        founderCredentialChoice.AddItem("Add a new API key…");
        founderCredentialChoice.SetItemMetadata(founderCredentialChoice.ItemCount - 1, "new");
        var defaultAvailable = providerConfiguration?.Providers.Any(option =>
            option.Provider == SelectedFounderProvider() && option.HasCredential) == true;
        founderCredentialChoice.Select(founderCredentialChoice.ItemCount > 2 ? 1 : defaultAvailable ? 0 : founderCredentialChoice.ItemCount - 1);
        RenderFounderCredentialInputs();
    }

    private void RenderFounderCredentialInputs()
    {
        var newKey = SelectedFounderCredential() == "new";
        founderKeyLabelInput.Visible = newKey;
        founderApiKeyInput.Visible = newKey;
        if (!newKey)
        {
            founderKeyLabelInput.Text = string.Empty;
            founderApiKeyInput.Text = string.Empty;
        }
    }

    private async Task ToggleFounderSetupAsync()
    {
        placingAddedAgent = false;
        if (founderSetupPanel.Visible)
        {
            founderApiKeyInput.Text = string.Empty;
            founderSetupPanel.Hide();
            return;
        }
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.GetProviderStatusAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            PopulateFounderCredentials();
            founderSetupPanel.Show();
            return "Choose a model and key, then click an empty tile to place the founder";
        });
    }

    private async Task ToggleAddAgentAsync()
    {
        if (founderSetupPanel.Visible && placingAddedAgent)
        {
            founderApiKeyInput.Text = string.Empty;
            founderSetupPanel.Hide();
            placingAddedAgent = false;
            return;
        }
        if (observationSession.Current?.Baseline.Snapshot is not { FounderSetup: { Started: true } }) return;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.GetProviderStatusAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            PopulateFounderCredentials();
            placingAddedAgent = true;
            founderSetupHint.Text = "Choose this adult’s provider, model, and key, then click an empty tile. Unclaimed land starts an independent household; this map has no established property borders yet.";
            founderSetupPanel.Show();
            return "Click an empty land tile to place the new agent";
        });
    }

    private async Task PlaceAgentAtAsync(Vector2I tile)
    {
        if (isOwnerAction || observationSession.Current?.Baseline.Snapshot is not { FounderSetup: { Started: true } } snapshot ||
            !MapContains(snapshot, tile.X, tile.Y) ||
            snapshot.Inhabitants.Any(item => item.Position.X == tile.X && item.Position.Y == tile.Y) ||
            snapshot.Objects.Any(item => item.Position.X == tile.X && item.Position.Y == tile.Y) ||
            snapshot.Resources.Any(item => item.Position.X == tile.X && item.Position.Y == tile.Y))
        {
            SetStatus("Choose an empty passable tile", good: false);
            return;
        }
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var provider = SelectedFounderProvider();
        var model = founderModelInput.Text.Trim();
        var choice = SelectedFounderCredential();
        var newKey = choice == "new";
        if (model.Length == 0 || newKey && (string.IsNullOrWhiteSpace(founderKeyLabelInput.Text) ||
            string.IsNullOrWhiteSpace(founderApiKeyInput.Text)))
        {
            SetStatus("Choose a model and enter both a label and key for a new credential", good: false);
            return;
        }
        var agentId = "agent:" + Guid.NewGuid().ToString("N");
        var cognition = new OwnerProviderConfigurationAction(
            "personal", provider, model, newKey ? founderApiKeyInput.Text : null,
            ForgetCredential: false, InhabitantId: agentId,
            CredentialSlotId: newKey ? Guid.NewGuid().ToString("N") : choice == "default" ? null : choice,
            NewCredentialLabel: newKey ? founderKeyLabelInput.Text.Trim() : null);
        try
        {
            await RunOwnerActionAsync(async () =>
            {
                var receipt = await ownerApi.PlaceAgentAsync(ResolveWorldUri(), authority, deviceId,
                    new OwnerAgentPlacementAction(agentId, tile.X, tile.Y, cognition), signer, CancellationToken.None);
                providerConfiguration = await ownerApi.GetProviderStatusAsync(
                    ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
                placingAddedAgent = false;
                founderSetupPanel.Hide();
                return $"Agent placed in independent household {receipt.HouseholdId}";
            });
        }
        finally
        {
            founderApiKeyInput.Text = string.Empty;
            founderKeyLabelInput.Text = string.Empty;
        }
    }

    private async Task PlaceFounderAtAsync(Vector2I tile)
    {
        if (isOwnerAction || observationSession.Current?.Baseline.Snapshot is not { FounderSetup: { Started: false } } snapshot ||
            !MapContains(snapshot, tile.X, tile.Y) ||
            snapshot.Inhabitants.Any(item => item.Position.X == tile.X && item.Position.Y == tile.Y) ||
            snapshot.Objects.Any(item => item.Position.X == tile.X && item.Position.Y == tile.Y) ||
            snapshot.Resources.Any(item => item.Position.X == tile.X && item.Position.Y == tile.Y))
        {
            SetStatus("Choose an empty land tile inside the camp", good: false);
            return;
        }
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var provider = SelectedFounderProvider();
        var model = founderModelInput.Text.Trim();
        var choice = SelectedFounderCredential();
        var newKey = choice == "new";
        if (model.Length == 0 || newKey && (string.IsNullOrWhiteSpace(founderKeyLabelInput.Text) ||
            string.IsNullOrWhiteSpace(founderApiKeyInput.Text)))
        {
            SetStatus("Choose a model and enter both a label and key for a new credential", good: false);
            return;
        }
        var founderId = "founder:" + Guid.NewGuid().ToString("N");
        var cognition = new OwnerProviderConfigurationAction(
            "personal", provider, model, newKey ? founderApiKeyInput.Text : null,
            ForgetCredential: false, InhabitantId: founderId,
            CredentialSlotId: newKey ? Guid.NewGuid().ToString("N") : choice == "default" ? null : choice,
            NewCredentialLabel: newKey ? founderKeyLabelInput.Text.Trim() : null);
        try
        {
            await RunOwnerActionAsync(async () =>
            {
                var receipt = await ownerApi.PlaceFounderAsync(ResolveWorldUri(), authority, deviceId,
                    new OwnerFounderPlacementAction(founderId, tile.X, tile.Y, cognition), signer, CancellationToken.None);
                providerConfiguration = await ownerApi.GetProviderStatusAsync(
                    ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
                PopulateFounderCredentials();
                return $"Founder {receipt.Placed}/{receipt.Required} placed in {receipt.HouseholdId}";
            });
        }
        finally
        {
            founderApiKeyInput.Text = string.Empty;
            founderKeyLabelInput.Text = string.Empty;
        }
    }

    private async Task StartFounderWorldAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            _ = await ownerApi.StartWorldAsync(ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            founderSetupPanel.Hide();
            return "World started";
        });
    }

    private void RenderFounderSetup(OwnerWorldSnapshot snapshot)
    {
        var setup = snapshot.FounderSetup;
        founderSetupButton.Visible = setup is { Started: false };
        startWorldButton.Visible = setup is { Started: false };
        addAgentButton.Visible = setup is { Started: true };
        if (setup is not { Started: false })
        {
            if (!placingAddedAgent) founderSetupPanel.Hide();
            return;
        }
        founderSetupButton.Text = $"Add founders {setup.Placed}/{setup.Required}";
        founderSetupHint.Text = setup.Placed < setup.Required
            ? $"Choose this founder’s provider, model, and API key. Then click an empty camp tile. The first two join Camp Alpha; the next two join Camp Beta. {setup.Placed}/{setup.Required} placed."
            : "All four founders are placed. Close this panel and choose Start World to let time run.";
    }
}
