using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Society;
using AgentWorld.Viewer.Observation;

namespace AgentWorld.Simulation.Tests;

public sealed partial class SettlementParenthoodTests
{
    [Fact]
    public void EndingOneCareObligationPreservesOtherDependentsHouseholdProjection()
    {
        var adult = SocietyFixture.CreateFounder("adult", "Adult");
        var child = SocietyFixture.CreateFounder("child", "Child") with
        {
            BirthTick = 0,
            AgeBand = SocietyAgeBand.Infant,
            LastLifecycleYearChecked = 0,
        };
        var checkpoint = SocietyFixture.CreateGenesis("multiple-care", [adult, child, child with { Id = "other" }]);
        checkpoint = SocietyFixture.CreateHousehold(checkpoint, "home", "Home", ["adult", "child", "other"]).Checkpoint;
        var first = SocietyFixture.AssumeInfantCare(checkpoint, "adult", "child");
        var second = SocietyFixture.AssumeInfantCare(first.Checkpoint, "adult", "other");
        checkpoint = SocietyFixture.RevokeRelationship(second.Checkpoint, first.CreatedId!, "adult").Checkpoint;
        Assert.Contains("adult", checkpoint.GetHousehold("home").CaregiverIds);
        checkpoint = SocietyFixture.RevokeRelationship(checkpoint, second.CreatedId!, "adult").Checkpoint;
        Assert.DoesNotContain("adult", checkpoint.GetHousehold("home").CaregiverIds);
    }

    [Fact]
    public async Task OrphanedInfantGetsOneVoluntaryCaregiverAndRealCareAcrossRestart()
    {
        var state = await OrphanState();
        var child = state.Society.Society.Births.Single().ChildId;
        using var world = PrivateWorldRuntime.Restore(state, _ => new ParentProvider("guardian_offer:"));
        for (var tick = 0; tick < 40 && !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                 edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted); tick++) await world.AdvanceOneTickAsync();
        var caregiver = Assert.Single(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);
        Assert.Equal(SocietyConsentState.ProtectedLifecycle, caregiver.Consent);
        Assert.Equal([caregiver.ProposerId], caregiver.AcceptedBy);
        Assert.Equal(2, world.Society.Relationships.Count(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == child));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "caregiver_assigned");
        world.Pause();
        var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new ParentProvider("care:"));
        Assert.False((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Resume();
        for (var tick = 0; tick < 80 && !restored.ExportState().Events.Any(item => item.Kind == "child_cared_for"); tick++)
            await restored.AdvanceOneTickAsync();
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "child_cared_for");
        Assert.True(restored.Inhabitants.Single(person => person.InhabitantId == child).HungerBasisPoints > 3_000);
        Assert.Contains(new OwnerWorldObservationStore(restored).GetSnapshot().Inhabitants.Single(person => person.Id == child).Relationships,
            edge => edge.Type == "caregiver");
    }

    [Theory]
    [InlineData("guardian_accept:", SocietyRelationshipState.Accepted)]
    [InlineData("guardian_refuse:", SocietyRelationshipState.Rejected)]
    [InlineData("safe_idle", SocietyRelationshipState.Revoked)]
    public async Task OlderDependentsDecideIndependentlyAndSilenceExpires(string response, SocietyRelationshipState expected)
    {
        var state = await OrphanState(olderChild: true);
        var child = state.Society.Society.Births.Single().ChildId;
        var adult = state.Inhabitants.First(person => person.InhabitantId != child).InhabitantId;
        using var world = PrivateWorldRuntime.Restore(state, actor => new ParentProvider(actor == adult ? "guardian_offer:" : "safe_idle"));
        for (var tick = 0; tick < 40 && !world.Society.Relationships.Any(edge => edge.Id.StartsWith("settlement-care:", StringComparison.Ordinal)); tick++)
            await world.AdvanceOneTickAsync();
        var proposal = Assert.Single(world.Society.Relationships, edge => edge.Id.StartsWith("settlement-care:", StringComparison.Ordinal));
        Assert.Equal(SocietyRelationshipState.Proposed, proposal.State);
        Assert.Empty(proposal.AcceptedBy ?? []);
        using var restored = PrivateWorldRuntime.Restore(world.ExportState(), actor => new ParentProvider(actor == child ? response : "safe_idle"));
        for (var tick = 0; tick < 125; tick++) await restored.AdvanceOneTickAsync();
        var result = restored.Society.GetRelationship(proposal.Id);
        Assert.Equal(expected, result.State);
        if (expected == SocietyRelationshipState.Accepted)
        {
            Assert.Contains(child, result.AcceptedBy ?? []);
            Assert.Contains(adult, result.AcceptedBy ?? []);
        }
        else Assert.DoesNotContain(child, result.AcceptedBy ?? []);
    }

    [Fact]
    public async Task ProtectedCareCannotReplaceALivingCaregiverOrForgeAnOlderChildsConsent()
    {
        var state = await OrphanState();
        var checkpoint = state.Society.Society;
        var child = checkpoint.Births.Single().ChildId;
        var adults = checkpoint.Inhabitants.Where(person => person.Status == SocietyInhabitantStatus.Active && person.Id != child).ToArray();
        var accepted = SocietyFixture.AssumeInfantCare(checkpoint, adults[0].Id, child);
        Assert.NotNull(accepted.CreatedId);
        Assert.Null(SocietyFixture.AssumeInfantCare(accepted.Checkpoint, adults[1].Id, child).CreatedId);
        Assert.Null(SocietyFixture.AssumeInfantCare(accepted.Checkpoint, adults[0].Id, child).CreatedId);
        var older = await OrphanState(olderChild: true);
        Assert.Null(SocietyFixture.AssumeInfantCare(older.Society.Society, adults[0].Id, child).CreatedId);
    }

    [Fact]
    public async Task CaregiverCanWithdrawAndAnotherAdultCanVolunteerWithoutRewritingParentage()
    {
        var state = await OrphanState();
        var child = state.Society.Society.Births.Single().ChildId;
        var adult = state.Inhabitants.First(person => person.InhabitantId != child).InhabitantId;
        using var world = PrivateWorldRuntime.Restore(state, actor => new ParentProvider(actor == adult ? "guardian_offer:" : "safe_idle"));
        for (var tick = 0; tick < 40 && !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                 edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted); tick++) await world.AdvanceOneTickAsync();
        using var leaving = PrivateWorldRuntime.Restore(world.ExportState(), actor => new ParentProvider(actor == adult ? "guardian_end:" : "guardian_offer:"));
        for (var tick = 0; tick < 40; tick++) await leaving.AdvanceOneTickAsync();
        Assert.Contains(leaving.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Revoked);
        Assert.Contains(leaving.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId != adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);
        Assert.Equal(state.Society.Society.Births, leaving.Society.Births);
    }

    [Fact]
    public async Task CaregiverTelemetryExposesOutcomeButNotPrivateNames()
    {
        var directory = Directory.CreateTempSubdirectory("agentworld-care-log-");
        try
        {
            var state = await OrphanState();
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Inhabitants = state.Society.Society.Inhabitants.Select(person => person with { Name = "private-care-secret" }).ToArray(),
                    }
                }
            };
            using var world = PrivateWorldRuntime.Restore(state, _ => new ParentProvider("guardian_offer:"));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<PrivateWorldRuntimeService>();
            using var service = new PrivateWorldRuntimeService(world, new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            for (var tick = 0; tick < 40 && !logger.Messages.Any(message => message.Contains("event=caregiver_assigned", StringComparison.Ordinal)); tick++)
                Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Contains(logger.Messages, message => message.Contains("settlement_family", StringComparison.Ordinal) &&
                message.Contains("event=caregiver_assigned", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("private-care-secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<PrivateWorldRuntimeState> OrphanState(bool olderChild = false)
    {
        var state = await PreparedState();
        var first = state.Inhabitants[0].InhabitantId;
        var second = state.Inhabitants[1].InhabitantId;
        using var world = PrivateWorldRuntime.Restore(state, actor => new ParentProvider(actor == first ? "parent_propose:" : "parent_accept:"));
        for (var tick = 0; tick < 605; tick++) await world.AdvanceOneTickAsync();
        state = world.ExportState();
        var child = Assert.Single(state.Society.Society.Births).ChildId;
        using var society = SocietyWorldRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyFixture.Kill(checkpoint, first, SocietyDeathCause.Accident, checkpoint.WorldTick));
        society.Apply(checkpoint => SocietyFixture.Kill(checkpoint, second, SocietyDeathCause.Accident, checkpoint.WorldTick));
        var societyState = society.ExportState();
        if (olderChild)
        {
            societyState = societyState with
            {
                Society = societyState.Society with
                {
                    Inhabitants = societyState.Society.Inhabitants.Select(person => person.Id == child ? person with
                    {
                        BirthTick = societyState.Society.WorldTick - 3 * societyState.Society.Config.TicksPerWorldYear,
                        LastLifecycleYearChecked = 3,
                        AgeBand = SocietyAgeBand.Child,
                    } : person).ToArray(),
                }
            };
        }
        return state with
        {
            Society = societyState,
            Inhabitants = state.Inhabitants.Where(person => person.InhabitantId != first && person.InhabitantId != second)
                .Select(person => person with { LastDecisionContext = null, HungerBasisPoints = person.InhabitantId == child && !olderChild ? 2_000 : 9_000, EnergyBasisPoints = 9_000 }).ToArray(),
        };
    }
}
