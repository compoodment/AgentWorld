using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgentWorld.Simulation.Kernel;

namespace AgentWorld.Simulation.Society;

/// <summary>
/// Authoritative Phase 4 society transitions. Every public operation validates
/// against a checkpoint and returns a replacement checkpoint with durable
/// events; callers never receive a partially-mutated world.
/// </summary>
public static class SocietyFixture
{
    public static SocietyInhabitant CreateFounder(
        string id,
        string name,
        string? providerBindingId = null,
        int healthBasisPoints = 10_000,
        SocietyConfig? config = null)
    {
        var effectiveConfig = config ?? new SocietyConfig();
        effectiveConfig.Validate();
        return new(
            NormalizeRequiredText(id, nameof(id)),
            NormalizeRequiredText(name, nameof(name)),
            checked(-effectiveConfig.AdultYears * effectiveConfig.TicksPerWorldYear),
            SocietyInhabitantStatus.Active,
            effectiveConfig.AgeBandAt(effectiveConfig.AdultYears),
            ValidateBasisPoints(healthBasisPoints, nameof(healthBasisPoints)),
            null,
            NormalizeOptionalText(providerBindingId),
            SocietyWorkRole.Unassigned,
            effectiveConfig.AdultYears);
    }

    public static SocietyCheckpoint CreateGenesis(
        string worldId,
        IEnumerable<SocietyInhabitant>? founders = null,
        IEnumerable<InventoryLot>? lots = null,
        SocietyConfig? config = null,
        string? worldDefaultProviderBindingId = null)
    {
        var effectiveConfig = config ?? new SocietyConfig();
        effectiveConfig.Validate();
        var inhabitants = (founders ??
                [CreateFounder("founder-scout", "Scout", config: effectiveConfig)])
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        ValidateInhabitants(inhabitants, effectiveConfig, 0);
        var inventory = InventoryFixture.CreateGenesis(lots ?? []);
        return new SocietyCheckpoint(
            NormalizeRequiredText(worldId, nameof(worldId)),
            0,
            0,
            false,
            effectiveConfig,
            NormalizeOptionalText(worldDefaultProviderBindingId),
            inhabitants,
            [],
            [],
            [],
            [],
            [],
            [],
            inventory,
            []);
    }

    public static SocietyOperationResult CreateHousehold(
        SocietyCheckpoint checkpoint,
        string householdId,
        string name,
        IEnumerable<string> memberIds)
    {
        Validate(checkpoint);
        var id = NormalizeRequiredText(householdId, nameof(householdId));
        var householdName = NormalizeRequiredText(name, nameof(name));
        var members = CanonicalIds(memberIds, nameof(memberIds));
        if (checkpoint.Households.Any(item => item.Id == id))
        {
            throw new InvalidOperationException("A household ID may be used only once.");
        }

        foreach (var memberId in members)
        {
            EnsureActive(checkpoint, memberId);
            if (checkpoint.GetInhabitant(memberId).HouseholdId is not null)
            {
                throw new InvalidOperationException("An inhabitant cannot join a second primary household.");
            }
        }

        var household = new SocietyHousehold(id, householdName, members, []);
        var inhabitants = checkpoint.Inhabitants.Select(item => members.Contains(item.Id, StringComparer.Ordinal)
                ? item with { HouseholdId = id }
                : item)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var relationships = checkpoint.Relationships.ToList();
        foreach (var memberId in members)
        {
            relationships.Add(new SocietyRelationship(
                $"{id}:membership:{memberId}",
                1,
                SocietyRelationshipType.HouseholdMembership,
                id,
                memberId,
                SocietyRelationshipState.Accepted,
                SocietyConsentState.ProtectedLifecycle,
                checkpoint.WorldTick,
                checkpoint.WorldTick,
                "household",
                id,
                new[] { id, memberId }.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        var next = checkpoint with
        {
            Inhabitants = inhabitants,
            Households = checkpoint.Households.Append(household)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Relationships = relationships.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(next, "household_created", $"{id}:{string.Join(',', members)}", id);
    }

    public static SocietyOperationResult ProposeRelationship(
        SocietyCheckpoint checkpoint,
        SocietyRelationshipProposal proposal)
    {
        Validate(checkpoint);
        ArgumentNullException.ThrowIfNull(proposal);
        ValidateProposal(proposal, checkpoint.WorldTick);
        EnsureActive(checkpoint, proposal.ProposerId);
        EnsureActive(checkpoint, proposal.TargetId);
        if (checkpoint.Relationships.Any(item => item.Id == proposal.Id))
        {
            throw new InvalidOperationException("A relationship proposal ID may be used only once.");
        }

        var relationship = new SocietyRelationship(
            proposal.Id,
            proposal.Revision,
            proposal.Type,
            proposal.ProposerId,
            proposal.TargetId,
            SocietyRelationshipState.Proposed,
            SocietyConsentState.Pending,
            proposal.RequestedTick,
            0,
            proposal.PrivacyClass,
            proposal.HouseholdId,
            []);
        var next = checkpoint with
        {
            Relationships = checkpoint.Relationships.Append(relationship)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(next, "relationship_proposed", $"{proposal.Id}:r{proposal.Revision}", proposal.Id);
    }

    public static SocietyOperationResult AcceptRelationship(
        SocietyCheckpoint checkpoint,
        string relationshipId,
        int revision,
        string acceptorId)
    {
        Validate(checkpoint);
        var relationship = checkpoint.GetRelationship(relationshipId);
        var acceptor = NormalizeRequiredText(acceptorId, nameof(acceptorId));
        if (relationship.State != SocietyRelationshipState.Proposed || relationship.Revision != revision ||
            relationship.TargetId != acceptor)
        {
            return Reject(checkpoint, "relationship_rejected", $"{relationshipId}:stale_or_wrong_acceptor");
        }

        EnsureActive(checkpoint, relationship.ProposerId);
        EnsureActive(checkpoint, relationship.TargetId);
        if (relationship.Type == SocietyRelationshipType.Partnership &&
            checkpoint.Relationships.Any(item => item.State == SocietyRelationshipState.Accepted &&
                item.Type == SocietyRelationshipType.Partnership &&
                (item.ProposerId == relationship.ProposerId || item.TargetId == relationship.ProposerId ||
                 item.ProposerId == relationship.TargetId || item.TargetId == relationship.TargetId)))
        {
            return Reject(checkpoint, "relationship_rejected", $"{relationshipId}:cardinality_conflict");
        }

        if (relationship.Type == SocietyRelationshipType.HouseholdMembership)
        {
            if (relationship.HouseholdId is null || !checkpoint.Households.Any(item => item.Id == relationship.HouseholdId))
            {
                return Reject(checkpoint, "relationship_rejected", $"{relationshipId}:missing_household");
            }

            if (checkpoint.GetInhabitant(relationship.TargetId).HouseholdId is not null)
            {
                return Reject(checkpoint, "relationship_rejected", $"{relationshipId}:existing_household");
            }
        }

        var accepted = relationship with
        {
            State = SocietyRelationshipState.Accepted,
            Consent = SocietyConsentState.Accepted,
            EffectiveTick = checked(checkpoint.WorldTick + 1),
            AcceptedBy = new[] { relationship.ProposerId, relationship.TargetId }
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        };
        var next = checkpoint with
        {
            Relationships = checkpoint.Relationships.Select(item => item.Id == relationship.Id ? accepted : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        next = ApplyRelationshipProjection(next, accepted);
        return Commit(next, "relationship_accepted", $"{relationshipId}:r{revision}", relationshipId);
    }

    public static SocietyOperationResult RefuseRelationship(
        SocietyCheckpoint checkpoint,
        string relationshipId,
        int revision,
        string targetId)
    {
        Validate(checkpoint);
        var relationship = checkpoint.GetRelationship(relationshipId);
        if (relationship.State != SocietyRelationshipState.Proposed || relationship.Revision != revision ||
            relationship.TargetId != targetId)
        {
            return Reject(checkpoint, "relationship_rejected", $"{relationshipId}:stale_or_wrong_target");
        }

        var rejected = relationship with
        {
            State = SocietyRelationshipState.Rejected,
            Consent = SocietyConsentState.Refused,
            EffectiveTick = checkpoint.WorldTick,
        };
        return Commit(
            checkpoint with
            {
                Relationships = checkpoint.Relationships.Select(item => item.Id == relationship.Id ? rejected : item)
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            },
            "relationship_rejected",
            $"{relationshipId}:consent_refused",
            relationshipId);
    }

    public static SocietyOperationResult RevokeRelationship(
        SocietyCheckpoint checkpoint,
        string relationshipId,
        string actorId)
    {
        Validate(checkpoint);
        var relationship = checkpoint.GetRelationship(relationshipId);
        var actor = NormalizeRequiredText(actorId, nameof(actorId));
        if (relationship.State is not (SocietyRelationshipState.Accepted or SocietyRelationshipState.Proposed) ||
            (relationship.ProposerId != actor && relationship.TargetId != actor))
        {
            return Reject(checkpoint, "relationship_rejected", $"{relationshipId}:unauthorized_revoke");
        }

        var revoked = relationship with
        {
            State = SocietyRelationshipState.Revoked,
            Consent = SocietyConsentState.Revoked,
            EffectiveTick = checkpoint.WorldTick,
        };
        var next = checkpoint with
        {
            Relationships = checkpoint.Relationships.Select(item => item.Id == relationship.Id ? revoked : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        next = RemoveRelationshipProjection(next, revoked);
        return Commit(next, "relationship_revoked", $"{relationshipId}:{actor}", relationshipId);
    }

    public static SocietyOperationResult AssignRole(
        SocietyCheckpoint checkpoint,
        string inhabitantId,
        SocietyWorkRole role)
    {
        Validate(checkpoint);
        EnsureActive(checkpoint, inhabitantId);
        var next = checkpoint with
        {
            Inhabitants = checkpoint.Inhabitants.Select(item => item.Id == inhabitantId
                    ? item with { CurrentRole = role }
                    : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(next, "work_role_assigned", $"{inhabitantId}:{role}", inhabitantId);
    }

    public static SocietyOperationResult RecordSocialMemory(
        SocietyCheckpoint checkpoint,
        SocietySocialMemory memory)
    {
        Validate(checkpoint);
        ArgumentNullException.ThrowIfNull(memory);
        EnsureActive(checkpoint, memory.OwnerId);
        if (!checkpoint.Inhabitants.Any(item => item.Id == memory.SubjectId) ||
            checkpoint.Memories.Any(item => item.Id == memory.Id))
        {
            throw new InvalidOperationException("A social memory requires a known subject and unique ID.");
        }

        var next = checkpoint with
        {
            Memories = checkpoint.Memories.Append(memory with
            { Summary = NormalizeRequiredText(memory.Summary, nameof(memory.Summary)) })
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(next, "social_memory_recorded", memory.Id, memory.Id);
    }

    public static SocietyOperationResult CreateOrganization(
        SocietyCheckpoint checkpoint,
        string organizationId,
        SocietyOrganizationKind kind,
        string ownerId,
        IEnumerable<string> memberIds)
    {
        Validate(checkpoint);
        var id = NormalizeRequiredText(organizationId, nameof(organizationId));
        EnsureActive(checkpoint, ownerId);
        var members = CanonicalIds(memberIds, nameof(memberIds));
        foreach (var member in members)
        {
            EnsureActive(checkpoint, member);
        }

        if (checkpoint.Organizations.Any(item => item.Id == id))
        {
            throw new InvalidOperationException("An organization ID may be used only once.");
        }

        var organization = new SocietyOrganization(id, kind, ownerId, members, $"organization:{id}");
        var next = checkpoint with
        {
            Organizations = checkpoint.Organizations.Append(organization)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(next, "organization_created", id, id);
    }

    public static SocietyOperationResult AddOrganizationMember(
        SocietyCheckpoint checkpoint,
        string organizationId,
        string ownerId,
        string memberId)
    {
        Validate(checkpoint);
        var organization = checkpoint.Organizations.Single(item => item.Id == organizationId);
        if (organization.OwnerId != ownerId)
        {
            return Reject(checkpoint, "organization_member_rejected", $"{organizationId}:unauthorized");
        }

        EnsureActive(checkpoint, memberId);
        var members = organization.MemberIds.Append(memberId).Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var next = checkpoint with
        {
            Organizations = checkpoint.Organizations.Select(item => item.Id == organizationId
                    ? item with { MemberIds = members }
                    : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(next, "organization_member_added", $"{organizationId}:{memberId}", memberId);
    }

    public static SocietyOperationResult TransferInventory(
        SocietyCheckpoint checkpoint,
        string transferId,
        string senderId,
        string recipientId,
        string lotId,
        int quantity,
        string purpose = "direct_transfer")
    {
        Validate(checkpoint);
        EnsureLivingParty(checkpoint, senderId);
        EnsureLivingParty(checkpoint, recipientId);
        var inventory = InventoryFixture.Transfer(
            checkpoint.Inventory,
            transferId,
            senderId,
            recipientId,
            lotId,
            quantity,
            purpose);
        return Commit(checkpoint with { Inventory = inventory }, "inventory_transfer_committed", transferId, transferId);
    }

    public static SocietyOperationResult ConsumeInventory(
        SocietyCheckpoint checkpoint,
        string ownerId,
        string lotId,
        int quantity,
        string purpose = "direct_consumption")
    {
        Validate(checkpoint);
        var owner = NormalizeRequiredText(ownerId, nameof(ownerId));
        EnsureLivingParty(checkpoint, owner);
        var lot = checkpoint.Inventory.GetLot(lotId);
        if (quantity <= 0 || lot.OwnerId != owner || lot.Quantity < quantity)
        {
            throw new InvalidOperationException("Consumption requires an owned lot with sufficient quantity.");
        }

        var reservationId = $"consume:{owner}:{lotId}:{checkpoint.Inventory.Events.Count + 1}";
        var reserved = InventoryFixture.Reserve(
            checkpoint.Inventory,
            reservationId,
            owner,
            lotId,
            quantity,
            purpose,
            checkpoint.WorldTick);
        var consumed = InventoryFixture.ConsumeReservation(reserved, reservationId);
        return Commit(
            checkpoint with { Inventory = consumed },
            "inventory_consumed",
            $"{owner}:{lotId}:{quantity}:{purpose}",
            owner);
    }

    public static SocietyOperationResult CreateBarterOffer(
        SocietyCheckpoint checkpoint,
        DirectBarterProposal proposal)
    {
        Validate(checkpoint);
        EnsureLivingParty(checkpoint, proposal.FirstPartyId);
        EnsureLivingParty(checkpoint, proposal.SecondPartyId);
        var inventory = InventoryFixture.CreateDirectBarterOffer(checkpoint.Inventory, proposal);
        return Commit(checkpoint with { Inventory = inventory }, "barter_offer_created", proposal.Id, proposal.Id);
    }

    public static SocietyOperationResult AcceptBarterOffer(
        SocietyCheckpoint checkpoint,
        string offerId,
        int revision,
        string partyId)
    {
        Validate(checkpoint);
        EnsureLivingParty(checkpoint, partyId);
        var inventory = InventoryFixture.AcceptDirectBarterOffer(checkpoint.Inventory, offerId, revision, partyId);
        var offer = inventory.GetOffer(offerId);
        return Commit(
            checkpoint with { Inventory = inventory },
            offer.State == DirectBarterState.Settled ? "barter_settled" : "barter_accepted",
            $"{offerId}:{partyId}:r{revision}",
            offerId);
    }

    public static SocietyOperationResult CommitBirth(
        SocietyCheckpoint checkpoint,
        SocietyBirthRequest request)
    {
        Validate(checkpoint);
        ArgumentNullException.ThrowIfNull(request);
        ValidateBirthRequest(request, checkpoint.WorldTick);
        var existing = checkpoint.Births.SingleOrDefault(item => item.RequestId == request.Id);
        if (existing is not null)
        {
            return existing.Revision == request.Revision
                ? new SocietyOperationResult(checkpoint, existing.ChildId, [])
                : Reject(checkpoint, "birth_rejected", $"{request.Id}:revision_conflict");
        }

        var firstParent = checkpoint.GetInhabitant(request.FirstParentId);
        var secondParent = checkpoint.GetInhabitant(request.SecondParentId);
        var household = checkpoint.GetHousehold(request.HouseholdId);
        if (!IsAdult(firstParent) || !IsAdult(secondParent) ||
            !request.ConsentingParentIds.OrderBy(item => item, StringComparer.Ordinal)
                .SequenceEqual(new[] { firstParent.Id, secondParent.Id }.OrderBy(item => item, StringComparer.Ordinal)) ||
            !HasActivePartnership(checkpoint, firstParent.Id, secondParent.Id) ||
            request.CaregiverIds.Count == 0 ||
            request.CaregiverIds.Any(id => !household.MemberIds.Contains(id, StringComparer.Ordinal) ||
                !IsAdult(checkpoint.GetInhabitant(id))))
        {
            return Reject(checkpoint, "birth_rejected", $"{request.Id}:readiness_or_consent");
        }

        var sourceLot = checkpoint.Inventory.GetLot(request.FoodLotId);
        if (sourceLot.OwnerId != request.HouseholdId &&
            sourceLot.OwnerId != firstParent.Id && sourceLot.OwnerId != secondParent.Id)
        {
            return Reject(checkpoint, "birth_rejected", $"{request.Id}:food_access");
        }

        InventoryCheckpoint inventory;
        try
        {
            var reservationId = $"birth:{request.Id}:food";
            inventory = InventoryFixture.Reserve(
                checkpoint.Inventory,
                reservationId,
                sourceLot.OwnerId,
                request.FoodLotId,
                request.FoodQuantity,
                $"birth:{request.Id}",
                checkpoint.WorldTick);
            inventory = InventoryFixture.ConsumeReservation(inventory, reservationId);
        }
        catch (InvalidOperationException)
        {
            return Reject(checkpoint, "birth_rejected", $"{request.Id}:reservation_failed");
        }

        var childId = $"{checkpoint.WorldId}:inhabitant:{request.Id}";
        var child = new SocietyInhabitant(
            childId,
            $"Child {childId}",
            checkpoint.WorldTick,
            SocietyInhabitantStatus.Active,
            SocietyAgeBand.Infant,
            10_000,
            household.Id,
            ResolveNewbornProvider(checkpoint, firstParent, secondParent, request),
            SocietyWorkRole.Unassigned,
            0);
        var relationships = checkpoint.Relationships.ToList();
        relationships.AddRange(
        [
            BirthRelationship(request, childId, SocietyRelationshipType.BiologicalParentage, firstParent.Id),
            BirthRelationship(request, childId, SocietyRelationshipType.BiologicalParentage, secondParent.Id),
            ..request.CaregiverIds.Select(caregiver => BirthRelationship(
                request,
                childId,
                SocietyRelationshipType.Caregiver,
                caregiver)),
            BirthRelationship(request, childId, SocietyRelationshipType.HouseholdMembership, household.Id),
        ]);
        var updatedHousehold = household with
        {
            MemberIds = household.MemberIds.Append(childId).Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            CaregiverIds = household.CaregiverIds.Union(request.CaregiverIds, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
        };
        var next = checkpoint with
        {
            Inhabitants = checkpoint.Inhabitants.Append(child).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Households = checkpoint.Households.Select(item => item.Id == household.Id ? updatedHousehold : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Relationships = relationships.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Births = checkpoint.Births.Append(new SocietyBirthRecord(request.Id, childId, request.Revision, checkpoint.WorldTick))
                .OrderBy(item => item.RequestId, StringComparer.Ordinal).ToArray(),
            Inventory = inventory,
        };
        return Commit(next, "birth_committed", $"{request.Id}:{childId}", childId);
    }

    public static SocietyOperationResult AdvanceTo(
        SocietyCheckpoint checkpoint,
        long targetTick)
    {
        Validate(checkpoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetTick, checkpoint.WorldTick);
        if (checkpoint.IsPaused)
        {
            throw new InvalidOperationException("A paused society must resume before it can advance.");
        }

        var current = checkpoint;
        var boundaryEvents = new List<(long Tick, string InhabitantId, int AgeYears)>();
        foreach (var inhabitant in checkpoint.Inhabitants.Where(item => item.Status == SocietyInhabitantStatus.Active))
        {
            var oldAge = checkpoint.Config.AgeAt(inhabitant.BirthTick, checkpoint.WorldTick);
            var newAge = checkpoint.Config.AgeAt(inhabitant.BirthTick, targetTick);
            for (var age = Math.Max(oldAge + 1, inhabitant.LastLifecycleYearChecked + 1); age <= newAge; age++)
            {
                boundaryEvents.Add((
                    checked(inhabitant.BirthTick + age * checkpoint.Config.TicksPerWorldYear),
                    inhabitant.Id,
                    checked((int)age)));
            }
        }

        foreach (var boundaryGroup in boundaryEvents
                     .GroupBy(item => item.Tick)
                     .OrderBy(group => group.Key))
        {
            current = current with
            {
                WorldTick = boundaryGroup.Key,
                Inventory = WithInventoryTick(current.Inventory, boundaryGroup.Key),
            };

            var transitionDetails = new List<string>();
            var mortalityIds = new List<string>();
            foreach (var boundary in boundaryGroup.OrderBy(item => item.InhabitantId, StringComparer.Ordinal))
            {
                var inhabitant = current.GetInhabitant(boundary.InhabitantId);
                if (inhabitant.Status != SocietyInhabitantStatus.Active)
                {
                    continue;
                }

                var nextBand = current.Config.AgeBandAt(boundary.AgeYears);
                current = current with
                {
                    Inhabitants = current.Inhabitants.Select(item => item.Id == inhabitant.Id
                            ? item with
                            {
                                AgeBand = nextBand,
                                LastLifecycleYearChecked = boundary.AgeYears,
                            }
                            : item)
                        .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                };
                if (nextBand != inhabitant.AgeBand)
                {
                    transitionDetails.Add($"{inhabitant.Id}:{inhabitant.AgeBand}:{nextBand}");
                }

                var risk = current.Config.NaturalMortalityRiskBasisPoints(boundary.AgeYears);
                if (risk > 0 && MortalityRoll(current.WorldId, inhabitant.Id, boundary.AgeYears) < risk)
                {
                    mortalityIds.Add(inhabitant.Id);
                }
            }

            current = CommitMany(
                current,
                transitionDetails.Select(detail => ("age_band_transition", detail)));
            foreach (var inhabitantId in mortalityIds)
            {
                if (current.GetInhabitant(inhabitantId).Status == SocietyInhabitantStatus.Active)
                {
                    current = Kill(current, inhabitantId, SocietyDeathCause.NaturalAge, current.WorldTick).Checkpoint;
                }
            }
        }

        current = current with
        {
            WorldTick = targetTick,
            Inventory = WithInventoryTick(current.Inventory, targetTick),
        };
        current = SettleDueEstates(current, targetTick);
        return new SocietyOperationResult(
            current,
            null,
            current.Events.Skip(checkpoint.Events.Count).ToArray());
    }

    public static SocietyOperationResult Kill(
        SocietyCheckpoint checkpoint,
        string inhabitantId,
        SocietyDeathCause cause,
        long? deathTick = null)
    {
        Validate(checkpoint);
        var inhabitant = checkpoint.GetInhabitant(inhabitantId);
        if (inhabitant.Status == SocietyInhabitantStatus.Dead)
        {
            return new SocietyOperationResult(checkpoint, null, []);
        }

        var tick = deathTick ?? checkpoint.WorldTick;
        ArgumentOutOfRangeException.ThrowIfLessThan(tick, checkpoint.WorldTick);
        return Kill(checkpoint with { WorldTick = tick }, inhabitantId, cause, tick);
    }

    public static SocietyOperationResult Pause(SocietyCheckpoint checkpoint)
    {
        Validate(checkpoint);
        return checkpoint.IsPaused
            ? new SocietyOperationResult(checkpoint, null, [])
            : Commit(checkpoint with { IsPaused = true }, "paused", "requested");
    }

    public static SocietyOperationResult Resume(SocietyCheckpoint checkpoint)
    {
        Validate(checkpoint);
        return !checkpoint.IsPaused
            ? new SocietyOperationResult(checkpoint, null, [])
            : Commit(
                checkpoint with
                {
                    IsPaused = false,
                    RunEpoch = checked(checkpoint.RunEpoch + 1),
                },
                "resumed",
                $"epoch:{checkpoint.RunEpoch + 1}");
    }

    public static void Validate(SocietyCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint.WorldId);
        if (checkpoint.WorldTick < 0 || checkpoint.RunEpoch < 0)
        {
            throw new InvalidDataException("Society clock and run epoch must be non-negative.");
        }

        checkpoint.Config.Validate();
        if (checkpoint.Inventory.WorldTick != checkpoint.WorldTick)
        {
            throw new InvalidDataException("Society and inventory clocks must agree.");
        }

        ValidateInhabitants(checkpoint.Inhabitants, checkpoint.Config, checkpoint.WorldTick);
        EnsureCanonicalIds(checkpoint.Households.Select(item => item.Id), "households");
        EnsureCanonicalIds(checkpoint.Relationships.Select(item => item.Id), "relationships");
        EnsureCanonicalIds(checkpoint.Organizations.Select(item => item.Id), "organizations");
        EnsureCanonicalIds(checkpoint.Memories.Select(item => item.Id), "memories");
        EnsureCanonicalIds(checkpoint.Estates.Select(item => item.Id), "estates");
        EnsureCanonicalIds(checkpoint.Births.Select(item => item.RequestId), "births");
        foreach (var household in checkpoint.Households)
        {
            EnsureCanonicalIds(household.MemberIds, $"household:{household.Id}:members");
            EnsureCanonicalIds(household.CaregiverIds, $"household:{household.Id}:caregivers");
            if (household.MemberIds.Any(id => !checkpoint.Inhabitants.Any(item => item.Id == id)))
            {
                throw new InvalidDataException("Households cannot reference unknown inhabitants.");
            }
        }

        foreach (var relationship in checkpoint.Relationships)
        {
            if (relationship.Revision <= 0 || relationship.ProposedTick < 0 || relationship.EffectiveTick < 0 ||
                string.IsNullOrWhiteSpace(relationship.ProposerId) ||
                string.IsNullOrWhiteSpace(relationship.TargetId) ||
                (relationship.State == SocietyRelationshipState.Accepted &&
                    relationship.Consent is not (SocietyConsentState.Accepted or SocietyConsentState.ProtectedLifecycle)))
            {
                throw new InvalidDataException("A relationship record is malformed.");
            }
        }

        var expectedEventId = 1L;
        var previousTick = 0L;
        foreach (var societyEvent in checkpoint.Events)
        {
            if (societyEvent.EventId != expectedEventId || societyEvent.WorldTick < previousTick ||
                societyEvent.WorldTick > checkpoint.WorldTick)
            {
                throw new InvalidDataException("Society events must be a canonical committed sequence.");
            }

            expectedEventId++;
            previousTick = societyEvent.WorldTick;
        }
    }

    private static SocietyOperationResult Kill(
        SocietyCheckpoint checkpoint,
        string inhabitantId,
        SocietyDeathCause cause,
        long deathTick)
    {
        var inhabitant = checkpoint.GetInhabitant(inhabitantId);
        var estateId = $"estate:{inhabitant.Id}:{deathTick.ToString(CultureInfo.InvariantCulture)}";
        var householdBeneficiaries = checkpoint.Households
            .Where(household => household.MemberIds.Contains(inhabitant.Id, StringComparer.Ordinal))
            .SelectMany(household => household.MemberIds.Concat(household.CaregiverIds))
            .Where(id => id != inhabitant.Id)
            .Where(id => checkpoint.GetInhabitant(id).Status == SocietyInhabitantStatus.Active)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var inventory = MoveOwnedLotsToEstate(checkpoint.Inventory, inhabitant.Id, estateId, deathTick);
        var relationships = checkpoint.Relationships.Select(relationship =>
                relationship.ProposerId == inhabitant.Id || relationship.TargetId == inhabitant.Id
                    ? relationship with
                    {
                        State = relationship.State is SocietyRelationshipState.Accepted or SocietyRelationshipState.Proposed
                            ? SocietyRelationshipState.EndedByDeath
                            : relationship.State,
                        Consent = relationship.State is SocietyRelationshipState.Accepted or SocietyRelationshipState.Proposed
                            ? SocietyConsentState.Revoked
                            : relationship.Consent,
                        EffectiveTick = deathTick,
                    }
                    : relationship)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var next = checkpoint with
        {
            WorldTick = deathTick,
            Inhabitants = checkpoint.Inhabitants.Select(item => item.Id == inhabitant.Id
                    ? item with
                    {
                        Status = SocietyInhabitantStatus.Dead,
                        HealthBasisPoints = 0,
                        DeathTick = deathTick,
                        DeathCause = cause,
                    }
                    : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Relationships = relationships,
            Estates = checkpoint.Estates.Append(new SocietyEstate(
                    estateId,
                    inhabitant.Id,
                    deathTick,
                    checked(deathTick + checkpoint.Config.EstateEscrowDays *
                        (long)checkpoint.Config.TicksPerWorldDay),
                    householdBeneficiaries))
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Inventory = inventory,
        };
        var result = Commit(next, "death_committed", $"{inhabitant.Id}:{cause}:{deathTick}", inhabitant.Id);
        foreach (var relationship in relationships.Where(item =>
                     (item.ProposerId == inhabitant.Id || item.TargetId == inhabitant.Id) &&
                     item.State == SocietyRelationshipState.EndedByDeath))
        {
            result = Commit(result.Checkpoint, "relationship_ended_by_death", relationship.Id);
        }

        return Commit(
            result.Checkpoint,
            "estate_created",
            $"{estateId}:{string.Join(',', householdBeneficiaries)}",
            estateId);
    }

    private static SocietyCheckpoint SettleDueEstates(
        SocietyCheckpoint checkpoint,
        long targetTick)
    {
        var current = checkpoint;
        foreach (var estate in checkpoint.Estates.Where(item => !item.Settled && item.ExpiryTick <= targetTick)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var beneficiaries = estate.BeneficiaryIds
                .Where(id => current.Inhabitants.Any(item =>
                    item.Id == id && item.Status == SocietyInhabitantStatus.Active))
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var lots = current.Inventory.Lots.Where(lot => lot.OwnerId == estate.Id).ToArray();
            var nextLots = current.Inventory.Lots.Where(lot => lot.OwnerId != estate.Id).ToList();
            foreach (var lot in lots)
            {
                if (beneficiaries.Length == 0)
                {
                    nextLots.Add(lot with { OwnerId = "settlement:communal" });
                    continue;
                }

                var baseShare = lot.Quantity / beneficiaries.Length;
                var remainder = lot.Quantity % beneficiaries.Length;
                for (var index = 0; index < beneficiaries.Length; index++)
                {
                    var quantity = baseShare + (index < remainder ? 1 : 0);
                    if (quantity == 0)
                    {
                        continue;
                    }

                    nextLots.Add(lot with
                    {
                        Id = $"{lot.Id}#estate:{estate.Id}:{beneficiaries[index]}",
                        OwnerId = beneficiaries[index],
                        Quantity = quantity,
                        ProvenanceLotId = lot.Id,
                    });
                }
            }

            var inventoryEvents = current.Inventory.Events.ToList();
            inventoryEvents.Add(new InventoryEvent(
                checked(inventoryEvents.Count + 1L),
                targetTick,
                "estate_settled",
                estate.Id));
            current = current with
            {
                WorldTick = targetTick,
                Inventory = new InventoryCheckpoint(
                    targetTick,
                    nextLots.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                    current.Inventory.Reservations,
                    current.Inventory.Offers,
                    inventoryEvents),
                Estates = current.Estates.Select(item =>
                        item.Id == estate.Id ? item with { Settled = true } : item)
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            };
            current = Commit(current, "estate_settled", estate.Id).Checkpoint;
        }

        return current;
    }

    private static InventoryCheckpoint MoveOwnedLotsToEstate(
        InventoryCheckpoint inventory,
        string ownerId,
        string estateId,
        long targetTick)
    {
        var lots = inventory.Lots.Select(lot => lot.OwnerId == ownerId
                ? lot with { OwnerId = estateId }
                : lot)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var reservations = inventory.Reservations.Select(reservation => reservation.OwnerId == ownerId
                ? reservation with { State = InventoryReservationState.Released }
                : reservation)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var offers = inventory.Offers.Select(offer =>
                offer.FirstPartyId == ownerId || offer.SecondPartyId == ownerId
                    ? offer with { State = DirectBarterState.Cancelled }
                    : offer)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var events = inventory.Events.ToList();
        events.Add(new InventoryEvent(
            checked(events.Count + 1L),
            targetTick,
            "estate_escrow_created",
            estateId));
        return new InventoryCheckpoint(targetTick, lots, reservations, offers, events);
    }

    private static InventoryCheckpoint WithInventoryTick(
        InventoryCheckpoint inventory,
        long targetTick) =>
        inventory.WorldTick == targetTick
            ? inventory
            : inventory with { WorldTick = targetTick };

    private static SocietyRelationship BirthRelationship(
        SocietyBirthRequest request,
        string childId,
        SocietyRelationshipType type,
        string sourceId)
    {
        return new SocietyRelationship(
            $"birth:{request.Id}:{type}:{sourceId}",
            1,
            type,
            sourceId,
            childId,
            SocietyRelationshipState.Accepted,
            SocietyConsentState.ProtectedLifecycle,
            request.RequestedTick,
            request.RequestedTick,
            type == SocietyRelationshipType.HouseholdMembership ? "household" : "family",
            request.HouseholdId,
            new[] { sourceId, childId }.OrderBy(item => item, StringComparer.Ordinal).ToArray());
    }

    private static string? ResolveNewbornProvider(
        SocietyCheckpoint checkpoint,
        SocietyInhabitant firstParent,
        SocietyInhabitant secondParent,
        SocietyBirthRequest request) =>
        request.ProviderPolicy switch
        {
            NewbornProviderPolicy.PerChild => NormalizeOptionalText(request.RequestedProviderBindingId),
            NewbornProviderPolicy.ParentInheritance =>
                firstParent.ProviderBindingId == secondParent.ProviderBindingId
                    ? firstParent.ProviderBindingId
                    : null,
            NewbornProviderPolicy.WorldDefault => checkpoint.WorldDefaultProviderBindingId,
            NewbornProviderPolicy.Hybrid =>
                firstParent.ProviderBindingId is not null &&
                firstParent.ProviderBindingId == secondParent.ProviderBindingId
                    ? firstParent.ProviderBindingId
                    : checkpoint.WorldDefaultProviderBindingId,
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

    private static SocietyCheckpoint ApplyRelationshipProjection(
        SocietyCheckpoint checkpoint,
        SocietyRelationship relationship)
    {
        if (relationship.Type != SocietyRelationshipType.HouseholdMembership &&
            relationship.Type != SocietyRelationshipType.Caregiver)
        {
            return checkpoint;
        }

        if (relationship.HouseholdId is null)
        {
            return checkpoint;
        }

        var household = checkpoint.GetHousehold(relationship.HouseholdId);
        var members = relationship.Type == SocietyRelationshipType.HouseholdMembership
            ? household.MemberIds.Append(relationship.TargetId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray()
            : household.MemberIds;
        var caregivers = relationship.Type == SocietyRelationshipType.Caregiver
            ? household.CaregiverIds.Append(relationship.ProposerId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray()
            : household.CaregiverIds;
        return checkpoint with
        {
            Inhabitants = relationship.Type == SocietyRelationshipType.HouseholdMembership
                ? checkpoint.Inhabitants.Select(item => item.Id == relationship.TargetId
                        ? item with { HouseholdId = household.Id }
                        : item)
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray()
                : checkpoint.Inhabitants,
            Households = checkpoint.Households.Select(item => item.Id == household.Id
                    ? item with { MemberIds = members, CaregiverIds = caregivers }
                    : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
    }

    private static SocietyCheckpoint RemoveRelationshipProjection(
        SocietyCheckpoint checkpoint,
        SocietyRelationship relationship)
    {
        if (relationship.Type != SocietyRelationshipType.HouseholdMembership &&
            relationship.Type != SocietyRelationshipType.Caregiver ||
            relationship.HouseholdId is null)
        {
            return checkpoint;
        }

        var household = checkpoint.GetHousehold(relationship.HouseholdId);
        return checkpoint with
        {
            Inhabitants = relationship.Type == SocietyRelationshipType.HouseholdMembership
                ? checkpoint.Inhabitants.Select(item => item.Id == relationship.TargetId
                        ? item with { HouseholdId = null }
                        : item)
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray()
                : checkpoint.Inhabitants,
            Households = checkpoint.Households.Select(item => item.Id == household.Id
                    ? item with
                    {
                        MemberIds = relationship.Type == SocietyRelationshipType.HouseholdMembership
                            ? household.MemberIds.Where(id => id != relationship.TargetId).ToArray()
                            : household.MemberIds,
                        CaregiverIds = relationship.Type == SocietyRelationshipType.Caregiver
                            ? household.CaregiverIds.Where(id => id != relationship.ProposerId).ToArray()
                            : household.CaregiverIds,
                    }
                    : item)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
    }

    private static SocietyOperationResult Commit(
        SocietyCheckpoint checkpoint,
        string kind,
        string detail,
        string? createdId = null)
    {
        var next = CommitMany(checkpoint, [(kind, detail)]);
        return new SocietyOperationResult(next, createdId, [next.Events[^1]]);
    }

    private static SocietyCheckpoint CommitMany(
        SocietyCheckpoint checkpoint,
        IEnumerable<(string Kind, string Detail)> pending)
    {
        var events = checkpoint.Events.ToList();
        foreach (var item in pending)
        {
            events.Add(new SocietyEvent(
                checked(events.Count + 1L),
                checkpoint.WorldTick,
                NormalizeRequiredText(item.Kind, nameof(item.Kind)),
                NormalizeRequiredText(item.Detail, nameof(item.Detail))));
        }

        var next = checkpoint with { Events = events };
        Validate(next);
        return next;
    }

    private static SocietyOperationResult Reject(
        SocietyCheckpoint checkpoint,
        string kind,
        string detail) =>
        Commit(checkpoint, kind, detail);

    private static bool HasActivePartnership(
        SocietyCheckpoint checkpoint,
        string firstId,
        string secondId) =>
        checkpoint.Relationships.Any(item =>
            item.Type == SocietyRelationshipType.Partnership &&
            item.State == SocietyRelationshipState.Accepted &&
            ((item.ProposerId == firstId && item.TargetId == secondId) ||
             (item.ProposerId == secondId && item.TargetId == firstId)));

    private static bool IsAdult(SocietyInhabitant inhabitant) =>
        inhabitant.Status == SocietyInhabitantStatus.Active &&
        inhabitant.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder;

    private static void EnsureActive(SocietyCheckpoint checkpoint, string id)
    {
        if (checkpoint.GetInhabitant(id).Status != SocietyInhabitantStatus.Active)
        {
            throw new InvalidOperationException($"Inhabitant '{id}' is not active.");
        }
    }

    private static void EnsureLivingParty(
        SocietyCheckpoint checkpoint,
        string id)
    {
        if (!checkpoint.Inhabitants.Any(item =>
                item.Id == id && item.Status == SocietyInhabitantStatus.Active) &&
            !checkpoint.Households.Any(item => item.Id == id) &&
            !checkpoint.Organizations.Any(item => item.Id == id))
        {
            throw new InvalidOperationException($"Party '{id}' is not an active society participant.");
        }
    }

    private static void ValidateProposal(
        SocietyRelationshipProposal proposal,
        long worldTick)
    {
        NormalizeRequiredText(proposal.Id, nameof(proposal.Id));
        NormalizeRequiredText(proposal.ProposerId, nameof(proposal.ProposerId));
        NormalizeRequiredText(proposal.TargetId, nameof(proposal.TargetId));
        NormalizeRequiredText(proposal.PrivacyClass, nameof(proposal.PrivacyClass));
        if (proposal.Revision <= 0 || proposal.ProposerId == proposal.TargetId ||
            proposal.RequestedTick < 0 || proposal.RequestedTick > worldTick ||
            proposal.Type is SocietyRelationshipType.BiologicalParentage)
        {
            throw new ArgumentOutOfRangeException(nameof(proposal));
        }
    }

    private static void ValidateBirthRequest(
        SocietyBirthRequest request,
        long worldTick)
    {
        NormalizeRequiredText(request.Id, nameof(request.Id));
        NormalizeRequiredText(request.FirstParentId, nameof(request.FirstParentId));
        NormalizeRequiredText(request.SecondParentId, nameof(request.SecondParentId));
        NormalizeRequiredText(request.HouseholdId, nameof(request.HouseholdId));
        if (request.Revision <= 0 || request.FirstParentId == request.SecondParentId ||
            request.FoodQuantity <= 0 || request.RequestedTick < 0 ||
            request.RequestedTick > worldTick || request.CaregiverIds.Count == 0 ||
            request.ConsentingParentIds.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private static void ValidateInhabitants(
        IReadOnlyList<SocietyInhabitant> inhabitants,
        SocietyConfig config,
        long worldTick)
    {
        EnsureCanonicalIds(inhabitants.Select(item => item.Id), "inhabitants");
        foreach (var inhabitant in inhabitants)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inhabitant.Name);
            if (inhabitant.BirthTick > worldTick ||
                inhabitant.HealthBasisPoints is < 0 or > 10_000 ||
                inhabitant.LastLifecycleYearChecked < 0 ||
                inhabitant.Status == SocietyInhabitantStatus.Active && inhabitant.DeathTick is not null ||
                inhabitant.Status == SocietyInhabitantStatus.Dead && inhabitant.DeathTick is null)
            {
                throw new InvalidDataException("An inhabitant lifecycle record is malformed.");
            }

            var expectedBand = config.AgeBandAt(inhabitant.BirthTick, worldTick);
            if (inhabitant.Status == SocietyInhabitantStatus.Active &&
                inhabitant.AgeBand != expectedBand &&
                inhabitant.BirthTick +
                    config.TicksPerWorldYear * inhabitant.LastLifecycleYearChecked <= worldTick)
            {
                throw new InvalidDataException(
                    $"An active inhabitant has a stale age band: {inhabitant.Id}:{inhabitant.AgeBand}:{expectedBand}:" +
                    $"tick={worldTick}:birth={inhabitant.BirthTick}:last={inhabitant.LastLifecycleYearChecked}.");
            }
        }
    }

    private static int MortalityRoll(
        string worldId,
        string inhabitantId,
        int ageYears)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"agentworld.society-mortality/v1|{worldId}|{inhabitantId}|{ageYears}"));
        return checked((int)(BitConverter.ToUInt32(bytes, 0) % 10_000));
    }

    private static string[] CanonicalIds(
        IEnumerable<string> ids,
        string name)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var actual = ids.Select(id => NormalizeRequiredText(id, name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (actual.Length == 0)
        {
            throw new ArgumentException("At least one ID is required.", name);
        }

        return actual;
    }

    private static void EnsureCanonicalIds(
        IEnumerable<string> ids,
        string name)
    {
        var actual = ids.ToArray();
        if (actual.Any(string.IsNullOrWhiteSpace) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !actual.SequenceEqual(actual.OrderBy(id => id, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"Society {name} must contain unique canonical IDs.");
        }
    }

    private static int ValidateBasisPoints(int value, string name)
    {
        if (value is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(name);
        }

        return value;
    }

    private static string NormalizeRequiredText(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
