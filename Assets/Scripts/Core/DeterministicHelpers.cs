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
    /// Gets the slot index for a creature. Returns int.MaxValue if not on a slot.
    /// Use this for deterministic tie-breaking in sorting operations.
    /// </summary>
    public static int GetSlotIndex(Creature c)
    {
        if (c == null)
            return int.MaxValue;

        var lookup = GetSlotIndexLookup();
        foreach (var kvp in lookup)
        {
            if (kvp.Key.currentCreature == c)
                return kvp.Value;
        }
        return int.MaxValue;
    }

    /// <summary>
    /// Gets the slot index for a creature using a provided lookup (for performance in loops).
    /// </summary>
    public static int GetSlotIndex(Creature c, Dictionary<BoardSlot, int> slotLookup)
    {
        if (c == null || slotLookup == null)
            return int.MaxValue;

        foreach (var kvp in slotLookup)
        {
            if (kvp.Key.currentCreature == c)
                return kvp.Value;
        }
        return int.MaxValue;
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
    /// Orders creatures by a primary key with slot index as a deterministic tie-breaker.
    /// Use this instead of OrderBy(...) when the primary key might have ties.
    /// </summary>
    public static IOrderedEnumerable<Creature> OrderByWithSlotTieBreaker<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
    {
        var lookup = GetSlotIndexLookup();
        return source.OrderBy(keySelector).ThenBy(c => GetSlotIndex(c, lookup));
    }

    /// <summary>
    /// Orders creatures by a primary key (descending) with slot index as a tie-breaker.
    /// </summary>
    public static IOrderedEnumerable<Creature> OrderByDescendingWithSlotTieBreaker<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
    {
        var lookup = GetSlotIndexLookup();
        return source.OrderByDescending(keySelector).ThenBy(c => GetSlotIndex(c, lookup));
    }

    /// <summary>
    /// Orders creatures by distance from a point with slot index as a tie-breaker.
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
            .ThenBy(c => GetSlotIndex(c, lookup))
            .ToList();
    }

    /// <summary>
    /// Finds the creature with the lowest value of a property, with deterministic tie-breaking.
    /// Returns null if the source is empty.
    /// </summary>
    public static Creature FindMinBy<TKey>(
        IEnumerable<Creature> source,
        Func<Creature, TKey> keySelector
    )
        where TKey : IComparable<TKey>
    {
        var lookup = GetSlotIndexLookup();
        return source.OrderBy(keySelector).ThenBy(c => GetSlotIndex(c, lookup)).FirstOrDefault();
    }

    /// <summary>
    /// Finds the creature with the highest value of a property, with deterministic tie-breaking.
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
            .ThenBy(c => GetSlotIndex(c, lookup))
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
    /// Returns all BoardSlots in deterministic order (sorted by slot index).
    /// This is the safest way to iterate over slots when order matters.
    /// </summary>
    public static List<BoardSlot> GetAllSlotsSorted()
    {
        var lookup = GetSlotIndexLookup();
        return lookup.Keys.Where(s => s != null).OrderBy(s => lookup[s]).ToList();
    }

    /// <summary>
    /// Returns slots for a specific owner in deterministic order (sorted by slot index).
    /// </summary>
    public static List<BoardSlot> GetSlotsForOwnerSorted(SlotOwner owner)
    {
        var lookup = GetSlotIndexLookup();
        return lookup
            .Keys.Where(s => s != null && s.owner == owner)
            .OrderBy(s => lookup[s])
            .ToList();
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
        // This ensures that creatures at "effectively the same distance" are compared by slot index.
        float invPrecision = 1f / Mathf.Max(0.001f, distancePrecision);
        return source
            .OrderBy(c =>
            {
                float sqrDist = Vector3.SqrMagnitude(c.transform.position - origin);
                // Round to nearest bucket to eliminate floating-point noise
                return Mathf.Round(sqrDist * invPrecision);
            })
            .ThenBy(c => GetSlotIndex(c, lookup))
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
