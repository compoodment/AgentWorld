using AgentWorld.Simulation.Harness;
using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Society;
using AgentWorld.Simulation.Kernel;

namespace AgentWorld.Viewer.Observation;

/// <summary>
/// Server-side projection of the Phase 2/3 composite runtime. It converts the
/// protected simulation records into stable viewer DTOs while retaining the
/// distinction between the live fixture topology, cognition state, and paused
/// authoring state.
/// </summary>
public sealed class OwnerWorldObservationStore
{
    private static readonly string[] OwnerServerCapabilities =
    [
        "snapshot.read.v1",
        "event-replay.read.v1",
        "event-history-reset.read.v1",
        "reconnect-baseline.read.v1",
        "seeded-map.read.v1",
        "inhabitant-inspection.read.v1",
        "spatial-knowledge.read.v1",
        "owner-device-pairing.v1",
        "owner-observation.read.v1",
        "owner-control.request.v1",
        "owner-provider-configuration.v1",
        "owner-inhabitant-provider-configuration.v1",
        "paused-authoring.request.v1",
        "content-governance.read.v1",
        "content-governance.write.v1",
    ];

    private static readonly string[] OwnerClientCapabilities =
    [
        "snapshot.read.v1",
        "event-replay.read.v1",
        "reconnect-baseline.read.v1",
        "owner-device-pairing.v1",
    ];

    private readonly OwnerWorldRuntime? ownerRuntime;
    private readonly PrivateWorldRuntime? privateRuntime;

    public OwnerWorldObservationStore(OwnerWorldRuntime runtime)
    {
        ownerRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public OwnerWorldObservationStore(PrivateWorldRuntime runtime)
    {
        privateRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public ViewerHandshake GetOwnerHandshake() => new(
        new ProtocolVersion(Major: 1, Minor: 1),
        OwnerServerCapabilities.ToArray(),
        OwnerClientCapabilities.ToArray());

    public ViewerWorldSnapshot GetSnapshot() => privateRuntime is not null
        ? ToSnapshot(privateRuntime.ExportState())
        : ToSnapshot(ownerRuntime!.Capture(0).Snapshot);

    public ViewerEventSlice GetEventsAfter(long afterEventId)
    {
        if (privateRuntime is not null)
        {
            var state = privateRuntime.ExportState();
            return new ViewerEventSlice(
                state.Society.Society.WorldTick,
                afterEventId,
                state.Events
                    .Where(worldEvent => worldEvent.EventId > afterEventId)
                    .Select(ToEvent)
                    .ToArray(), state.EventHistoryFloor, afterEventId < state.EventHistoryFloor);
        }

        var capture = ownerRuntime!.Capture(afterEventId);
        return new ViewerEventSlice(
            capture.Snapshot.World.Identity.WorldTick,
            capture.AfterEventId,
            capture.Events.Select(ToEvent).ToArray());
    }

    public ViewerReconnectBaseline GetReconnectBaseline(long afterEventId)
    {
        if (privateRuntime is not null)
        {
            var state = privateRuntime.ExportState();
            var privateSnapshot = ToSnapshot(state);
            return new ViewerReconnectBaseline(
                privateSnapshot,
                new ViewerEventSlice(
                    privateSnapshot.WorldTick,
                    afterEventId,
                    state.Events
                        .Where(worldEvent => worldEvent.EventId > afterEventId)
                        .Select(ToEvent)
                        .ToArray(), state.EventHistoryFloor, afterEventId < state.EventHistoryFloor));
        }

        var capture = ownerRuntime!.Capture(afterEventId);
        var snapshot = ToSnapshot(capture.Snapshot);
        return new ViewerReconnectBaseline(
            snapshot,
            new ViewerEventSlice(
                snapshot.WorldTick,
                capture.AfterEventId,
                capture.Events.Select(ToEvent).ToArray()));
    }

    private static ViewerWorldSnapshot ToSnapshot(OwnerWorldSnapshot state)
    {
        var map = state.CurrentMap;
        var resourceStates = state.World.Resources.ToDictionary(resource => resource.Id, StringComparer.Ordinal);
        var actor = ToActor(state.World.Actor);
        return new ViewerWorldSnapshot(
            state.World.Identity.WorldId,
            state.World.Identity.WorldTick,
            state.CurrentMapManifestDigest,
            map.Tiles
                .OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X)
                .Select(tile => new ViewerTile(tile.Position.X, tile.Position.Y, ToWireValue(tile.Terrain)))
                .ToArray(),
            map.CampObjects
                .OrderBy(mapObject => mapObject.Id, StringComparer.Ordinal)
                .Select(mapObject => new ViewerMapObject(mapObject.Id, mapObject.Kind, ToPosition(mapObject.Position)))
                .ToArray(),
            map.Resources
                .OrderBy(resource => resource.Id, StringComparer.Ordinal)
                .Select(resource => new ViewerResource(
                    resource.Id,
                    resource.Kind,
                    ToPosition(resource.Position),
                    resource.IsRenewable,
                    resourceStates.TryGetValue(resource.Id, out var runtimeResource)
                        ? ToWireValue(runtimeResource.State)
                        : "available"))
                .ToArray(),
            actor,
            state.LatestGlobalEventId)
        {
            Inhabitants = CreateInhabitants(state),
            Authoring = new ViewerAuthoringState(
                state.IsPaused,
                state.RunEpoch,
                state.Revision,
                state.TopologyRevision,
                state.InitialMapManifestDigest,
                state.CurrentMapManifestDigest,
                state.Climate.Weather,
                state.Climate.Season,
                state.ApprovedAssetReferences
                    .OrderBy(reference => reference.AssetId, StringComparer.Ordinal)
                    .Select(reference => $"{reference.AssetId}@{reference.AssetDigest}")
                    .ToArray()),
            Instructions = state.Instructions
                .OrderBy(instruction => instruction.SubmissionSequence)
                .Select(instruction => new ViewerInstruction(
                    instruction.InstructionId,
                    instruction.TargetInhabitantId,
                    ToWireValue(instruction.Kind),
                    instruction.Text,
                    ToWireValue(instruction.State),
                    instruction.SubmittedTick,
                    instruction.RunEpoch,
                    instruction.SubmissionSequence))
                .ToArray(),
            Cognition = state.Cognition is null
                ? null
                : new ViewerCognition(
                    state.Cognition.ProviderKind.ToString().ToLowerInvariant(),
                    state.Cognition.IsPaused,
                    state.Cognition.InFlightRequestId,
                    state.Cognition.CurrentIntention?.CandidateId,
                    state.Cognition.CurrentIntention?.Provider.ToString().ToLowerInvariant(),
                    state.Cognition.Events
                        .OrderBy(worldEvent => worldEvent.EventId)
                        .TakeLast(12)
                        .Select(worldEvent => new ViewerCognitionEvent(
                            worldEvent.EventId,
                            worldEvent.WorldTick,
                            worldEvent.Kind,
                            worldEvent.Detail))
                        .ToArray()),
        };
    }

    private static ViewerWorldSnapshot ToSnapshot(PrivateWorldRuntimeState state)
    {
        var map = state.Map;
        var activeInhabitants = state.Society.Society.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var physicalById = state.Inhabitants.ToDictionary(item => item.InhabitantId, StringComparer.Ordinal);
        var resourceStates = state.Resources.ToDictionary(item => item.ResourceId, item => item.State, StringComparer.Ordinal);
        var first = activeInhabitants.FirstOrDefault();
        ViewerActor? actor = null;
        if (first is not null)
        {
            var physical = physicalById[first.Id];
            var inventory = InventoryFor(state, first.Id);
            actor = new ViewerActor(first.Id, ToPosition(physical.Position), physical.HungerBasisPoints,
                physical.EnergyBasisPoints, inventory.Where(item => item.Kind == "food").Sum(item => item.Quantity),
                inventory.Where(item => item.Kind == "wood").Sum(item => item.Quantity));
        }
        var jobs = state.WorldSimulation?.ProductionJobs.Concat(state.WorldSimulation.CropBuilds ?? []).ToArray() ?? [];
        var latestEventId = state.Events.Count == 0 ? 0 : state.Events[^1].EventId;
        return new ViewerWorldSnapshot(
            state.Society.Society.WorldId,
            state.Society.Society.WorldTick,
            map.ManifestDigest,
            map.Tiles
                .OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X)
                .Select(tile => new ViewerTile(tile.Position.X, tile.Position.Y, ToWireValue(tile.Terrain)))
                .ToArray(),
            map.CampObjects
                .OrderBy(mapObject => mapObject.Id, StringComparer.Ordinal)
                .Select(mapObject => new ViewerMapObject(mapObject.Id, mapObject.Kind, ToPosition(mapObject.Position)))
                .ToArray(),
            map.Resources
                .OrderBy(resource => resource.Id, StringComparer.Ordinal)
                .Select(resource => new ViewerResource(
                    resource.Id,
                    resource.Kind,
                    ToPosition(resource.Position),
                    resource.IsRenewable,
                    resourceStates.TryGetValue(resource.Id, out var resourceState)
                        ? ToWireValue(resourceState)
                        : "available"))
                .ToArray(),
            actor,
            latestEventId)
        {
            Inhabitants = activeInhabitants
                .Select(inhabitant => ToPlaytestInhabitant(state, inhabitant, physicalById[inhabitant.Id]))
                .ToArray(),
            Stockpiles = state.Society.Society.Households.Select(household =>
                new ViewerStockpile(household.Id, household.Name, InventoryFor(state, household.Id))).ToArray(),
            Council = state.Council is { } council ? new ViewerCouncil(
                state.Society.Society.Inhabitants.FirstOrDefault(person => person.Id == council.StewardId)?.Name,
                council.FoodPolicy, council.Ballot?.Policy, council.Ballot?.Approvals.Count ?? 0,
                council.Ballot?.Rejections.Count ?? 0, council.Ballot?.Electorate.Count ?? 0) : null,
            Authoring = new ViewerAuthoringState(
                state.Society.Society.IsPaused,
                state.Society.Society.RunEpoch,
                state.EventHistoryFloor + state.Events.Count,
                0,
                map.ManifestDigest,
                map.ManifestDigest,
                state.WorldSystems?.Climate.Weather.ToString().ToLowerInvariant() ?? "clear",
                state.WorldSystems?.Climate.Season.ToString().ToLowerInvariant() ?? "spring",
                []),
            Instructions = (state.Instructions ?? [])
                .Where(instruction => !(state.CompletedInstructionIds ?? []).Contains(instruction.InstructionId, StringComparer.Ordinal))
                .OrderBy(instruction => instruction.SubmissionSequence)
                .Select(instruction => new ViewerInstruction(
                    instruction.InstructionId,
                    instruction.TargetInhabitantId,
                    ToWireValue(instruction.Kind),
                    instruction.Text,
                    ToWireValue(instruction.State),
                    instruction.SubmittedTick,
                    instruction.RunEpoch,
                    instruction.SubmissionSequence))
                .ToArray(),
            Cognition = ToCognition(state),
            ContentPackages = state.Content?.Packages
                .OrderBy(package => package.Manifest.PackageId, StringComparer.Ordinal)
                .Select(package => new ViewerContentPackage(
                    package.Manifest.PackageId,
                    package.Manifest.Version.ToString(),
                    package.Manifest.PackageDigest,
                    package.Lifecycle.ToString().ToLowerInvariant(),
                    package.LockDigest,
                    package.ValidationTick,
                    package.StagedTick,
                    package.ActivationTick,
                    package.ManifestDigest))
                .ToArray() ?? [],
            ContentEvents = state.Content?.Events
                .OrderBy(item => item.EventId)
                .Select(item => new ViewerContentGovernanceEvent(
                    item.EventId,
                    item.WorldTick,
                    item.PackageId,
                    item.Kind,
                    item.Detail))
                .ToArray() ?? [],
            WorldSystems = state.WorldSystems is { } systems
                ? new ViewerWorldSystemsSummary(
                    systems.Climate.Season.ToString().ToLowerInvariant(),
                    systems.Climate.Weather.ToString().ToLowerInvariant(),
                    systems.Ecology.Resources.Count,
                    systems.Factions.Factions.Count,
                    systems.Currency.Accounts.Count,
                    systems.Culture.Cultures.Count,
                    systems.Chunks.Count,
                    state.WorldContent?.Buildings.Count ?? 0,
                    state.WorldContent?.Recipes.Count ?? 0,
                    state.WorldSimulation?.Buildings.Count ?? 0,
                    jobs.Length,
                    state.AssetReservations?.Reservations
                        .Select(item => item.NormalizedDigest)
                        .Distinct(StringComparer.Ordinal)
                        .Count() ?? 0,
                    state.AssetReservations is { } assetState
                        ? assetState.Reservations
                            .GroupBy(item => item.NormalizedDigest, StringComparer.Ordinal)
                            .Sum(group => group.First().DurableStorageBytes)
                        : 0,
                    state.AssetReservations is { } cacheState
                        ? cacheState.Reservations
                            .GroupBy(item => $"{item.NormalizedDigest}|{item.DecodeProfile}", StringComparer.Ordinal)
                            .Sum(group => group.First().DecodedCacheBytes)
                        : 0,
                    state.AssetReservations is { } gpuState
                        ? gpuState.Reservations
                            .GroupBy(item => $"{item.NormalizedDigest}|{item.DecodeProfile}", StringComparer.Ordinal)
                            .Sum(group => group.First().GpuBytes)
                        : 0,
                    state.AssetReservations?.Reservations.Sum(item => item.RenderUnits) ?? 0)
                : null,
            PlacedBuildings = state.WorldSimulation?.Buildings
                .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                .Select(item => new ViewerPlacedBuilding(
                    item.InstanceId,
                    item.DefinitionId,
                    ToPosition(item.Position),
                    item.PlacedTick))
                .ToArray() ?? [],
            ProductionJobs = jobs
                .OrderBy(item => item.JobId, StringComparer.Ordinal)
                .Select(item => new ViewerProductionJob(
                    item.JobId,
                    item.RecipeId,
                    item.BuildingInstanceId,
                    item.WorkerId,
                    item.StartedTick,
                    item.CompletionTick,
                    item.State.ToString().ToLowerInvariant()))
                .ToArray(),
        };
    }

    private static List<ViewerInhabitant> CreateInhabitants(OwnerWorldSnapshot state)
    {
        var inhabitants = new List<ViewerInhabitant>
        {
            ToProtectedActor(state),
        };
        inhabitants.AddRange(state.FounderDrafts
            .OrderBy(draft => draft.Id, StringComparer.Ordinal)
            .Select(ToFounderDraft));
        return inhabitants;
    }

    private static ViewerInhabitant ToProtectedActor(OwnerWorldSnapshot state)
    {
        var world = state.World;
        var actor = world.Actor;
        var route = DetermineFixtureRoute(world);
        var perceived = KnownNearby(world.Map, actor.Position).ToArray();
        var known = KnownFixtureTopology(actor.Position, perceived, route);
        var cognition = state.Cognition;
        var decisionFactors = new List<ViewerDecisionFactor>
        {
            new(
                "decision-source",
                cognition is null
                    ? "deterministic fixture"
                    : $"{cognition.ProviderKind.ToString().ToLowerInvariant()} provider"),
            new("hunger", $"{actor.HungerBasisPoints} basis points"),
            new("energy", $"{actor.EnergyBasisPoints} basis points"),
            new("fixture-topology", world.Map.ManifestDigest),
        };
        if (cognition?.CurrentIntention is { } intention)
        {
            decisionFactors.Add(new ViewerDecisionFactor("current-intention", intention.CandidateId));
            decisionFactors.Add(new ViewerDecisionFactor("intention-provider", intention.Provider.ToString().ToLowerInvariant()));
        }

        return new ViewerInhabitant(
            actor.Id,
            "Scout",
            "active_fixture",
            ToPosition(actor.Position),
            actor.HungerBasisPoints,
            actor.EnergyBasisPoints,
            [
                new ViewerInventoryEntry("food", actor.FoodItems),
                new ViewerInventoryEntry("wood", actor.WoodItems),
            ],
            decisionFactors,
            route,
            new ViewerSpatialKnowledge(ToPosition(actor.Position), perceived, known),
            IsDraft: false)
        {
            PublicIntention = cognition?.CurrentIntention is { } publicIntention
                ? ToPublicIntention(publicIntention.CandidateId, publicIntention.Provider.ToString().ToLowerInvariant(), publicIntention.WorldTick)
                : null,
        };
    }

    private static ViewerInhabitant ToFounderDraft(OwnerFounderDraft draft) => new(
        draft.Id,
        draft.DisplayName,
        "authoring_draft",
        ToPosition(draft.Position),
        0,
        0,
        [],
        [
            new ViewerDecisionFactor("status", "paused authoring draft; not active in the protected fixture"),
            new ViewerDecisionFactor("created-revision", draft.CreatedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ],
        new ViewerRoute("not_active", null, null, [], string.Empty),
        new ViewerSpatialKnowledge(ToPosition(draft.Position), [ToPosition(draft.Position)], [ToPosition(draft.Position)]),
        IsDraft: true);

    private static ViewerInhabitant ToPlaytestInhabitant(
        PrivateWorldRuntimeState state,
        SocietyInhabitant inhabitant,
        PlaytestInhabitantState physical)
    {
        var inventory = InventoryFor(state, inhabitant.Id);
        var route = DeterminePlaytestRoute(state, physical, inventory);
        var perceived = KnownNearby(state.Map, physical.Position).ToArray();
        var known = KnownFixtureTopology(physical.Position, perceived, route);
        var household = state.Society.Society.Households
            .FirstOrDefault(item => item.Id == inhabitant.HouseholdId);
        var decisionFactors = new List<ViewerDecisionFactor>
        {
            new("personality", physical.Personality),
            new("aspiration", physical.Aspiration),
            new("role", inhabitant.CurrentRole.ToString().ToLowerInvariant()),
            new("household", household?.Name ?? "unhoused"),
            new("hunger", $"{physical.HungerBasisPoints} basis points"),
            new("energy", $"{physical.EnergyBasisPoints} basis points"),
        };
        var runtime = state.Society.Cognition.Runtimes
            .FirstOrDefault(item => item.InhabitantId == inhabitant.Id);
        if (runtime?.CurrentIntention is { } intention)
        {
            decisionFactors.Add(new ViewerDecisionFactor("current-intention", intention.CandidateId));
            decisionFactors.Add(new ViewerDecisionFactor("intention-provider", intention.Provider.ToString().ToLowerInvariant()));
        }

        return new ViewerInhabitant(
            inhabitant.Id,
            inhabitant.Name,
            inhabitant.Status.ToString().ToLowerInvariant(),
            ToPosition(physical.Position),
            physical.HungerBasisPoints,
            physical.EnergyBasisPoints,
            inventory,
            decisionFactors,
            route,
            new ViewerSpatialKnowledge(ToPosition(physical.Position), perceived, known),
            IsDraft: false)
        {
            PublicIntention = runtime?.CurrentIntention is { } publicIntention
                ? ToPublicIntention(publicIntention.CandidateId, publicIntention.Provider.ToString().ToLowerInvariant(), publicIntention.WorldTick)
                : null,
            Relationships = RelationshipsFor(state, inhabitant.Id),
            Project = physical.Project is { } project
                ? new ViewerProject(project.Label, project.Stage, project.WorkDone, 10, project.Blocker, project.StartedTick)
                : null,
            Survival = physical.Survival is { } survival
                ? new ViewerSurvival(survival.WarmthBasisPoints, survival.IllnessBasisPoints,
                    inventory.Any(item => item.Kind == "clothing" && item.Quantity > 0),
                    inventory.Any(item => item.Kind == "tool" && item.Quantity > 0), survival.NutritionBasisPoints, survival.LastMealKind) : null,
            SocialNotes = state.Society.Society.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                    (offer.FirstPartyId == inhabitant.Id || offer.SecondPartyId == inhabitant.Id))
                .Select(offer => offer.AcceptedBy.Contains(inhabitant.Id, StringComparer.Ordinal)
                    ? "Waiting for the other inhabitant to accept or decline an exchange."
                    : "An exchange is offered; acceptance or refusal is still undecided.")
                .Concat(state.Society.Society.Memories.Where(memory => memory.OwnerId == inhabitant.Id && memory.Visibility == "public")
                    .OrderByDescending(memory => memory.SourceTick).Take(3).Select(memory => memory.Summary)).ToArray(),
        };
    }

    private static ViewerInhabitantRelationship[] RelationshipsFor(
        PrivateWorldRuntimeState state,
        string inhabitantId) => state.Society.Society.Relationships
        .Where(relationship =>
            (relationship.ProposerId == inhabitantId || relationship.TargetId == inhabitantId) &&
            relationship.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted)
        .OrderBy(relationship => relationship.Type)
        .ThenBy(relationship => relationship.Id, StringComparer.Ordinal)
        .Select(relationship => new ViewerInhabitantRelationship(
            relationship.Id,
            relationship.ProposerId == inhabitantId ? relationship.TargetId : relationship.ProposerId,
            ToWireValue(relationship.Type),
            ToWireValue(relationship.State),
            relationship.PrivacyClass,
            relationship.EffectiveTick))
        .ToArray();

    private static ViewerPublicIntention ToPublicIntention(
        string candidateId,
        string provider,
        long worldTick) => new(
        candidateId,
        PublicIntentionSummary(candidateId),
        provider,
        worldTick);

    private static string PublicIntentionSummary(string candidateId) => candidateId switch
    {
        "seek_food" => "looking for food",
        "harvest_food" => "gathering food",
        "consume_food" => "eating carried food",
        "sleep" => "looking for rest",
        "safe_idle" => "keeping a safe routine",
        _ => candidateId.Replace('_', ' '),
    };

    private static string ToWireValue(SocietyRelationshipType type) => type switch
    {
        SocietyRelationshipType.Partnership => "partnership",
        SocietyRelationshipType.Caregiver => "caregiver",
        SocietyRelationshipType.HouseholdMembership => "household_membership",
        SocietyRelationshipType.BiologicalParentage => "biological_parentage",
        SocietyRelationshipType.LegalGuardian => "legal_guardian",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static string ToWireValue(SocietyRelationshipState state) => state switch
    {
        SocietyRelationshipState.Proposed => "proposed",
        SocietyRelationshipState.Accepted => "accepted",
        SocietyRelationshipState.Rejected => "rejected",
        SocietyRelationshipState.Revoked => "revoked",
        SocietyRelationshipState.Dissolved => "dissolved",
        SocietyRelationshipState.EndedByDeath => "ended_by_death",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static ViewerInventoryEntry[] InventoryFor(
        PrivateWorldRuntimeState state,
        string ownerId) => state.Society.Society.Inventory.Lots
        .Where(lot => lot.OwnerId == ownerId && lot.Quantity > 0)
        .GroupBy(lot => lot.ItemKind, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new ViewerInventoryEntry(group.Key, group.Sum(lot => lot.Quantity)))
        .ToArray();

    private static ViewerRoute DeterminePlaytestRoute(
        PrivateWorldRuntimeState state,
        PlaytestInhabitantState physical,
        IReadOnlyList<ViewerInventoryEntry> inventory)
    {
        var food = inventory.FirstOrDefault(item => item.Kind == "food");
        if (food is { Quantity: > 0 } && physical.HungerBasisPoints < 8_500)
        {
            return new ViewerRoute("consume", null, null, [], state.Map.ManifestDigest);
        }

        var bedroll = state.Map.GetObject("bedroll");
        if (physical.EnergyBasisPoints < 1_500)
        {
            return IsWithinInteractionRange(physical.Position, bedroll.Position)
                ? new ViewerRoute("sleep", bedroll.Id, ToPosition(bedroll.Position), [], state.Map.ManifestDigest)
                : RouteTo(state.Map, physical.Position, bedroll.Position, "sleep", bedroll.Id);
        }

        var berry = state.Map.GetResource("berry-patch");
        var berryState = state.Resources.FirstOrDefault(item => item.ResourceId == berry.Id)?.State;
        if (berryState == ResourceState.Available && IsWithinInteractionRange(physical.Position, berry.Position))
        {
            return new ViewerRoute("harvest", berry.Id, ToPosition(berry.Position), [], state.Map.ManifestDigest);
        }

        if (berryState == ResourceState.Available && physical.HungerBasisPoints < 7_000)
        {
            return RouteTo(state.Map, physical.Position, berry.Position, "seek_food", berry.Id);
        }

        if (physical.EnergyBasisPoints < 3_500)
        {
            return IsWithinInteractionRange(physical.Position, bedroll.Position)
                ? new ViewerRoute("sleep", bedroll.Id, ToPosition(bedroll.Position), [], state.Map.ManifestDigest)
                : RouteTo(state.Map, physical.Position, bedroll.Position, "sleep", bedroll.Id);
        }

        return new ViewerRoute("idle", null, null, [], state.Map.ManifestDigest);
    }

    private static bool IsWithinInteractionRange(GridPoint origin, GridPoint destination) =>
        Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y) <= 1;

    private static ViewerRoute DetermineFixtureRoute(HarnessWorld world)
    {
        var actor = world.Actor;
        if (actor.FoodItems > 0)
        {
            return new ViewerRoute("consume", null, null, [], world.Map.ManifestDigest);
        }

        var berry = world.Map.GetResource("berry-patch");
        if (world.GetResource(berry.Id).State == ResourceState.Available)
        {
            return RouteTo(world.Map, actor.Position, berry.Position, "harvest", berry.Id);
        }

        var bedroll = world.Map.GetObject("bedroll");
        if (actor.Position != bedroll.Position)
        {
            return RouteTo(world.Map, actor.Position, bedroll.Position, "sleep", bedroll.Id);
        }

        return new ViewerRoute("fixture_complete", null, null, [], world.Map.ManifestDigest);
    }

    private static ViewerRoute RouteTo(
        SeededMap map,
        GridPoint origin,
        GridPoint destination,
        string status,
        string destinationId)
    {
        var steps = origin == destination
            ? []
            : DeterministicRouteFinder.Find(map, origin, destination)
                .Skip(1)
                .Select(ToPosition)
                .ToArray();
        return new ViewerRoute(status, destinationId, ToPosition(destination), steps, map.ManifestDigest);
    }

    private static IEnumerable<ViewerPosition> KnownNearby(SeededMap map, GridPoint origin) => map.Tiles
        .Where(tile => Math.Abs(tile.Position.X - origin.X) <= 1 && Math.Abs(tile.Position.Y - origin.Y) <= 1)
        .OrderBy(tile => tile.Position.Y)
        .ThenBy(tile => tile.Position.X)
        .Select(tile => ToPosition(tile.Position));

    /// <summary>
    /// The deterministic fixture has no persistent cognitive map. Its truthful
    /// knowledge is therefore limited to the current local perception and the
    /// route/destination it has already committed to follow. The server still
    /// owns the full map for routing, but must not accidentally project that
    /// omniscience as inhabitant knowledge.
    /// </summary>
    private static ViewerPosition[] KnownFixtureTopology(
        GridPoint currentPosition,
        IReadOnlyList<ViewerPosition> perceived,
        ViewerRoute route)
    {
        var routeKnowledge = route.Destination is null
            ? route.Steps
            : route.Steps.Append(route.Destination);

        return perceived
            .Append(ToPosition(currentPosition))
            .Concat(routeKnowledge)
            .Distinct()
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
    }

    private static ViewerActor ToActor(HarnessActor actor) => new(
        actor.Id,
        ToPosition(actor.Position),
        actor.HungerBasisPoints,
        actor.EnergyBasisPoints,
        actor.FoodItems,
        actor.WoodItems);

    private static ViewerEvent ToEvent(OwnerWorldEvent worldEvent) => new(
        worldEvent.EventId,
        worldEvent.WorldTick,
        worldEvent.Kind,
        worldEvent.Detail);

    private static ViewerEvent ToEvent(PlaytestWorldEvent worldEvent) => new(
        worldEvent.EventId,
        worldEvent.WorldTick,
        worldEvent.Kind,
        worldEvent.Detail);

    private static ViewerCognition ToCognition(PrivateWorldRuntimeState state)
    {
        var runtimes = state.Society.Cognition.Runtimes
            .OrderBy(runtime => runtime.InhabitantId, StringComparer.Ordinal)
            .ToArray();
        var current = runtimes
            .Select(runtime => runtime.CurrentIntention)
            .FirstOrDefault(intention => intention is not null);
        var provider = current?.Provider.ToString().ToLowerInvariant() ??
            (state.Society.Society.WorldDefaultProviderBindingId is null ? "deterministic" : "configured");
        return new ViewerCognition(
            provider,
            state.Society.Society.IsPaused,
            null,
            current?.CandidateId,
            current?.Provider.ToString().ToLowerInvariant(),
            state.Society.Cognition.Events
                .OrderBy(worldEvent => worldEvent.EventId)
                .TakeLast(12)
                .Select(worldEvent => new ViewerCognitionEvent(
                    worldEvent.EventId,
                    worldEvent.WorldTick,
                    worldEvent.Kind,
                    worldEvent.Detail))
                .ToArray(),
            runtimes.Where(runtime => runtime.CurrentIntention is not null)
                .Select(runtime =>
                {
                    var intention = runtime.CurrentIntention!;
                    return new ViewerInhabitantDecision(
                        runtime.InhabitantId, intention.Usage?.ProviderId ?? intention.Provider.ToString().ToLowerInvariant(),
                        intention.CandidateId, intention.WorldTick, intention.Confidence,
                        intention.Usage?.ModelId, intention.Usage?.InputTokens, intention.Usage?.OutputTokens,
                        intention.Usage?.Role, intention.Usage?.LatencyMilliseconds,
                        runtime.Events.LastOrDefault(item => item.WorldTick == intention.WorldTick &&
                            item.Kind is "cognition_fallback_applied" or "cognition_decision_applied")?.Kind == "cognition_fallback_applied");
                }).ToArray());
    }

    private static ViewerPosition ToPosition(GridPoint point) => new(point.X, point.Y);

    private static string ToWireValue(TerrainKind terrain) => terrain switch
    {
        TerrainKind.Meadow => "meadow",
        TerrainKind.Water => "water",
        TerrainKind.Mountain => "mountain",
        _ => throw new ArgumentOutOfRangeException(nameof(terrain)),
    };

    private static string ToWireValue(ResourceState state) => state switch
    {
        ResourceState.Available => "available",
        ResourceState.Depleted => "depleted",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string ToWireValue(OwnerInstructionKind kind) => kind switch
    {
        OwnerInstructionKind.Suggestive => "suggestive",
        OwnerInstructionKind.MustDo => "must_do",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ToWireValue(OwnerInstructionState state) => state switch
    {
        OwnerInstructionState.Queued => "queued",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
