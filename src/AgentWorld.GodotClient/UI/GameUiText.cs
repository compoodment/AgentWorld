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
        if (normalized.StartsWith("care:", StringComparison.Ordinal)) return "care for a child";
        if (normalized.StartsWith("guardian_offer:", StringComparison.Ordinal)) return "offer to care for a dependent";
        if (normalized.StartsWith("guardian_accept:", StringComparison.Ordinal)) return "accept a caregiver";
        if (normalized.StartsWith("guardian_refuse:", StringComparison.Ordinal)) return "refuse a caregiver proposal";
        if (normalized.StartsWith("guardian_end:", StringComparison.Ordinal)) return "withdraw from caregiving";
        if (normalized.StartsWith("parent_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("parent_propose:", StringComparison.Ordinal) ? "discuss parenthood"
                : normalized.StartsWith("parent_accept:", StringComparison.Ordinal) ? "agree to parenthood" : "decline or withdraw parenthood";
        }
        if (normalized.StartsWith("partner_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("partner_propose:", StringComparison.Ordinal) ? "propose a partnership"
                : normalized.StartsWith("partner_accept:", StringComparison.Ordinal) ? "accept a partnership"
                : normalized.StartsWith("partner_refuse:", StringComparison.Ordinal) ? "refuse a partnership" : "leave a partnership";
        }
        if (normalized.StartsWith("learn:", StringComparison.Ordinal))
        {
            return "request practical training";
        }
        if (normalized.StartsWith("lesson_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("lesson_decline:", StringComparison.Ordinal) || normalized == "lesson_cancel"
                ? "decline or stop training" : "take part in training";
        }
        if (normalized.StartsWith("council_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("council_propose:", StringComparison.Ordinal) ? "propose a food policy"
                : normalized == "council_vote_yes" ? "support a food policy" : "oppose a food policy";
        }
        if (normalized.StartsWith("trade_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("trade_propose:", StringComparison.Ordinal) ? "offer an exchange"
                : normalized.StartsWith("trade_accept:", StringComparison.Ordinal) ? "accept an exchange" : "decline an exchange";
        }
        if (normalized.StartsWith("build:recipe:", StringComparison.Ordinal) ||
            normalized.StartsWith("build:building:", StringComparison.Ordinal))
        {
            var localId = normalized[(Math.Max(normalized.LastIndexOf('/'), normalized.LastIndexOf(':')) + 1)..];
            var versionSeparator = localId.IndexOf('@');
            if (versionSeparator >= 0)
            {
                localId = localId[..versionSeparator];
            }
            return $"build {HumanizeIdentifier(localId)}";
        }

        var known = normalized switch
        {
            "safe_idle" => "take it easy",
            "seek_food" => "find food",
            "seek_rest" => "rest",
            "eat_food" => "eat",
            "consume_food" => "eat",
            "collect_shared_food" => "collect food from camp",
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
