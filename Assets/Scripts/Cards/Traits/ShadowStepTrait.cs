using System.Collections;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Shadow Step")]
public class ShadowStepTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (self.IsImmovable)
            return;

        // Collect other living, non-dying, non-immovable allies.
        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c =>
                c != null
                && c != self
                && c.owner == self.owner
                && c.currentHealth > 0
                && !c.isDying
                && !c.IsImmovable
            )
            .ToList();
        if (allies.Count == 0)
            return;

        int minHp = allies.Min(c => c.currentHealth);
        var lowest = allies.Where(c => c.currentHealth == minHp).ToList();
        if (lowest.Count == 0)
            return;

        int index = 0;
        if (GameManager.Instance != null)
            index = GameManager.Instance.NextRandomInt(0, lowest.Count);
        else
            index = Random.Range(0, lowest.Count);

        var target = lowest[index];
        if (target == null)
            return;

        var slotSelf = BoardUtils.GetSlotOf(self);
        var slotTarget = BoardUtils.GetSlotOf(target);
        if (slotSelf == null || slotTarget == null)
            return;

        const float duration = 0.45f;
        ResolutionManager.Instance.StartCoroutine(
            SwapRoutine(self, target, slotSelf, slotTarget, duration)
        );
    }

    private static IEnumerator SwapRoutine(
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

        // Swap the slot assignments atomically.
        if (slotA != null && slotA.currentCreature == a)
            slotA.currentCreature = b;
        if (slotB != null && slotB.currentCreature == b)
            slotB.currentCreature = a;
    }
}
