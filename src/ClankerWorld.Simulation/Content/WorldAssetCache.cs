namespace ClankerWorld.Simulation.Content;

/// <summary>
/// The rebuildable renderer-cache identity. It contains only the normalized
/// asset digest and decode profile; no file path, byte buffer, or host handle
/// crosses the simulation boundary.
/// </summary>
public sealed record WorldAssetCacheKey(
    string NormalizedDigest,
    string DecodeProfile);

public sealed record WorldAssetCacheEntry(
    WorldAssetCacheKey Key,
    long DecodedCacheBytes,
    long GpuBytes,
    long LastUsedTick,
    long? PinnedUntilTick);

public sealed record WorldAssetCacheState(
    IReadOnlyList<WorldAssetCacheEntry> Entries);

public sealed record WorldAssetCacheTotals(
    int EntryCount,
    long DecodedCacheBytes,
    long GpuBytes);

public sealed record WorldAssetCacheResult(
    bool IsSuccess,
    WorldAssetCacheTotals Totals,
    IReadOnlyList<WorldAssetCacheKey> Evicted,
    string? FailureCode,
    string? Diagnostic)
{
    public static WorldAssetCacheResult Success(
        WorldAssetCacheTotals totals,
        IReadOnlyList<WorldAssetCacheKey> evicted) =>
        new(true, totals, evicted, null, null);

    public static WorldAssetCacheResult Failure(
        WorldAssetCacheTotals totals,
        string failureCode,
        string diagnostic) =>
        new(false, totals, [], failureCode, diagnostic);
}

/// <summary>
/// Deterministic, value-only residency accounting for a renderer cache. It
/// enforces the same world decoded-cache and GPU ceilings as activation,
/// evicts by the documented LRU key, protects current-frame pins, and applies
/// no partial mutation when a candidate cannot fit.
/// </summary>
public sealed class WorldAssetCacheLedger
{
    private readonly WorldAssetReservationPolicy policy;
    private readonly Dictionary<WorldAssetCacheKey, WorldAssetCacheEntry> entries = [];

    public WorldAssetCacheLedger(WorldAssetReservationPolicy? policy = null)
    {
        this.policy = policy ?? WorldAssetReservationPolicy.Default;
        this.policy.Validate();
    }

    public WorldAssetReservationPolicy Policy => policy;

    public WorldAssetCacheState ExportState() => new(
        entries.Values
            .OrderBy(item => item.Key.NormalizedDigest, StringComparer.Ordinal)
            .ThenBy(item => item.Key.DecodeProfile, StringComparer.Ordinal)
            .ToArray());

    public static WorldAssetCacheLedger Restore(
        WorldAssetCacheState? state,
        WorldAssetReservationPolicy? policy = null)
    {
        var ledger = new WorldAssetCacheLedger(policy);
        if (state is null)
        {
            return ledger;
        }

        ArgumentNullException.ThrowIfNull(state.Entries);
        var previousKey = (NormalizedDigest: string.Empty, DecodeProfile: string.Empty);
        foreach (var entry in state.Entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ValidateEntry(entry);
            var currentKey = (entry.Key.NormalizedDigest, entry.Key.DecodeProfile);
            if (CompareKey(currentKey, previousKey) <= 0)
            {
                throw new InvalidDataException("The world asset cache entries are not in canonical order.");
            }

            previousKey = currentKey;
            if (!ledger.entries.TryAdd(entry.Key, entry))
            {
                throw new InvalidDataException("The world asset cache contains duplicate entries.");
            }
        }

        ledger.Validate();
        return ledger;
    }

    public WorldAssetCacheResult TryEnsureResident(
        WorldAssetCacheKey key,
        long decodedCacheBytes,
        long gpuBytes,
        long worldTick,
        bool pinForFrame = false)
    {
        ArgumentNullException.ThrowIfNull(key);
        var currentTotals = Totals;
        try
        {
            ValidateKey(key);
            ArgumentOutOfRangeException.ThrowIfNegative(decodedCacheBytes);
            ArgumentOutOfRangeException.ThrowIfNegative(gpuBytes);
            ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
            if (decodedCacheBytes == 0 || gpuBytes == 0)
            {
                return WorldAssetCacheResult.Failure(
                    currentTotals,
                    "cache_charge_invalid",
                    "A resident asset must have positive decoded-cache and GPU charges.");
            }
        }
        catch (ArgumentException exception)
        {
            return WorldAssetCacheResult.Failure(currentTotals, "cache_request_invalid", exception.Message);
        }

        if (decodedCacheBytes > policy.MaxDecodedCacheBytes || gpuBytes > policy.MaxGpuBytes)
        {
            return WorldAssetCacheResult.Failure(
                currentTotals,
                "world_cache_breach",
                "The requested resident asset is larger than a configured world cache ceiling.");
        }

        if (entries.TryGetValue(key, out var existing))
        {
            if (existing.DecodedCacheBytes != decodedCacheBytes || existing.GpuBytes != gpuBytes)
            {
                return WorldAssetCacheResult.Failure(
                    currentTotals,
                    "cache_charge_mismatch",
                    "A cache key cannot change its canonical decoded-cache or GPU charge.");
            }

            if (existing.LastUsedTick > worldTick)
            {
                return WorldAssetCacheResult.Failure(
                    currentTotals,
                    "cache_time_regression",
                    "Cache use cannot move backward in the authoritative world clock.");
            }
        }

        long? pinnedUntilTick = pinForFrame
            ? worldTick
            : existing?.PinnedUntilTick is { } existingPin && existingPin >= worldTick
                ? existingPin
                : null;
        var candidate = new WorldAssetCacheEntry(
            key,
            decodedCacheBytes,
            gpuBytes,
            worldTick,
            pinnedUntilTick);
        var working = entries.ToDictionary(item => item.Key, item => item.Value);
        working[key] = candidate;
        var evicted = new List<WorldAssetCacheKey>();
        var totals = SumTotals(working.Values);
        foreach (var victim in working.Values
                     .Where(item => item.Key != key && !IsPinnedAt(item, worldTick))
                     .OrderBy(item => item.LastUsedTick)
                     .ThenBy(item => item.Key.NormalizedDigest, StringComparer.Ordinal)
                     .ThenBy(item => item.Key.DecodeProfile, StringComparer.Ordinal)
                     .ToArray())
        {
            if (WithinPolicy(totals))
            {
                break;
            }

            working.Remove(victim.Key);
            evicted.Add(victim.Key);
            totals = SumTotals(working.Values);
        }

        if (!WithinPolicy(totals))
        {
            return WorldAssetCacheResult.Failure(
                currentTotals,
                "world_cache_breach",
                "The requested resident asset cannot fit without evicting a pinned or current-frame entry.");
        }

        entries.Clear();
        foreach (var entry in working)
        {
            entries.Add(entry.Key, entry.Value);
        }

        return WorldAssetCacheResult.Success(totals, evicted);
    }

    public int ClearExpiredPins(long worldTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        var cleared = 0;
        foreach (var entry in entries.Values.ToArray())
        {
            if (entry.PinnedUntilTick is not { } pinnedUntilTick || pinnedUntilTick >= worldTick)
            {
                continue;
            }

            entries[entry.Key] = entry with { PinnedUntilTick = null };
            cleared++;
        }

        return cleared;
    }

    public WorldAssetCacheTotals Totals => SumTotals(entries.Values);

    public void Validate()
    {
        policy.Validate();
        foreach (var entry in entries.Values)
        {
            ValidateEntry(entry);
        }

        if (!WithinPolicy(Totals))
        {
            throw new InvalidDataException("The world asset cache exceeds its configured policy.");
        }
    }

    private bool WithinPolicy(WorldAssetCacheTotals totals) =>
        totals.DecodedCacheBytes <= policy.MaxDecodedCacheBytes &&
        totals.GpuBytes <= policy.MaxGpuBytes;

    private static WorldAssetCacheTotals SumTotals(IEnumerable<WorldAssetCacheEntry> values)
    {
        var materialized = values.ToArray();
        return new WorldAssetCacheTotals(
            materialized.Length,
            checked(materialized.Sum(item => item.DecodedCacheBytes)),
            checked(materialized.Sum(item => item.GpuBytes)));
    }

    private static void ValidateEntry(WorldAssetCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry.Key);
        ValidateKey(entry.Key);
        if (entry.DecodedCacheBytes <= 0 || entry.GpuBytes <= 0 || entry.LastUsedTick < 0 ||
            entry.PinnedUntilTick is < 0 ||
            entry.PinnedUntilTick is { } pinnedUntilTick && pinnedUntilTick < entry.LastUsedTick)
        {
            throw new InvalidDataException("A world asset cache entry contains an invalid charge or tick.");
        }
    }

    private static void ValidateKey(WorldAssetCacheKey key)
    {
        ContentPackageRules.ValidateDigest(key.NormalizedDigest, nameof(key.NormalizedDigest));
        ArgumentException.ThrowIfNullOrWhiteSpace(key.DecodeProfile);
        if (key.DecodeProfile != key.DecodeProfile.Trim() ||
            key.DecodeProfile.Any(char.IsWhiteSpace) ||
            key.DecodeProfile.Length > 64)
        {
            throw new ArgumentException("Asset decode profiles must be trimmed, bounded, and contain no whitespace.");
        }
    }

    private static bool IsPinnedAt(WorldAssetCacheEntry entry, long worldTick) =>
        entry.PinnedUntilTick is { } pinnedUntilTick && pinnedUntilTick >= worldTick;

    private static int CompareKey(
        (string NormalizedDigest, string DecodeProfile) left,
        (string NormalizedDigest, string DecodeProfile) right)
    {
        var digestComparison = string.CompareOrdinal(left.NormalizedDigest, right.NormalizedDigest);
        return digestComparison != 0
            ? digestComparison
            : string.CompareOrdinal(left.DecodeProfile, right.DecodeProfile);
    }
}
