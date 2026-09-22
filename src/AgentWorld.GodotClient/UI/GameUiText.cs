namespace AgentWorld.GodotClient.UI;

/// <summary>
/// Converts simulation identifiers into the deliberately small, player-facing
/// vocabulary used by the game HUD. Protocol diagnostics remain available in
/// developer tools instead of leaking into ordinary play.
/// </summary>
public static class GameUiText
{
    private const int MinutesPerDay = 1_440;

    public static string FormatWorldClock(long worldTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        var day = (worldTick / MinutesPerDay) + 1;
        var minuteOfDay = (int)(worldTick % MinutesPerDay);
        var hour = minuteOfDay / 60;
        var minute = minuteOfDay % 60;
        return $"Day {day} · {hour:00}:{minute:00}";
    }

    public static bool IsPlayerFacingEvent(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return kind switch
        {
            "tick_advanced" or
            "inhabitant_moved" or
            "actor_moved" or
            "destination_reached" or
            "movement_blocked" or
            "inhabitant_idle" => false,
            _ when kind.StartsWith("cognition_", StringComparison.Ordinal) => false,
            _ when kind.StartsWith("instruction_", StringComparison.Ordinal) => false,
            _ when kind.StartsWith("content_", StringComparison.Ordinal) => false,
            _ when kind.StartsWith("owner_", StringComparison.Ordinal) => false,
            _ when kind.EndsWith("_failed", StringComparison.Ordinal) => false,
            _ when kind.EndsWith("_rejected", StringComparison.Ordinal) => false,
            _ => true,
        };
    }

    public static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "something";
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("build:recipe:", StringComparison.Ordinal) ||
            normalized.StartsWith("build:building:", StringComparison.Ordinal))
        {
            return $"build {HumanizeIdentifier(normalized[(normalized.LastIndexOf(':') + 1)..])}";
        }

        var known = normalized switch
        {
            "safe_idle" => "take it easy",
            "seek_food" => "find food",
            "seek_rest" => "rest",
            "eat_food" => "eat",
            "harvest_food" => "gather food",
            _ => null,
        };
        if (known is not null)
        {
            return known;
        }

        var words = normalized
            .Replace(':', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('/', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "something";
        }

        var phrase = string.Join(' ', words.Select(word => word.ToLowerInvariant()));
        return char.ToUpperInvariant(phrase[0]) + phrase[1..];
    }
}
