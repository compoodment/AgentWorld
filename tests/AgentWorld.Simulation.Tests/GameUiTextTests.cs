using AgentWorld.GodotClient.UI;

namespace AgentWorld.Simulation.Tests;

public sealed class GameUiTextTests
{
    [Theory]
    [InlineData(0, "Day 1 · 00:00")]
    [InlineData(62, "Day 1 · 01:02")]
    [InlineData(1_440, "Day 2 · 00:00")]
    public void WorldClockUsesDaysAndTimeInsteadOfRawTicks(long worldTick, string expected)
    {
        Assert.Equal(expected, GameUiText.FormatWorldClock(worldTick));
    }

    [Theory]
    [InlineData("tick_advanced", false)]
    [InlineData("cognition_requested", false)]
    [InlineData("inhabitant_moved", false)]
    [InlineData("build_completed", true)]
    [InlineData("trade_completed", true)]
    [InlineData("religion_founded", true)]
    public void EventTimelineKeepsWorldNewsAndDropsProtocolNoise(string kind, bool expected)
    {
        Assert.Equal(expected, GameUiText.IsPlayerFacingEvent(kind));
    }

    [Theory]
    [InlineData("build:recipe:carrot", "build Carrot")]
    [InlineData("build:building:sha256:abcdef/building/stone-hearth@1.0.0", "build Stone hearth")]
    [InlineData("consume_food", "eat")]
    [InlineData("trade_propose:founder-mira", "offer an exchange")]
    [InlineData("trade_accept:settlement-trade:1:founder-mira:founder-rowan", "accept an exchange")]
    [InlineData("trade_decline:settlement-trade:1:founder-mira:founder-rowan", "decline an exchange")]
    [InlineData("seek_food", "find food")]
    [InlineData("religion_founded", "Religion founded")]
    public void InternalIdentifiersBecomeReadablePhrases(string value, string expected)
    {
        Assert.Equal(expected, GameUiText.HumanizeIdentifier(value));
    }
}
