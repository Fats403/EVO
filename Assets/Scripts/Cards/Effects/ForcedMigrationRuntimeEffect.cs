using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime effect for "Forced Migration": swaps the board positions of two
/// targeted creatures with a short, smooth animation.
/// </summary>
[CreateAssetMenu(menuName = "Effects/Forced Migration")]
public class ForcedMigrationRuntimeEffect : RuntimeEffectBase
{
    [Tooltip("Duration of the swap animation in seconds.")]
    public float swapDuration = 0.45f;

    public override void Apply(List<Creature> targets, SlotOwner owner, ResolutionManager rm)
    {
        if (targets == null || targets.Count < 2 || rm == null)
            return;

        // Take the first two distinct targets.
        Creature a = targets[0];
        Creature b = null;
        for (int i = 1; i < targets.Count; i++)
        {
            if (targets[i] != null && targets[i] != a)
            {
                b = targets[i];
                break;
            }
        }

        if (a == null || b == null)
            return;
        if (a == b)
            return;

        // Respect immovable flag on the creature card: do not move creatures that are marked immovable.
        if ((a != null && a.IsImmovable) || (b != null && b.IsImmovable))
            return;

        if (a.currentHealth <= 0 || b.currentHealth <= 0 || a.isDying || b.isDying)
            return;

        BoardSlot slotA = BoardUtils.GetSlotOf(a);
        BoardSlot slotB = BoardUtils.GetSlotOf(b);
        if (slotA == null || slotB == null)
            return;

        rm.StartCoroutine(SwapRoutine(a, b, slotA, slotB, swapDuration));
    }

    private IEnumerator SwapRoutine(
        Creature a,
        Creature b,
        BoardSlot slotA,
        BoardSlot slotB,
        float duration
    )
    {
        if (a == null || b == null || slotA == null || slotB == null)
            yield break;

        Vector3 startPosA = a.transform.position;
        Vector3 startPosB = b.transform.position;
        Vector3 endPosA = slotB.transform.position;
        Vector3 endPosB = slotA.transform.position;

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // Smooth in/out curve.
            float eased = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI);

            a.transform.position = Vector3.Lerp(startPosA, endPosA, eased);
            b.transform.position = Vector3.Lerp(startPosB, endPosB, eased);
            yield return null;
        }

        a.transform.position = endPosA;
        b.transform.position = endPosB;

        // Swap the slot assignments atomically.
        if (slotA.currentCreature == a)
            slotA.currentCreature = b;
        if (slotB.currentCreature == b)
            slotB.currentCreature = a;
    }
}
