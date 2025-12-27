using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BoardUtils
{
    // Returns the BoardSlot that currently contains the creature (or null)
    public static BoardSlot GetSlotOf(Creature c)
    {
        if (c == null)
            return null;
        var slots = Object.FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
        foreach (var s in slots)
        {
            if (s != null && s.currentCreature == c)
                return s;
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

    // Closest living enemy by world-space distance
    public static Creature GetClosestEnemy(Creature c)
    {
        if (c == null)
            return null;
        var enemies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(x => x != null && x.currentHealth > 0 && !x.isDying && x.owner != c.owner)
            .ToList();
        if (enemies.Count == 0)
            return null;
        var pos = c.transform.position;
        return enemies
            .OrderBy(e => Vector3.SqrMagnitude(e.transform.position - pos))
            .FirstOrDefault();
    }

    // All board slots for a given owner, optionally only those currently occupied.
    public static List<BoardSlot> GetSlotsForOwner(SlotOwner owner, bool occupiedOnly)
    {
        var slots = Object
            .FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .Where(s => s != null && s.owner == owner);
        if (occupiedOnly)
        {
            slots = slots.Where(s => s.occupied && s.currentCreature != null);
        }
        return slots.OrderBy(s => s.transform.position.x).ToList();
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
            Debug.LogWarning("BoardUtils: GameManager.Instance is null during GetRandomEmptySlot. Determinism may be compromised.");
            idx = Random.Range(0, emptySlots.Count);
        }
        return emptySlots[idx];
    }
}
