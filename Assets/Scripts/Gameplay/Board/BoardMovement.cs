using System.Collections;
using UnityEngine;

/// <summary>
/// Centralized helpers for moving creatures between board slots.
/// Handles smooth animations and keeps slot occupancy consistent
/// even if creatures die mid-move.
/// </summary>
public static class BoardMovement
{
    /// <summary>
    /// Smoothly swap the board positions of two creatures over the given duration.
    /// If either creature dies during the animation, the surviving creature is
    /// left in a sensible slot and no "ghost occupied" slots are left behind.
    /// </summary>
    public static IEnumerator SwapCreatures(
        Creature a,
        Creature b,
        BoardSlot slotA,
        BoardSlot slotB,
        float duration
    )
    {
        if (a == null || b == null || slotA == null || slotB == null)
            yield break;

        // Bring both creatures visually to the foreground while they move.
        SortingState stateA = SortingUtils.PushToForeground(a.transform);
        SortingState stateB = SortingUtils.PushToForeground(b.transform);

        Vector3 startPosA = a.transform.position;
        Vector3 startPosB = b.transform.position;
        Vector3 endPosA = slotB.transform.position;
        Vector3 endPosB = slotA.transform.position;

        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // Smooth in/out curve.
            float eased = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI);

            if (a != null)
                a.transform.position = Vector3.Lerp(startPosA, endPosA, eased);
            if (b != null)
                b.transform.position = Vector3.Lerp(startPosB, endPosB, eased);

            yield return null;
        }

        if (a != null)
            a.transform.position = endPosA;
        if (b != null)
            b.transform.position = endPosB;

        // Restore any temporary sorting overrides.
        SortingUtils.RestoreSorting(stateA);
        SortingUtils.RestoreSorting(stateB);

        // Check who is still alive before finalizing slot ownership.
        bool aAlive = a != null && !a.isDying && a.currentHealth > 0;
        bool bAlive = b != null && !b.isDying && b.currentHealth > 0;

        // Clear old ownership if it still points at these creatures.
        if (slotA.currentCreature == a)
            slotA.Vacate();
        if (slotB.currentCreature == b)
            slotB.Vacate();

        if (aAlive && bAlive)
        {
            // Normal swap: each ends up in the other's slot.
            slotA.Occupy(b);
            slotB.Occupy(a);
        }
        else if (aAlive && !bAlive)
        {
            // Only A survives – keep A in its original slot.
            slotA.Occupy(a);
        }
        else if (!aAlive && bAlive)
        {
            // Only B survives – keep B in its original slot.
            slotB.Occupy(b);
        }
        // If both died, both slots remain empty.

        // CRITICAL: Invalidate the slot cache after any movement so that subsequent
        // lookups (e.g., GetAdjacentAllies, GetSlotOf) return fresh data. This prevents
        // stale slot→creature mappings from causing duplicate or incorrect adjacency checks.
        DeterministicHelpers.InvalidateSlotCache();
    }

    /// <summary>
    /// Smoothly move a single creature from one slot to another.
    /// If the creature dies during the animation, no destination slot
    /// is claimed, and any previous slot reference is safely cleared.
    /// </summary>
    public static IEnumerator MoveCreatureToSlot(
        Creature c,
        BoardSlot from,
        BoardSlot to,
        float duration
    )
    {
        if (c == null || from == null || to == null)
            yield break;

        SortingState sortingState = SortingUtils.PushToForeground(c.transform);

        Vector3 startPos = c.transform.position;
        Vector3 endPos = to.transform.position;

        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI);

            if (c != null)
                c.transform.position = Vector3.Lerp(startPos, endPos, eased);

            yield return null;
        }

        if (c != null)
            c.transform.position = endPos;

        SortingUtils.RestoreSorting(sortingState);

        bool alive = c != null && !c.isDying && c.currentHealth > 0;

        if (from.currentCreature == c)
            from.Vacate();

        if (alive)
        {
            to.Occupy(c);
        }
        // If it died during the move, Kill() will already have vacated its last slot;
        // we simply don't re-occupy the destination.

        // CRITICAL: Invalidate the slot cache after any movement.
        DeterministicHelpers.InvalidateSlotCache();
    }
}
