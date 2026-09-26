using ClankerWorld.GodotClient.ClientState;
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
    [InlineData(1_440, "Day 2 · 00:00")]
    public void WorldClockUsesDaysAndTimeInsteadOfRawTicks(long worldTick, string expected)
    {
        Assert.Equal(expected, GameUiText.FormatWorldClock(worldTick));
    }

    [Theory]
    [InlineData(0, "Day 1 · 12:00 AM")]
    [InlineData(720, "Day 1 · 12:00 PM")]
    [InlineData(780, "Day 1 · 1:00 PM")]
    [InlineData(1_439, "Day 1 · 11:59 PM")]
    public void WorldClockCanUseTwelveHourDisplayWithoutChangingWorldTime(long worldTick, string expected)
    {
        Assert.Equal(expected, GameUiText.FormatWorldClock(worldTick, useTwelveHourClock: true));
    }

    [Fact]
    public void GameClockPreferencePersistsOutsideWorldSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "clankerworld-display-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new GameDisplayPreferencesStore(Path.Combine(directory, "game-settings.json"));
            Assert.False(store.Load().UseTwelveHourClock);
            store.Save(new GameDisplayPreferences(UseTwelveHourClock: true));
            var restored = new GameDisplayPreferencesStore(Path.Combine(directory, "game-settings.json")).Load();
            Assert.True(restored.UseTwelveHourClock);
            Assert.True(restored.NotifyBirths);
            store.Save(restored with { NotifyDeaths = false });
            Assert.False(store.Load().AllowsNotification("death"));
            Assert.True(store.Load().AllowsNotification("birth"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("child_born", "birth")]
    [InlineData("inhabitant_removed", "death")]
    [InlineData("inhabitant_building_proposed", "invention")]
    [InlineData("settlement_founded", "settlement")]
    [InlineData("food_consumed", null)]
    public void OnlyImportantEventsBecomeOptionalPopups(string kind, string? category)
    {
        Assert.Equal(category, GameUiText.NotificationCategory(kind));
    }

    [Theory]
    [InlineData("tick_advanced", false)]
    [InlineData("build_completed", true)]
    public void EventTimelineKeepsWorldNewsAndDropsProtocolNoise(string kind, bool expected)
    {
        Assert.Equal(expected, GameUiText.IsPlayerFacingEvent(kind));
    }

    [Theory]
    [InlineData("build:building:sha256:abcdef/building/stone-hearth@1.0.0", "build Stone hearth")]
    [InlineData("seek_food", "find food")]
    public void InternalIdentifiersBecomeReadablePhrases(string value, string expected)
    {
        Assert.Equal(expected, GameUiText.HumanizeIdentifier(value));
    }
}
