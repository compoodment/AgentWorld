using AgentWorld.Simulation;

namespace AgentWorld.Simulation.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void SimulationAssemblyDoesNotReferenceGodot()
    {
        var references = typeof(SimulationAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => reference.Name?.StartsWith("Godot", StringComparison.Ordinal) == true);
    }
}
