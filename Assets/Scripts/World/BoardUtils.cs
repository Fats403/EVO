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

    // Adjacent allies: same owner, immediately left/right by x-order among occupied slots
    public static IEnumerable<Creature> GetAdjacentAllies(Creature c)
    {
        var result = new List<Creature>();
        if (c == null)
            return result;
        var mySlot = GetSlotOf(c);
        if (mySlot == null)
            return result;

        var sameOwnerSlots = Object
            .FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .Where(s =>
                s != null && s.owner == mySlot.owner && s.occupied && s.currentCreature != null
            )
            .OrderBy(s => s.transform.position.x)
            .ToList();

        int idx = sameOwnerSlots.FindIndex(s => s.currentCreature == c);
        if (idx < 0)
            return result;

        // left
        if (idx - 1 >= 0)
        {
            var left = sameOwnerSlots[idx - 1].currentCreature;
            if (left != null)
                result.Add(left);
        }
        // right
        if (idx + 1 < sameOwnerSlots.Count)
        {
            var right = sameOwnerSlots[idx + 1].currentCreature;
            if (right != null)
                result.Add(right);
        }
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
        int idx =
            GameManager.Instance != null
                ? GameManager.Instance.NextRandomInt(0, emptySlots.Count)
                : Random.Range(0, emptySlots.Count);
        return emptySlots[idx];
    }
}
