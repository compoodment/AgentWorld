namespace ClankerWorld.Simulation.Society;

public static partial class SocietyFixture
{
    /// <summary>Protected care assignment for an infant, never consent on behalf of a capable dependent.</summary>
    public static SocietyOperationResult AssumeInfantCare(SocietyCheckpoint checkpoint, string adultId, string childId)
    {
        Validate(checkpoint);
        var adult = checkpoint.GetInhabitant(adultId);
        var child = checkpoint.GetInhabitant(childId);
        if (adult.Status != SocietyInhabitantStatus.Active || child.Status != SocietyInhabitantStatus.Active ||
            adult.AgeBand is not (SocietyAgeBand.Adult or SocietyAgeBand.Elder) || child.AgeBand != SocietyAgeBand.Infant ||
            adult.HouseholdId is null || adult.HouseholdId != child.HouseholdId ||
            checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted &&
                checkpoint.GetInhabitant(edge.ProposerId).Status == SocietyInhabitantStatus.Active))
        {
            return Reject(checkpoint, "care_assignment_rejected", "ineligible_or_existing_caregiver");
        }
        var id = $"dependent-care:{adultId}:{childId}:{checkpoint.WorldTick}";
        if (checkpoint.Relationships.Any(edge => edge.Id == id))
            return Reject(checkpoint, "care_assignment_rejected", "duplicate_assignment");
        var edge = new SocietyRelationship(id, 1, SocietyRelationshipType.Caregiver, adultId, childId,
            SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle,
            checkpoint.WorldTick, checked(checkpoint.WorldTick + 1), "public", adult.HouseholdId, [adultId]);
        var next = checkpoint with
        {
            Relationships = checkpoint.Relationships.Append(edge).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return Commit(ApplyRelationshipProjection(next, edge), "dependent_care_assigned", id, id);
    }
}
