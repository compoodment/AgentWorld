using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Control creationOverlay = new();
    private readonly PanelContainer creationPanel = new();
    private readonly LineEdit designName = new() { Text = "Custom shelter", MaxLength = 96 };
    private readonly OptionButton designPurpose = new();
    private readonly SpinBox designWood = new() { MinValue = 1, MaxValue = 48, Step = 1, Value = 8 };
    private readonly RichTextLabel designSummary = new();
    private readonly Label designStatus = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly OptionButton designPackages = new();
    private readonly Button previewDesignButton = new() { Text = "Preview" };
    private readonly Button submitDesignButton = new() { Text = "Submit design" };
    private readonly Button reviewDesignButton = new() { Text = "Review selected" };
    private readonly Button advanceDesignButton = new() { Text = "Validate" };
    private readonly Button withdrawDesignButton = new() { Text = "Withdraw unused" };
    private readonly ConfirmationDialog withdrawDesignConfirmation = new();
    private OwnerBuildingDesignPreview? reviewedDesign;
    private string? withdrawalTarget;
    private string? pendingDesignSelection;

    private void BuildCreationWorkbench()
    {
        creationOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        creationOverlay.ZIndex = 120;
        creationOverlay.MouseFilter = MouseFilterEnum.Stop;
        AddChild(creationOverlay);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        creationOverlay.AddChild(shade);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        creationOverlay.AddChild(center);
        center.AddChild(creationPanel);
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        body.AddThemeConstantOverride("separation", 8);
        var heading = new HBoxContainer();
        heading.AddChild(new Label { Text = "Building workbench", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "×", TooltipText = "Return to the menu" };
        close.Pressed += creationOverlay.Hide;
        StyleButton(close);
        heading.AddChild(close);
        body.AddChild(heading);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 450), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var fields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fields.AddThemeConstantOverride("separation", 8);
        fields.AddChild(designStatus);
        scroll.AddChild(fields);
        body.AddChild(scroll);
        fields.AddChild(new Label { Text = "1 · Design and test", Modulate = new Color("AFC4BA") });
        fields.AddChild(designName);
        var options = new HBoxContainer();
        designPurpose.AddItem("Shelter");
        designPurpose.AddItem("Storehouse");
        designPurpose.AddItem("Fuelled hearth");
        designPurpose.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        options.AddChild(designPurpose);
        options.AddChild(new Label { Text = "Wood cost" });
        options.AddChild(designWood);
        fields.AddChild(options);
        var designActions = new HBoxContainer();
        foreach (var button in new[] { previewDesignButton, submitDesignButton }) { StyleButton(button); designActions.AddChild(button); }
        fields.AddChild(designActions);
        ConfigureTextPanel(designSummary, 140);
        designSummary.Text = "Preview tests construction in a disposable world. Nothing is installed or approved by previewing.";
        fields.AddChild(designSummary);
        fields.AddChild(new Label { Text = "2 · Review and activate", Modulate = new Color("AFC4BA") });
        fields.AddChild(designPackages);
        var governance = new HBoxContainer();
        foreach (var button in new[] { reviewDesignButton, advanceDesignButton, withdrawDesignButton }) { StyleButton(button); governance.AddChild(button); }
        fields.AddChild(governance);
        fields.AddChild(new Label
        {
            Text = "Staged designs activate on the next running tick. Built or used designs cannot be withdrawn without a migration.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("A7B9B7"),
        });
        AddPanelContents(creationPanel, body);
        creationOverlay.Hide();
        designName.TextChanged += _ => InvalidateDesignPreview();
        designPurpose.ItemSelected += _ => InvalidateDesignPreview();
        designWood.ValueChanged += _ => InvalidateDesignPreview();
        designPackages.ItemSelected += _ =>
        {
            designSummary.Text = "Review the selected saved design before validating, approving or staging it.";
            RefreshCreationAvailability();
        };
        previewDesignButton.Pressed += () => _ = PreviewDesignAsync(reviewExisting: false);
        reviewDesignButton.Pressed += () => _ = PreviewDesignAsync(reviewExisting: true);
        submitDesignButton.Pressed += () => _ = SubmitDesignAsync();
        advanceDesignButton.Pressed += () => _ = AdvanceDesignAsync();
        withdrawDesignButton.Pressed += () =>
        {
            withdrawalTarget = SelectedDesign()?.PackageId;
            withdrawDesignConfirmation.DialogText = "Withdraw this unused design? Committed buildings, jobs and projects block withdrawal. No materials or built work will be deleted.";
            withdrawDesignConfirmation.PopupCentered(new Vector2I(480, 180));
        };
        withdrawDesignConfirmation.Title = "Withdraw design";
        withdrawDesignConfirmation.Confirmed += () => _ = WithdrawDesignAsync();
        AddChild(withdrawDesignConfirmation);
    }

    private OwnerBuildingDesignAction CurrentDesign() => new(designName.Text.Trim(), designPurpose.Selected switch
    { 1 => "storage", 2 => "hearth", _ => "shelter" }, checked((int)designWood.Value));

    private OwnerWorldContentPackage? SelectedDesign()
    {
        if (designPackages.Selected < 0 || designPackages.ItemCount == 0) return null;
        var id = designPackages.GetItemMetadata(designPackages.Selected).AsString();
        return observationSession.Current?.Baseline.Snapshot.ContentPackages.FirstOrDefault(package => package.PackageId == id);
    }

    private void InvalidateDesignPreview()
    {
        reviewedDesign = null;
        designSummary.Text = "Design changed. Preview again before submitting or approving.";
        RefreshCreationAvailability();
    }

    private void RenderDesignPackages(OwnerWorldSnapshot snapshot)
    {
        var selected = pendingDesignSelection ?? (designPackages.Selected >= 0 && designPackages.ItemCount > 0
            ? designPackages.GetItemMetadata(designPackages.Selected).AsString() : reviewedDesign?.Package.PackageId);
        designPackages.Clear();
        foreach (var package in snapshot.ContentPackages.Where(package => package.PackageId.StartsWith("owner-building-", StringComparison.Ordinal)))
        {
            var index = designPackages.ItemCount;
            var proposer = package.ProposedByInhabitantId is null ? null : snapshot.Inhabitants
                .FirstOrDefault(inhabitant => inhabitant.Id == package.ProposedByInhabitantId)?.DisplayName ?? package.ProposedByInhabitantId;
            var provenance = proposer is null ? string.Empty : " · proposed by " + proposer;
            designPackages.AddItem($"{package.DisplayName ?? "Building design"}{provenance} · {GameUiText.HumanizeIdentifier(package.Lifecycle)}");
            designPackages.SetItemMetadata(index, package.PackageId);
            if (package.PackageId == selected)
            {
                designPackages.Select(index);
                pendingDesignSelection = null;
            }
        }
    }

    private void RefreshCreationAvailability()
    {
        var supported = observationSession.Current?.Handshake.ServerCapabilities.Contains("owner-building-design.v1", StringComparer.Ordinal) == true;
        var unavailable = !supported || registration is null || isOwnerAction || isPairingOperation;
        designName.Editable = !unavailable;
        designPurpose.Disabled = unavailable;
        designWood.Editable = !unavailable;
        previewDesignButton.Disabled = unavailable || string.IsNullOrWhiteSpace(designName.Text);
        var selected = SelectedDesign();
        designPackages.Disabled = unavailable || designPackages.ItemCount == 0;
        reviewDesignButton.Disabled = unavailable || selected is null;
        var reviewed = reviewedDesign is { ConstructionPassed: true } && reviewedDesign.Design == CurrentDesign();
        submitDesignButton.Disabled = unavailable || !reviewed || observationSession.Current!.Baseline.Snapshot.ContentPackages
            .Any(package => package.PackageId == reviewedDesign!.Package.PackageId);
        var exactReview = reviewed && selected?.PackageId == reviewedDesign!.Package.PackageId && selected.ManifestDigest == reviewedDesign.ManifestDigest;
        advanceDesignButton.Text = selected?.Lifecycle switch { "validated" => "Approve", "approved" => "Stage", "staged" => "Waiting for resume", "active" => "Active", "quarantined" => "Withdrawn", _ => "Validate" };
        advanceDesignButton.Disabled = unavailable || !exactReview || selected?.Lifecycle is not ("proposed" or "validated" or "approved");
        withdrawDesignButton.Disabled = unavailable || selected?.Lifecycle is not ("active" or "staged");
    }

    private async Task PreviewDesignAsync(bool reviewExisting)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var selected = SelectedDesign();
        if (reviewExisting && selected is null) return;
        var action = CurrentDesign();
        await RunOwnerActionAsync(async () =>
        {
            var result = reviewExisting
                ? await ownerApi.ReviewBuildingAsync(ResolveWorldUri(), authority, deviceId, new(selected!.PackageId), signer, CancellationToken.None)
                : await ownerApi.PreviewBuildingAsync(ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            designName.Text = result.Design.Name;
            designPurpose.Select(result.Design.Purpose switch { "storage" => 1, "hearth" => 2, _ => 0 });
            designWood.Value = result.Design.WoodCost;
            reviewedDesign = result;
            designSummary.Text = $"{result.Design.Name} · 1 × 1 tile · {result.Design.WoodCost} wood\n{result.Summary}\n" +
                $"Construction: {(result.ConstructionPassed ? "passed" : "failed")} · consumed {result.WoodConsumed} wood\nManifest: {result.ManifestDigest}";
            return "design preview complete; live world unchanged";
        });
    }

    private async Task SubmitDesignAsync()
    {
        if (reviewedDesign is not { ConstructionPassed: true } preview || preview.Design != CurrentDesign() ||
            !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            _ = await ownerApi.ProposeContentAsync(ResolveWorldUri(), authority, deviceId, preview.Package, signer, CancellationToken.None);
            pendingDesignSelection = preview.Package.PackageId;
            return "design submitted; review, validate and approve before staging";
        });
    }

    private async Task AdvanceDesignAsync()
    {
        var package = SelectedDesign();
        if (package is null || reviewedDesign is not { ConstructionPassed: true } preview ||
            preview.Package.PackageId != package.PackageId || preview.ManifestDigest != package.ManifestDigest ||
            preview.Design != CurrentDesign() || !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            var action = new OwnerContentPackageIdAction(package.PackageId);
            _ = package.Lifecycle switch
            {
                "proposed" => await ownerApi.ValidateContentAsync(ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None),
                "validated" => await ownerApi.ApproveContentAsync(ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None),
                "approved" => await ownerApi.StageContentAsync(ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None),
                _ => throw new InvalidOperationException("This design has no pending approval step."),
            };
            return package.Lifecycle == "approved" ? "design staged; activation waits for the next running tick" : "design review step saved";
        });
    }

    private async Task WithdrawDesignAsync()
    {
        var target = withdrawalTarget;
        withdrawalTarget = null;
        if (target is null || !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            _ = await ownerApi.RollbackContentAsync(ResolveWorldUri(), authority, deviceId,
                new(target, "owner_workbench_withdrawal"), signer, CancellationToken.None);
            return "unused design withdrawn; committed work preserved";
        });
    }
}
