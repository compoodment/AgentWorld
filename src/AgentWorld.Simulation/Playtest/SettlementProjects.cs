using AgentWorld.Simulation.Content;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Kernel;
using AgentWorld.Simulation.Society;
using AgentWorld.Simulation.World;

namespace AgentWorld.Simulation.Playtest;

/// <summary>A durable public work plan, not hidden model reasoning.</summary>
public sealed record SettlementProject(
    string CandidateId,
    string Label,
    long StartedTick,
    string Stage,
    int WorkDone = 0,
    string? Blocker = null,
    string? JobId = null,
    long LastTransitionTick = 0);

public sealed partial class PrivateWorldRuntime
{
    private const int ProjectWorkTicks = 10;

    private static bool IsCompatibleSavedMap(SeededMap generated, PrivateWorldRuntimeState state)
    {
        if (MapManifestCodec.Digest(state.Map) != state.Map.ManifestDigest)
        {
            return false;
        }
        if (generated.ManifestDigest == state.Map.ManifestDigest)
        {
            return true;
        }
        var baseIds = generated.Resources.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        var added = state.Map.Resources.Where(resource => !baseIds.Contains(resource.Id)).ToArray();
        if (state.SchemaVersion < 5 || added.Length is < 1 or > 3 || added.Select(resource => resource.Position).Distinct().Count() != added.Length ||
            state.Content?.Packages.Any(package => package.Manifest.PackageId == SettlementContent.PackageId &&
                package.Manifest.PackageDigest == SettlementContent.Create().PackageDigest && package.ActivationTick is not null) != true ||
            added.Any(resource => resource.Id != "settlement-" + resource.Kind ||
                resource.Kind is not ("stone" or "fiber" or "seed") || resource.IsRenewable != (resource.Kind is "fiber" or "seed") ||
                !generated.IsPassable(resource.Position) ||
                generated.CampObjects.Any(item => item.Position == resource.Position) ||
                generated.Resources.Any(item => item.Position == resource.Position)))
        {
            return false;
        }
        var original = state.Map with { Resources = state.Map.Resources.Where(resource => baseIds.Contains(resource.Id)).ToArray() };
        return MapManifestCodec.Digest(original) == generated.ManifestDigest;
    }

    private void StageSettlementContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == SettlementContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == StarterContent.PackageId && package.Lifecycle == ContentPackageLifecycle.Active))
        {
            return;
        }
        var manifest = SettlementContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest), [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent("settlement_content_staged", manifest.PackageId);
    }

    private void AddSettlementResources()
    {
        var occupied = map.CampObjects.Select(item => item.Position).Concat(map.Resources.Select(item => item.Position))
            .Concat(worldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                worldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building.Position)))
            .ToHashSet();
        var additions = new List<MapResource>();
        foreach (var kind in new[] { "stone", "fiber", "seed" })
        {
            var id = "settlement-" + kind;
            if (map.Resources.Any(resource => resource.Id == id))
            {
                continue;
            }
            var tile = map.Tiles.FirstOrDefault(tile => map.IsPassable(tile.Position) && !occupied.Contains(tile.Position));
            if (tile is null)
            {
                AppendEvent("settlement_resource_blocked", kind);
                continue;
            }
            additions.Add(new MapResource(id, kind, tile.Position, kind is "fiber" or "seed"));
            occupied.Add(tile.Position);
        }
        if (additions.Count == 0)
        {
            return;
        }
        map = map with { Resources = map.Resources.Concat(additions).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray() };
        map = map with { ManifestDigest = MapManifestCodec.Digest(map) };
        worldSystems = worldSystems with
        {
            Ecology = worldSystems.Ecology with
            {
                Resources = worldSystems.Ecology.Resources.Concat(additions.Select(resource => new EcologyResource(
                    resource.Id, resource.Kind, resource.Position, resource.IsRenewable, 16, 16,
                    resource.IsRenewable ? 4 : 0, resource.IsRenewable ? 1 : 0, SeasonKind.Spring,
                    WorldCalendarRules.FromTick(WorldTick, worldSystems.Config).DayIndex + 1, EcologyResourceState.Available)))
                    .OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
            },
            Chunks = worldSystems.Chunks.Select(chunk => ChunkManifestCodec.WithDigest(chunk with
            {
                Resources = map.Resources.Select(resource => new ChunkResourceMetadata(resource.Id, resource.Kind, resource.Position, resource.IsRenewable)).ToArray(),
            })).ToArray(),
        };
        SyncEcologyResourceStates();
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent("settlement_resources_added", string.Join(',', additions.Select(resource => resource.Kind)));
    }

    private static void ValidateProject(SettlementProject project, long worldTick)
    {
        if (string.IsNullOrWhiteSpace(project.CandidateId) || project.CandidateId.Length > 512 ||
            !(project.CandidateId.StartsWith("build:building:", StringComparison.Ordinal) && project.CandidateId.Length > 15 ||
              project.CandidateId.StartsWith("build:recipe:", StringComparison.Ordinal) && project.CandidateId.Length > 13) ||
            string.IsNullOrWhiteSpace(project.Label) || project.Label.Length > 256 ||
            project.StartedTick < 0 || project.StartedTick > worldTick ||
            project.LastTransitionTick < project.StartedTick || project.LastTransitionTick > worldTick ||
            project.WorkDone is < 0 or > ProjectWorkTicks ||
            project.Stage is not ("acquiring" or "gathering" or "delivering" or "travelling" or "working" or "waiting" or "blocked" or "paused" or "completed" or "cancelled"))
        {
            throw new InvalidDataException("The saved settlement project is invalid.");
        }
    }

    private bool CanContinueProject(PlaytestInhabitantState state) =>
        state.Project is { Stage: not ("completed" or "cancelled") } project &&
        (project.Stage != "blocked" || WorldTick - project.LastTransitionTick < 60) &&
        state.HungerBasisPoints >= 3_500 && state.EnergyBasisPoints >= 2_500 &&
        PendingInstructionFor(state.InhabitantId) is null;

    private void BeginProject(string inhabitantId, PlaytestInhabitantState state, string candidateId)
    {
        if (state.Project is not { Stage: not ("completed" or "cancelled") } existing || existing.CandidateId != candidateId)
        {
            var definitionId = candidateId[(candidateId.StartsWith("build:building:", StringComparison.Ordinal) ? 15 : 13)..];
            var label = worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId)?.DisplayName
                ?? worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == definitionId)?.DisplayName;
            if (label is null)
            {
                return;
            }
            state = state with { Project = new SettlementProject(candidateId, label, WorldTick, "acquiring", LastTransitionTick: WorldTick) };
            inhabitants[inhabitantId] = state;
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent("project_chosen", $"{inhabitantId}:{candidateId}");
        }
        else if (state.Project.Stage == "blocked")
        {
            state = state with { Project = state.Project with { LastTransitionTick = WorldTick } };
            inhabitants[inhabitantId] = state;
        }
        ContinueProject(inhabitantId, state);
    }

    private void ContinueProject(string inhabitantId, PlaytestInhabitantState state)
    {
        var project = state.Project!;
        if (project.JobId is not null)
        {
            var job = worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
                .FirstOrDefault(item => item.JobId == project.JobId);
            SetProject(inhabitantId, project with
            {
                Stage = job?.State switch { WorldProductionJobState.Running => "waiting", WorldProductionJobState.Completed => "completed", _ => "cancelled" },
                Blocker = job is null || job.State == WorldProductionJobState.Cancelled ? "Production was removed or cancelled" : null,
            });
            return;
        }

        var isBuilding = project.CandidateId.StartsWith("build:building:", StringComparison.Ordinal);
        var definitionId = project.CandidateId[(isBuilding ? 15 : 13)..];
        var building = isBuilding ? worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId) : null;
        var recipe = isBuilding ? null : worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == definitionId);
        if (building is null && recipe is null)
        {
            SetProject(inhabitantId, project with { Stage = "cancelled", Blocker = "Content is no longer active" });
            return;
        }
        var inputs = building?.BuildCosts ?? recipe!.Inputs;
        var missing = inputs.FirstOrDefault(input => !HasAvailableQuantities([input]));
        if (missing.Amount > 0)
        {
            AcquireProjectInput(inhabitantId, state, missing);
            return;
        }

        GridPoint position;
        var hasSite = building is not null
            ? TryFindBuildingPosition(building, out position)
            : TryFindRecipeSite(recipe!, out _, out position);
        if (!hasSite)
        {
            SetProject(inhabitantId, project with { Stage = "blocked", Blocker = "Waiting for a free work site" });
            return;
        }
        if (state.Position != position)
        {
            SetProject(inhabitantId, project with { Stage = "travelling", Blocker = null });
            MoveToward(inhabitantId, inhabitants[inhabitantId], position, "project");
            return;
        }
        if (project.WorkDone < ProjectWorkTicks)
        {
            SetProject(inhabitantId, project with { Stage = "working", WorkDone = project.WorkDone + 1, Blocker = null });
            return;
        }
        ApplyBuildDecision(inhabitantId, state, project.CandidateId);
        if (building is not null && worldSimulation.Buildings.Any(item => item.InstanceId == BuildInstanceId(inhabitantId, building)))
        {
            SetProject(inhabitantId, project with { Stage = "completed", Blocker = null });
        }
        else if (recipe is not null)
        {
            var job = worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
                .FirstOrDefault(item => item.WorkerId == inhabitantId && item.StartedTick == WorldTick && item.RecipeId == definitionId);
            if (job is not null)
            {
                SetProject(inhabitantId, project with { Stage = "waiting", JobId = job.JobId, Blocker = null });
            }
        }
    }

    private void AcquireProjectInput(string inhabitantId, PlaytestInhabitantState state, ContentQuantity input)
    {
        var project = state.Project!;
        var carried = society.Checkpoint.Inventory.Lots.FirstOrDefault(lot => lot.OwnerId == inhabitantId &&
            lot.ItemKind == input.ResourceId && AvailableLotQuantity(lot) > 0);
        if (carried is not null)
        {
            var camp = map.GetObject("storage").Position;
            SetProject(inhabitantId, project with { Stage = "delivering", Blocker = $"Taking {input.ResourceId} to household storage" });
            if (!IsWithinInteractionRange(state.Position, camp, ResourceInteractionRange))
            {
                MoveToward(inhabitantId, inhabitants[inhabitantId], camp, "deliver", ResourceInteractionRange);
                return;
            }
            ApplyInventoryTransition(inventory => InventoryFixture.Transfer(inventory,
                $"project-delivery:{WorldTick}:{inhabitantId}", inhabitantId, HouseholdId, carried.Id,
                Math.Min(input.Amount, AvailableLotQuantity(carried)), "project_contribution"));
            AppendEvent("project_material_delivered", $"{inhabitantId}:{input.ResourceId}");
            return;
        }

        var source = map.Resources.FirstOrDefault(resource =>
            (resource.Kind == input.ResourceId || (input.ResourceId == "wood" && resource.Kind == "construction")) &&
            resources.GetValueOrDefault(resource.Id) == ResourceState.Available);
        if (source is null)
        {
            SetProject(inhabitantId, project with { Stage = "blocked", Blocker = $"No available source of {input.ResourceId}" });
            return;
        }
        SetProject(inhabitantId, project with { Stage = "gathering", Blocker = $"Need {input.Amount} {input.ResourceId}" });
        GatherProjectMaterial(inhabitantId, state, input.ResourceId, source);
    }

    private void GatherProjectMaterial(string inhabitantId, PlaytestInhabitantState state, string itemKind, MapResource source)
    {
        if (!IsWithinInteractionRange(state.Position, source.Position, ResourceInteractionRange))
        {
            MoveToward(inhabitantId, inhabitants[inhabitantId], source.Position, "materials", ResourceInteractionRange);
            return;
        }
        var ecology = worldSystems.Ecology.GetResource(source.Id);
        var harvest = EcologyRules.Harvest(ecology, 1);
        if (!harvest.IsValid || harvest.Resource is null)
        {
            return;
        }
        worldSystems = worldSystems with
        {
            Ecology = worldSystems.Ecology with
            {
                Resources = worldSystems.Ecology.Resources.Select(resource => resource.Id == source.Id ? harvest.Resource : resource).ToArray(),
            },
        };
        SyncEcologyResourceStates();
        ApplyInventoryTransition(inventory => InventoryFixture.AddLot(inventory, $"material:{WorldTick}:{inhabitantId}",
            itemKind, inhabitantId, 4, WorldTick));
        AppendEvent("material_gathered", $"{inhabitantId}:{itemKind}:4");
    }

    private IEnumerable<(string Requester, ContentQuantity Input)> ProjectRequests(string helperId)
    {
        foreach (var person in inhabitants.Values.OrderBy(person => person.InhabitantId, StringComparer.Ordinal))
        {
            if (person.InhabitantId == helperId || person.Project is not { Stage: not ("completed" or "cancelled" or "waiting") } project)
            {
                continue;
            }
            var isBuilding = project.CandidateId.StartsWith("build:building:", StringComparison.Ordinal);
            var definitionId = project.CandidateId[(isBuilding ? 15 : 13)..];
            var inputs = isBuilding
                ? worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId)?.BuildCosts
                : worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == definitionId)?.Inputs;
            foreach (var input in inputs ?? [])
            {
                if (!HasAvailableQuantities([input]))
                {
                    yield return (person.InhabitantId, input);
                }
            }
        }
    }

    private MapResource? MaterialSource(string itemKind) => map.Resources.FirstOrDefault(resource =>
        (resource.Kind == itemKind || (itemKind == "wood" && resource.Kind == "construction")) &&
        resources.GetValueOrDefault(resource.Id) == ResourceState.Available);

    private bool CanAcquireProjectInputs(IReadOnlyList<ContentQuantity> inputs) => inputs.All(input =>
    {
        var stored = society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == input.ResourceId &&
                (lot.OwnerId == HouseholdId || inhabitants.ContainsKey(lot.OwnerId)))
            .Sum(lot => (long)AvailableLotQuantity(lot));
        var harvestable = worldSystems.Ecology.Resources.Where(resource =>
                resource.State == EcologyResourceState.Available &&
                (resource.Kind == input.ResourceId || (input.ResourceId == "wood" && resource.Kind == "construction")))
            .Sum(resource => (long)resource.Quantity * 4);
        return stored + harvestable >= input.Amount;
    });

    private void AddProjectAssistanceCandidates(List<CognitionCandidate> candidates, string helperId)
    {
        foreach (var request in ProjectRequests(helperId).DistinctBy(request => request.Input.ResourceId))
        {
            var itemKind = request.Input.ResourceId;
            if (MaterialSource(itemKind) is not null || society.Checkpoint.Inventory.Lots.Any(lot =>
                    lot.OwnerId == helperId && lot.ItemKind == itemKind && AvailableLotQuantity(lot) > 0))
            {
                candidates.Add(new CognitionCandidate("assist:" + itemKind,
                    $"Help {society.Checkpoint.GetInhabitant(request.Requester).Name}: gather and share {itemKind} for their project.", 15));
            }
        }
    }

    private void AssistProject(string helperId, PlaytestInhabitantState state, string itemKind)
    {
        var request = ProjectRequests(helperId).FirstOrDefault(request => request.Input.ResourceId == itemKind);
        if (request.Requester is null)
        {
            return;
        }
        var carried = society.Checkpoint.Inventory.Lots.FirstOrDefault(lot => lot.OwnerId == helperId &&
            lot.ItemKind == itemKind && AvailableLotQuantity(lot) > 0);
        if (carried is null)
        {
            if (MaterialSource(itemKind) is { } source)
            {
                GatherProjectMaterial(helperId, state, itemKind, source);
            }
            return;
        }
        var camp = map.GetObject("storage").Position;
        if (!IsWithinInteractionRange(state.Position, camp, ResourceInteractionRange))
        {
            MoveToward(helperId, state, camp, "share_materials", ResourceInteractionRange);
            return;
        }
        var quantity = Math.Min(request.Input.Amount, AvailableLotQuantity(carried));
        ApplyInventoryTransition(inventory => InventoryFixture.Transfer(inventory, $"project-share:{WorldTick}:{helperId}",
            helperId, HouseholdId, carried.Id, quantity, "project_request_fulfilled"));
        var memoryId = $"project-gratitude:{request.Requester}:{helperId}";
        if (!society.Checkpoint.Memories.Any(memory => memory.Id == memoryId))
        {
            society.Apply(checkpoint => SocietyFixture.RecordSocialMemory(checkpoint, new SocietySocialMemory(
                memoryId, request.Requester, helperId,
                $"Grateful for {society.Checkpoint.GetInhabitant(helperId).Name}'s help with project materials.", "public", WorldTick)));
        }
        AppendEvent("project_request_fulfilled", $"{helperId}:{request.Requester}:{itemKind}:{quantity}");
    }

    private int AvailableLotQuantity(InventoryLot lot) => lot.Quantity - society.Checkpoint.Inventory.Reservations
        .Where(reservation => reservation.LotId == lot.Id && reservation.State is InventoryReservationState.Reserved or
            InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed).Sum(reservation => reservation.Quantity);

    private void SetProject(string inhabitantId, SettlementProject project)
    {
        var state = inhabitants[inhabitantId];
        if (state.Project?.Stage != project.Stage || state.Project?.Blocker != project.Blocker)
        {
            project = project with { LastTransitionTick = WorldTick };
            AppendEvent("project_progress", $"{inhabitantId}:{project.Stage}:{project.Blocker ?? project.Label}");
        }
        inhabitants[inhabitantId] = state with { Project = project };
    }
}
