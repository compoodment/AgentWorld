using ClankerWorld.GodotClient.UI;

namespace ClankerWorld.Simulation.Tests;

public sealed class GameUiTextTests
{
    [Fact]
    public void ResourceHelpDistinguishesExhaustionRegrowthAndLegacyUnknownQuantities()
    {
        var wood = new OwnerWorldResource("wood", "construction", new(0, 0), false, "depleted", 0, 12, 0, 0, "spring");
        Assert.Equal("Wild timber · 0/12\nDepleted\nFinite — no natural regrowth.", GameUiText.ResourceTooltip(wood));
        var berries = wood with { Id = "food", Kind = "food", IsRenewable = true, RegenerationAmount = 4, RegenerationIntervalDays = 1 };
        Assert.Contains("+4 every 1 world day(s) in Spring", GameUiText.ResourceTooltip(berries), StringComparison.Ordinal);
        Assert.Equal("Soil 3/3", GameUiText.ResourceQuantity("fertile_land", 3, 3));
        var legacy = new OwnerWorldResource("old", "food", new(0, 0), true, "available");
        Assert.Contains("details unavailable", GameUiText.ResourceTooltip(legacy), StringComparison.Ordinal);
        Assert.DoesNotContain("0/", GameUiText.ResourceTooltip(legacy), StringComparison.Ordinal);
    }

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
    [InlineData("parent_propose:founder-mira", "discuss parenthood")]
    [InlineData("parent_accept:founder-mira", "agree to parenthood")]
    [InlineData("parent_cancel:founder-mira", "decline or withdraw parenthood")]
    [InlineData("care:child-1", "care for a child")]
    [InlineData("guardian_offer:child-1", "offer to care for a dependent")]
    [InlineData("guardian_accept:proposal", "accept a caregiver")]
    [InlineData("guardian_refuse:proposal", "refuse a caregiver proposal")]
    [InlineData("guardian_end:proposal", "withdraw from caregiving")]
    [InlineData("learn:builder:founder-ilya", "request practical training")]
    [InlineData("lesson_teach:founder-scout", "take part in training")]
    [InlineData("lesson_decline:founder-scout", "decline or stop training")]
    [InlineData("council_propose:essential_first", "propose a food policy")]
    [InlineData("council_vote_yes", "support a food policy")]
    [InlineData("council_vote_no", "oppose a food policy")]
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
