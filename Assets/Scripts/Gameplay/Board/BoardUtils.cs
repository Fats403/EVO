using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BoardUtils
{
    /// <summary>
    /// Returns the BoardSlot that currently contains the creature (or null).
    /// Uses the cached slot lookup from DeterministicHelpers for consistency.
    /// </summary>
    public static BoardSlot GetSlotOf(Creature c)
    {
        if (c == null)
            return null;

        // Use the deterministic slot index lookup for consistent behavior
        var slotLookup = DeterministicHelpers.GetSlotIndexLookup();
        foreach (var kvp in slotLookup)
        {
            if (kvp.Key != null && kvp.Key.currentCreature == c)
                return kvp.Key;
        }
        return null;
    }

    public static IEnumerable<Creature> GetAdjacentAllies(Creature c)
    {
        var result = new List<Creature>();
        if (c == null)
            return result;

        var mySlot = GetSlotOf(c);
        if (mySlot == null)
            return result;

        // Use ALL slots for that owner (including empty ones), ordered on X.
        var allSlotsForOwner = GetSlotsForOwner(mySlot.owner, occupiedOnly: false);
        if (allSlotsForOwner == null || allSlotsForOwner.Count == 0)
            return result;

        int idx = allSlotsForOwner.FindIndex(s => s == mySlot);
        if (idx < 0)
            return result;

        void AddNeighborAtIndex(int i)
        {
            if (i < 0 || i >= allSlotsForOwner.Count)
                return;

            var neighborSlot = allSlotsForOwner[i];
            if (
                neighborSlot == null
                || !neighborSlot.occupied
                || neighborSlot.currentCreature == null
            )
                return;

            result.Add(neighborSlot.currentCreature);
        }

        // Only directly adjacent slots; gaps will naturally break adjacency.
        AddNeighborAtIndex(idx - 1); // left
        AddNeighborAtIndex(idx + 1); // right

        return result;
    }

    // Closest living enemy by world-space distance (deterministic tie-breaking by slot index)
    public static Creature GetClosestEnemy(Creature c)
    {
        if (c == null)
            return null;
        var enemies = DeterministicHelpers.GetCreaturesSorted(x => x.owner != c.owner);
        if (enemies.Count == 0)
            return null;
        return DeterministicHelpers
            .OrderByDistanceWithTieBreaker(enemies, c.transform.position)
            .FirstOrDefault();
    }

    /// <summary>
    /// All board slots for a given owner, optionally only those currently occupied.
    /// Returns slots in deterministic order (by CANONICAL slot index).
    /// In networked games, the guest's local indices are mirrored, so we must
    /// convert to canonical (host) indices before sorting to ensure both clients
    /// get the same ordering.
    /// </summary>
    public static List<BoardSlot> GetSlotsForOwner(SlotOwner owner, bool occupiedOnly)
    {
        // Start with the deterministically sorted slots for this owner
        var slots = DeterministicHelpers.GetSlotsForOwnerSorted(owner);
        if (occupiedOnly)
        {
            slots = slots.Where(s => s.occupied && s.currentCreature != null).ToList();
        }
        return slots;
    }

    // Returns the slot closest to the horizontal center among a side's slots.
    // If requireOccupied is true, only considers occupied slots.
    public static BoardSlot GetCenterSlot(SlotOwner owner, bool requireOccupied)
    {
        var slots = GetSlotsForOwner(owner, occupiedOnly: requireOccupied);
        if (slots == null || slots.Count == 0)
            return null;

        float minX = slots.First().transform.position.x;
        float maxX = slots.Last().transform.position.x;
        float midX = (minX + maxX) * 0.5f;

        return slots.OrderBy(s => Mathf.Abs(s.transform.position.x - midX)).FirstOrDefault();
    }

    // Returns a random empty slot on the given owner's side, or null if none.
    public static BoardSlot GetRandomEmptySlot(SlotOwner owner)
    {
        var emptySlots = GetSlotsForOwner(owner, occupiedOnly: false)
            .Where(s => s != null && !s.occupied)
            .ToList();
        if (emptySlots.Count == 0)
            return null;
        int idx = 0;
        if (GameManager.Instance != null)
        {
            idx = GameManager.Instance.NextRandomInt(0, emptySlots.Count);
        }
        else
        {
            Debug.LogWarning(
                "BoardUtils: GameManager.Instance is null during GetRandomEmptySlot. Determinism may be compromised."
            );
            idx = Random.Range(0, emptySlots.Count);
        }
        return emptySlots[idx];
    }
}
