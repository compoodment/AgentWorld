using AgentWorld.Simulation.Playtest;
using AgentWorld.Simulation.Harness;

namespace AgentWorld.Simulation.Tests;

public sealed class PrivateWorldRuntimeTests
{
    [Fact]
    public void PrivateWorldStartsWithAnActiveSettlementInsteadOfAuthoringDrafts()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");

        Assert.Equal(4, runtime.Society.Inhabitants.Count);
        Assert.All(runtime.Society.Inhabitants, inhabitant =>
            Assert.Equal(AgentWorld.Simulation.Society.SocietyInhabitantStatus.Active, inhabitant.Status));
        Assert.Single(runtime.Society.Households);
        Assert.Equal(4, runtime.Inhabitants.Count);
        Assert.Contains(runtime.Inhabitants, inhabitant => inhabitant.Personality == "curious");
        Assert.Contains(runtime.Inhabitants, inhabitant => inhabitant.Aspiration == "build something lasting");
    }

    [Fact]
    public async Task PrivateWorldAdvancesAllFoundersThroughBoundedCognition()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");

        var result = await runtime.AdvanceOneTickAsync();

        Assert.True(result.Advanced);
        Assert.Equal(1, result.WorldTick);
        Assert.Equal(4, result.Decisions.Count);
        Assert.All(result.Decisions, decision => Assert.True(decision.Admission.Accepted));
        Assert.Contains(result.Events, worldEvent => worldEvent.Kind == "inhabitant_moved");
        Assert.All(runtime.Inhabitants, inhabitant => Assert.True(inhabitant.HungerBasisPoints < 6_500));
    }

    [Fact]
    public async Task PrivateWorldCheckpointRoundTripsWithTheSamePopulationAndTick()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        _ = await runtime.AdvanceOneTickAsync();

        var encoded = PrivateWorldRuntimeCodec.Encode(runtime.ExportState());
        var restoredState = PrivateWorldRuntimeCodec.Decode(encoded);
        using var restored = PrivateWorldRuntime.Restore(restoredState);

        Assert.Equal(runtime.WorldTick, restored.WorldTick);
        Assert.Equal(
            runtime.Society.Inhabitants.Select(item => item.Id),
            restored.Society.Inhabitants.Select(item => item.Id));
        Assert.Equal(runtime.Inhabitants, restored.Inhabitants);
        Assert.Equal(
            encoded,
            PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task PrivateWorldPauseIsAnIdempotentBoundary()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        runtime.Pause();
        runtime.Pause();

        var paused = await runtime.AdvanceOneTickAsync();

        Assert.False(paused.Advanced);
        Assert.Equal("paused", paused.Outcome);
        Assert.Single(runtime.ExportState().Events, worldEvent => worldEvent.Kind == "paused");
    }

    [Fact]
    public async Task PrivateWorldInstructionsAreIdempotentAndReachCognition()
    {
        using var runtime = new PrivateWorldRuntime("playtest-alpha");
        var request = new OwnerInstructionRequest(
            "instruction-key-1",
            "owner-device:test",
            "founder-rowan",
            OwnerInstructionKind.MustDo,
            "sleep at the bedroll");

        var first = runtime.SubmitInstruction(request);
        var replay = runtime.SubmitInstruction(request);
        _ = await runtime.AdvanceOneTickAsync();

        Assert.Equal(first, replay);
        Assert.Contains(runtime.ExportState().CompletedInstructionIds!, id => id == first.InstructionId);
        Assert.Contains(
            runtime.ExportState().Events,
            worldEvent => worldEvent.Kind == "instruction_applied" && worldEvent.Detail.Contains(first.InstructionId, StringComparison.Ordinal));
    }
}
