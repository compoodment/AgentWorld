namespace AgentWorld.Simulation;

/// <summary>
/// Marks the pure authoritative simulation assembly.
/// </summary>
/// <remarks>
/// This is intentionally not world behavior. It gives the foundation test a
/// stable assembly boundary to protect while actual kernel contracts arrive.
/// </remarks>
public sealed class SimulationAssemblyMarker;
