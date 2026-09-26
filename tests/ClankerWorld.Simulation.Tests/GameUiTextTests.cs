using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class GameUiTextTests
{
    [Fact]
    public void OwnerSnapshotReportsTheSavedWorldCalendarPace()
    {
        using var world = new PrivateWorldRuntime("calendar-projection");
        var saved = world.ExportState();
        var pace = new OwnerWorldObservationStore(world).GetSnapshot().CalendarPace;
        Assert.NotNull(pace);
        Assert.Equal(saved.WorldSystems!.Config.TicksPerDay, pace.TicksPerDay);
        Assert.Equal(saved.WorldSystems.Config.DaysPerYear, pace.DaysPerYear);
    }

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
    [InlineData(0, "01-01-0001 · 00:00")]
    [InlineData(1_440, "02-01-0001 · 00:00")]
    [InlineData(44_640, "01-02-0001 · 00:00")]
    public void WorldClockUsesTheSavedCalendarInsteadOfRawTicks(long worldTick, string expected)
    {
        Assert.Equal(expected, GameUiText.FormatWorldClock(worldTick));
    }

    [Theory]
    [InlineData(0, "01-01-0001 · 12:00 AM")]
    [InlineData(720, "01-01-0001 · 12:00 PM")]
    [InlineData(780, "01-01-0001 · 1:00 PM")]
    [InlineData(1_439, "01-01-0001 · 11:59 PM")]
    public void WorldClockCanUseTwelveHourDisplayWithoutChangingWorldTime(long worldTick, string expected)
    {
        Assert.Equal(expected, GameUiText.FormatWorldClock(worldTick, useTwelveHourClock: true));
    }

    [Fact]
    public void CustomFortyDayYearUsesFourTenDayMonthsAndScalesClockFromWorldTicks()
    {
        var calendar = new OwnerWorldCalendarPace(360, 40);
        Assert.Equal("01-01-0001 · 00:04", GameUiText.FormatWorldClock(1, calendarPace: calendar));
        Assert.Equal("01-02-0001 · 00:00", GameUiText.FormatWorldClock(3_600, calendarPace: calendar));
        Assert.Equal("01-01-0002 · 00:00", GameUiText.FormatWorldClock(14_400, calendarPace: calendar));
        Assert.Equal("02-01-0002 · 12:00 PM", GameUiText.FormatWorldClock(14_940,
            useTwelveHourClock: true, calendarPace: calendar));
        Assert.Equal("01-02-0002 · 12:00", GameUiText.FormatWorldClock(14_940,
            calendarPace: calendar, dateFormat: "mdy"));
        Assert.Equal("0002-01-02 · 12:00", GameUiText.FormatWorldClock(14_940,
            calendarPace: calendar, dateFormat: "ymd"));
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
            store.Save(restored with { NotifyDeaths = false, DateFormat = "ymd" });
            Assert.False(store.Load().AllowsNotification("death"));
            Assert.True(store.Load().AllowsNotification("birth"));
            Assert.Equal("ymd", store.Load().DateFormat);
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
