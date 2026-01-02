using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Centralized helpers for deterministic operations in networked games.
///
/// CRITICAL: All game state operations that involve collections of creatures,
/// random selection, or iteration order MUST use these helpers to ensure
/// identical behavior across networked clients.
///
/// Common pitfalls these helpers prevent:
/// 1. FindObjectsByType returns arbitrary order - use GetAllCreaturesSorted()
/// 2. LINQ ThenBy(_ => Rand()) is broken - use SelectRandomDeterministic()
/// 3. Distance-based selection without tie-breaker - use OrderByWithSlotTieBreaker()
/// </summary>
public static class DeterministicHelpers
{
    // Cached slot index lookup - rebuilt when needed
    private static Dictionary<BoardSlot, int> _cachedSlotIndices;
    private static int _lastSlotCacheFrame = -1;

    /// <summary>
    /// Gets or rebuilds the slot index lookup dictionary.
    /// Cached per frame for performance.
    /// </summary>
    public static Dictionary<BoardSlot, int> GetSlotIndexLookup()
    {
        if (_cachedSlotIndices == null || _lastSlotCacheFrame != Time.frameCount)
        {
            _cachedSlotIndices = UnityEngine
                .Object.FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
                .ToDictionary(s => s, s => s.index);
            _lastSlotCacheFrame = Time.frameCount;
        }
        return _cachedSlotIndices;
    }

    /// <summary>
    /// Gets the logical slot index for a creature from the current board state.
    /// Returns int.MaxValue if not on a slot. In networked games, this value
    /// is normalised into the host's canonical index space so that both peers
    /// use identical indices for ordering and checksums, even though the guest
    /// may mirror indices locally for UI/placement.
    /// </summary>
    public static int GetSlotIndex(Creature c)
    {
        if (c == null)
            return int.MaxValue;

        var lookup = GetSlotIndexLookup();
        return GetSlotIndex(c, lookup);
    }

    /// <summary>
    /// Gets the logical slot index for a creature using a provided lookup
    /// (for performance in loops). In networked games, this value is
    /// normalised into the host's canonical index space. For non-networked
    /// games, the local BoardSlot.index is returned as-is.
    /// </summary>
    public static int GetSlotIndex(Creature c, Dictionary<BoardSlot, int> slotLookup)
    {
        if (c == null || slotLookup == null)
            return int.MaxValue;

        foreach (var kvp in slotLookup)
        {
            if (kvp.Key.currentCreature == c)
            {
                int localIndex = kvp.Value;

                // In AI / offline games, local indices are canonical.
                if (!NetworkSessionStore.IsNetworkedGame)
                    return localIndex;

                // In networked games, the host's local index IS canonical.
                // The guest mirrors indices for UI/placement, so we invert
                // that mirror here to recover the host's canonical index.
                return NetworkRoleHelper.IsGuest
                    ? NetworkRoleHelper.MirrorSlotIndex(localIndex)
                    : localIndex;
            }
        }
        return int.MaxValue;
    }

    /// <summary>
    /// Returns a fair tie-break value for a creature that alternates priority
    /// between players each round. On even rounds, host (slots 0-4) wins ties.
    /// On odd rounds, guest (slots 5-9) wins ties.
    ///
    /// This ensures neither player has a persistent tie-break advantage.
    /// </summary>
    public static int GetFairTieBreakValue(Creature c, Dictionary<BoardSlot, int> slotLookup)
    {
        int canonicalIndex = GetSlotIndex(c, slotLookup);
        if (canonicalIndex == int.MaxValue)
            return int.MaxValue;

        // Get current round number (default to 1 if GameManager unavailable)
        int round = GameManager.Instance != null ? GameManager.Instance.currentRound : 1;

        // On even rounds: use canonical index as-is (host advantage: 0-4 < 5-9)
        // On odd rounds: invert so guest has advantage (9-index: guest 5-9 → 4-0, host 0-4 → 9-5)
        if (round % 2 == 0)
        {
            return canonicalIndex;
        }
        else
        {
            // Invert: 9 - index makes guest's 5→4, 6→3, etc. (lower = wins tie)
            // and host's 0→9, 1→8, etc. (higher = loses tie)
            return 9 - canonicalIndex;
        }
    }

    /// <summary>
    /// Overload using current slot lookup cache.
    /// </summary>
    public static int GetFairTieBreakValue(Creature c)
    {
        return GetFairTieBreakValue(c, GetSlotIndexLookup());
    }

    /// <summary>
    /// Returns all living creatures sorted by slot index for deterministic iteration.
    /// This is the safest way to iterate over creatures when order matters.
    /// </summary>
    public static List<Creature> GetAllCreaturesSorted()
    {
        var lookup = GetSlotIndexLookup();
        return UnityEngine
            .Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .OrderBy(c => GetSlotIndex(c, lookup))
            .ToList();
    }

    /// <summary>
    /// Returns ALL creatures (including dying/dead) sorted by slot index.
    /// Use this for notification hooks that need to reach all creatures.
    /// </summary>
    public static List<Creature> GetAllCreaturesInSlotOrder()
    {
        var lookup = GetSlotIndexLookup();
        return UnityEngine
            .Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null)
            .OrderBy(c => GetSlotIndex(c, lookup))
            .ToList();
    }

    /// <summary>
    /// Returns creatures matching a filter, sorted by slot index.
    /// </summary>
    public static List<Creature> GetCreaturesSorted(Func<Creature, bool> filter)
    {
        var lookup = GetSlotIndexLookup();
        return UnityEngine
            .Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying && filter(c))
            .OrderBy(c => GetSlotIndex(c, lookup))
            .ToList();
    }

    /// <summary>
    /// Returns creatures for a specific owner, sorted by slot index.
    /// </summary>
    public static List<Creature> GetCreaturesForOwner(SlotOwner owner)
    {
        return GetCreaturesSorted(c => c.owner == owner);
    }

    /// <summary>
    /// Selects a random element from a list deterministically.
    /// The list MUST already be in deterministic order (e.g., from GetCreaturesSorted).
    ///
    /// CRITICAL: Do NOT use this on unsorted lists from FindObjectsByType!
    /// </summary>
    public static T SelectRandomDeterministic<T>(IList<T> sortedList)
        where T : class
    {
        if (sortedList == null || sortedList.Count == 0)
            return null;

        if (sortedList.Count == 1)
            return sortedList[0];

        int index = NextRandomInt(0, sortedList.Count);
        return sortedList[index];
    }

    /// <summary>
    /// Alias for SelectRandomDeterministic for more readable code.
    /// The list MUST already be in deterministic order!
    /// </summary>
    public static T PickRandom<T>(IList<T> sortedList)
        where T : class
    {
        return SelectRandomDeterministic(sortedList);
    }

    /// <summary>
    /// Selects a random element from a list matching a filter, deterministically.
    /// The source list MUST already be in deterministic order.
    /// </summary>
    public static T SelectRandomWhere<T>(IList<T> sortedList, Func<T, bool> filter)
        where T : class
    {
        if (sortedList == null || sortedList.Count == 0)
            return null;

        var filtered = sortedList.Where(filter).ToList();
        return SelectRandomDeterministic(filtered);
    }

    /// <summary>
    /// Orders creatures by a primary key with a fair tie-breaker that alternates
    /// priority between players each round. Use this instead of OrderBy(...) when
    /// the primary key might have ties.
    /// </summary>
    public static IOrderedEnumerable<Creature> OrderByWithSlotTieBreaker<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
    {
        var lookup = GetSlotIndexLookup();
        return source.OrderBy(keySelector).ThenBy(c => GetFairTieBreakValue(c, lookup));
    }

    /// <summary>
    /// Orders creatures by a primary key (descending) with a fair tie-breaker that
    /// alternates priority between players each round.
    /// </summary>
    public static IOrderedEnumerable<Creature> OrderByDescendingWithSlotTieBreaker<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
    {
        var lookup = GetSlotIndexLookup();
        return source.OrderByDescending(keySelector).ThenBy(c => GetFairTieBreakValue(c, lookup));
    }

    /// <summary>
    /// Orders creatures by distance from a point with a fair tie-breaker that
    /// alternates priority between players each round.
    /// Use this for target selection based on proximity.
    /// </summary>
    public static List<Creature> OrderByDistanceWithTieBreaker(
        IEnumerable<Creature> source,
        Vector3 origin
    )
    {
        var lookup = GetSlotIndexLookup();
        return source
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - origin))
            .ThenBy(c => GetFairTieBreakValue(c, lookup))
            .ToList();
    }

    /// <summary>
    /// Finds the creature with the lowest value of a property, with a fair tie-breaker
    /// that alternates priority between players each round.
    /// Returns null if the source is empty.
    /// </summary>
    public static Creature FindMinBy<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
        where TKey : IComparable<TKey>
    {
        var lookup = GetSlotIndexLookup();
        return source
            .OrderBy(keySelector)
            .ThenBy(c => GetFairTieBreakValue(c, lookup))
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds the creature with the highest value of a property, with a fair tie-breaker
    /// that alternates priority between players each round.
    /// Returns null if the source is empty.
    /// </summary>
    public static Creature FindMaxBy<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
        where TKey : IComparable<TKey>
    {
        var lookup = GetSlotIndexLookup();
        return source
            .OrderByDescending(keySelector)
            .ThenBy(c => GetFairTieBreakValue(c, lookup))
            .FirstOrDefault();
    }

    /// <summary>
    /// Wrapper for deterministic random int. Always use this instead of Random.Range
    /// for any game state decisions.
    /// </summary>
    public static int NextRandomInt(int minInclusive, int maxExclusive)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.NextRandomInt(minInclusive, maxExclusive);
        }

        Debug.LogWarning(
            "DeterministicHelpers: GameManager.Instance is null. Using fallback RNG - this may cause desync!"
        );
        return UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Shuffles a list in-place deterministically using Fisher-Yates algorithm.
    /// </summary>
    public static void ShuffleDeterministic<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextRandomInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Clears the slot index cache. Call this if slots are dynamically created/destroyed.
    /// Normally the cache auto-refreshes each frame.
    /// </summary>
    public static void InvalidateSlotCache()
    {
        _cachedSlotIndices = null;
        _lastSlotCacheFrame = -1;
    }

    /// <summary>
    /// Returns all BoardSlots in deterministic order (sorted by CANONICAL slot index).
    /// In networked games, the guest's local indices are mirrored, so we convert
    /// to canonical indices before sorting.
    /// </summary>
    public static List<BoardSlot> GetAllSlotsSorted()
    {
        var lookup = GetSlotIndexLookup();
        return lookup
            .Keys.Where(s => s != null)
            .OrderBy(s => GetCanonicalSlotIndexFromLocal(lookup[s]))
            .ToList();
    }

    /// <summary>
    /// Returns slots for a specific owner in deterministic order (sorted by CANONICAL slot index).
    /// In networked games, the guest's local indices are mirrored, so we convert
    /// to canonical indices before sorting.
    /// </summary>
    public static List<BoardSlot> GetSlotsForOwnerSorted(SlotOwner owner)
    {
        var lookup = GetSlotIndexLookup();
        return lookup
            .Keys.Where(s => s != null && s.owner == owner)
            .OrderBy(s => GetCanonicalSlotIndexFromLocal(lookup[s]))
            .ToList();
    }

    /// <summary>
    /// Converts a local slot index to the canonical (host) slot index.
    /// The guest's local indices are mirrored, so we invert them.
    /// For non-networked games or on the host, returns the index unchanged.
    /// </summary>
    private static int GetCanonicalSlotIndexFromLocal(int localIndex)
    {
        if (NetworkSessionStore.IsNetworkedGame && NetworkRoleHelper.IsGuest)
            return NetworkRoleHelper.MirrorSlotIndex(localIndex);
        return localIndex;
    }

    /// <summary>
    /// Orders creatures by distance from a point with discretized distance buckets to avoid
    /// floating-point precision issues, then by slot index as a final tie-breaker.
    /// The distance is discretized to 0.01 unit increments to ensure consistent ordering
    /// even when positions have minor floating-point differences between clients.
    /// </summary>
    public static List<Creature> OrderByDistanceWithTieBreaker(
        IEnumerable<Creature> source,
        Vector3 origin,
        float distancePrecision
    )
    {
        var lookup = GetSlotIndexLookup();
        // Discretize distances to avoid floating-point precision differences between clients.
        // This ensures that creatures at "effectively the same distance" are compared fairly.
        float invPrecision = 1f / Mathf.Max(0.001f, distancePrecision);
        return source
            .OrderBy(c =>
            {
                float sqrDist = Vector3.SqrMagnitude(c.transform.position - origin);
                // Round to nearest bucket to eliminate floating-point noise
                return Mathf.Round(sqrDist * invPrecision);
            })
            .ThenBy(c => GetFairTieBreakValue(c, lookup))
            .ToList();
    }

    /// <summary>
    /// Validates that two game states have the same creature ordering by comparing
    /// slot indices. Returns true if orderings match, false otherwise.
    /// Useful for debugging desync issues.
    /// </summary>
    public static bool ValidateCreatureOrdering(List<Creature> list1, List<Creature> list2)
    {
        if (list1 == null || list2 == null)
            return list1 == list2;
        if (list1.Count != list2.Count)
            return false;

        var lookup = GetSlotIndexLookup();
        for (int i = 0; i < list1.Count; i++)
        {
            int idx1 = GetSlotIndex(list1[i], lookup);
            int idx2 = GetSlotIndex(list2[i], lookup);
            if (idx1 != idx2)
                return false;
        }
        return true;
    }
}
