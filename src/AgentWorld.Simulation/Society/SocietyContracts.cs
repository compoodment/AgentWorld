using AgentWorld.Simulation.Kernel;

namespace AgentWorld.Simulation.Society;

public enum SocietyInhabitantStatus
{
    Active,
    Dead,
}

public enum SocietyAgeBand
{
    Infant,
    Child,
    Adolescent,
    Adult,
    Elder,
}

public enum SocietyRelationshipType
{
    Partnership,
    Caregiver,
    HouseholdMembership,
    BiologicalParentage,
    LegalGuardian,
}

public enum SocietyRelationshipState
{
    Proposed,
    Accepted,
    Rejected,
    Revoked,
    Dissolved,
    EndedByDeath,
}

public enum SocietyConsentState
{
    Pending,
    Accepted,
    Refused,
    ProtectedLifecycle,
    Revoked,
}

public enum SocietyDeathCause
{
    NaturalAge,
    Hazard,
    Illness,
    Accident,
    Rule,
}

public enum SocietyWorkRole
{
    Unassigned,
    Farmer,
    Builder,
    Caregiver,
    Trader,
    Teacher,
    Organizer,
}

public enum SocietyOrganizationKind
{
    Farm,
    Workshop,
    Organization,
}

public enum NewbornProviderPolicy
{
    PerChild,
    ParentInheritance,
    WorldDefault,
    Hybrid,
}

/// <summary>
/// Versioned lifecycle tuning. There is deliberately no maximum age field:
/// natural mortality is a rising probability, not a hidden hard cap.
/// </summary>
public sealed record SocietyConfig(
    int TicksPerWorldDay = KernelClock.TicksPerDay,
    int DaysPerWorldYear = KernelClock.DaysPerYear,
    int InfantYears = 2,
    int ChildYears = 12,
    int AdultYears = 18,
    int ElderYears = 65,
    int EstateEscrowDays = 7,
    int BaseNaturalMortalityBasisPoints = 100,
    int NaturalMortalitySlopeBasisPoints = 25,
    int ContractVersion = 1)
{
    public long TicksPerWorldYear => checked((long)TicksPerWorldDay * DaysPerWorldYear);

    public int AgeAt(long birthTick, long worldTick)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(worldTick, birthTick);
        return checked((int)((worldTick - birthTick) / TicksPerWorldYear));
    }

    public SocietyAgeBand AgeBandAt(long birthTick, long worldTick) => AgeBandAt(AgeAt(birthTick, worldTick));

    public SocietyAgeBand AgeBandAt(int ageYears) => ageYears switch
    {
        _ when ageYears < InfantYears => SocietyAgeBand.Infant,
        _ when ageYears < ChildYears => SocietyAgeBand.Child,
        _ when ageYears < AdultYears => SocietyAgeBand.Adolescent,
        _ when ageYears < ElderYears => SocietyAgeBand.Adult,
        _ => SocietyAgeBand.Elder,
    };

    public int NaturalMortalityRiskBasisPoints(int ageYears)
    {
        if (ageYears < ElderYears)
        {
            return 0;
        }

        return Math.Min(
            9_999,
            checked(BaseNaturalMortalityBasisPoints +
                (ageYears - ElderYears) * NaturalMortalitySlopeBasisPoints));
    }

    public void Validate()
    {
        if (TicksPerWorldDay <= 0 || DaysPerWorldYear <= 0 || InfantYears <= 0 ||
            ChildYears <= InfantYears || AdultYears <= ChildYears || ElderYears <= AdultYears ||
            EstateEscrowDays <= 0 || BaseNaturalMortalityBasisPoints < 0 ||
            NaturalMortalitySlopeBasisPoints < 0 || ContractVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SocietyConfig));
        }
    }
}

public sealed record SocietyInhabitant(
    string Id,
    string Name,
    long BirthTick,
    SocietyInhabitantStatus Status,
    SocietyAgeBand AgeBand,
    int HealthBasisPoints,
    string? HouseholdId,
    string? ProviderBindingId,
    SocietyWorkRole CurrentRole,
    long LastLifecycleYearChecked,
    long? DeathTick = null,
    SocietyDeathCause? DeathCause = null);

public sealed record SocietyHousehold(
    string Id,
    string Name,
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<string> CaregiverIds);

public sealed record SocietyRelationship(
    string Id,
    int Revision,
    SocietyRelationshipType Type,
    string ProposerId,
    string TargetId,
    SocietyRelationshipState State,
    SocietyConsentState Consent,
    long ProposedTick,
    long EffectiveTick,
    string PrivacyClass,
    string? HouseholdId = null,
    IReadOnlyList<string>? AcceptedBy = null)
{
    public IReadOnlyList<string> AcceptedParties => AcceptedBy ?? [];
}

public sealed record SocietyRelationshipProposal(
    string Id,
    int Revision,
    SocietyRelationshipType Type,
    string ProposerId,
    string TargetId,
    long RequestedTick,
    string? HouseholdId = null,
    string PrivacyClass = "private");

public sealed record SocietyOrganization(
    string Id,
    SocietyOrganizationKind Kind,
    string OwnerId,
    IReadOnlyList<string> MemberIds,
    string InventoryOwnerId);

public sealed record SocietySocialMemory(
    string Id,
    string OwnerId,
    string SubjectId,
    string Summary,
    string Visibility,
    long SourceTick,
    long? TombstonedTick = null);

public sealed record SocietyBirthRequest(
    string Id,
    int Revision,
    string FirstParentId,
    string SecondParentId,
    string HouseholdId,
    IReadOnlyList<string> CaregiverIds,
    IReadOnlyList<string> ConsentingParentIds,
    string FoodLotId,
    int FoodQuantity,
    long RequestedTick,
    NewbornProviderPolicy ProviderPolicy = NewbornProviderPolicy.Hybrid,
    string? RequestedProviderBindingId = null);

public sealed record SocietyEstate(
    string Id,
    string DeceasedId,
    long CreatedTick,
    long ExpiryTick,
    IReadOnlyList<string> BeneficiaryIds,
    bool Settled = false);

public sealed record SocietyBirthRecord(
    string RequestId,
    string ChildId,
    int Revision,
    long CommittedTick);

public sealed record SocietyEvent(
    long EventId,
    long WorldTick,
    string Kind,
    string Detail);

public sealed record SocietyCheckpoint(
    string WorldId,
    long WorldTick,
    long RunEpoch,
    bool IsPaused,
    SocietyConfig Config,
    string? WorldDefaultProviderBindingId,
    IReadOnlyList<SocietyInhabitant> Inhabitants,
    IReadOnlyList<SocietyHousehold> Households,
    IReadOnlyList<SocietyRelationship> Relationships,
    IReadOnlyList<SocietyOrganization> Organizations,
    IReadOnlyList<SocietySocialMemory> Memories,
    IReadOnlyList<SocietyEstate> Estates,
    IReadOnlyList<SocietyBirthRecord> Births,
    InventoryCheckpoint Inventory,
    IReadOnlyList<SocietyEvent> Events)
{
    public SocietyInhabitant GetInhabitant(string id) =>
        Inhabitants.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    public SocietyHousehold GetHousehold(string id) =>
        Households.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    public SocietyRelationship GetRelationship(string id) =>
        Relationships.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    public SocietyEstate GetEstate(string id) =>
        Estates.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));
}

public sealed record SocietyOperationResult(
    SocietyCheckpoint Checkpoint,
    string? CreatedId = null,
    IReadOnlyList<SocietyEvent>? NewEvents = null);
