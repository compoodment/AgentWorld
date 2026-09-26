using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Cognition;

/// <summary>
/// Canonical digest for the compact observation sent to a provider. It binds
/// the provider response to the exact actor state and legal candidate set that
/// the authoritative runtime admitted.
/// </summary>
public static class CognitionObservationDigest
{
    public static string Create(
        HarnessWorld world,
        long runEpoch,
        long decisionGeneration,
        IReadOnlyList<CognitionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(candidates);

        var builder = new StringBuilder("clankerworld.cognition-observation/v1\n");
        builder.Append("world_id=").Append(world.Identity.WorldId).Append('\n');
        builder.Append("world_tick=").Append(world.Identity.WorldTick).Append('\n');
        builder.Append("run_epoch=").Append(runEpoch).Append('\n');
        builder.Append("decision_generation=").Append(decisionGeneration).Append('\n');
        builder.Append("actor=").Append(world.Actor.Id).Append('|')
            .Append(world.Actor.Position.X).Append(',').Append(world.Actor.Position.Y).Append('|')
            .Append(world.Actor.HungerBasisPoints).Append('|')
            .Append(world.Actor.EnergyBasisPoints).Append('|')
            .Append(world.Actor.FoodItems).Append('|')
            .Append(world.Actor.WoodItems).Append('\n');
        foreach (var resource in world.Resources.OrderBy(resource => resource.Id, StringComparer.Ordinal))
        {
            builder.Append("resource=").Append(resource.Id).Append('|')
                .Append(resource.State).Append('\n');
        }

        foreach (var candidate in candidates)
        {
            builder.Append("candidate=").Append(candidate.Id).Append('|')
                .Append(candidate.Description).Append('|')
                .Append(candidate.DeterministicPriority).Append('|')
                .Append(candidate.DestinationId ?? string.Empty).Append('\n');
        }

        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))}";
    }
}
