using System.Collections;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Territorial Push")]
public class TerritorialPushEffect : EffectTraitBase
{
    public float moveDuration = 0.45f;

    public override void OnApply(Creature target)
    {
        if (target == null)
            return;
        var slot = BoardUtils.GetSlotOf(target);
        if (slot == null)
            return;

        var slots = BoardUtils.GetSlotsForOwner(slot.owner, occupiedOnly: false);
        if (slots == null || slots.Count == 0)
            return;

        int idx = slots.FindIndex(s => s == slot);
        if (idx < 0)
            return;

        // Determine push direction: nearest edge (left or right). If exactly center, random.
        int centerIdx = slots.Count / 2;
        bool pushLeft;
        if (idx < centerIdx)
            pushLeft = true;
        else if (idx > centerIdx)
            pushLeft = false;
        else
        {
            int roll =
                GameManager.Instance != null
                    ? GameManager.Instance.NextRandomInt(0, 2)
                    : Random.Range(0, 2);
            pushLeft = roll == 0;
        }

        BoardSlot dest = null;
        if (pushLeft)
        {
            // Closest empty slot toward left edge.
            for (int i = 0; i < idx; i++)
            {
                if (slots[i] != null && !slots[i].occupied)
                {
                    dest = slots[i];
                    break;
                }
            }
        }
        else
        {
            // Closest empty slot toward right edge.
            for (int i = slots.Count - 1; i > idx; i--)
            {
                if (slots[i] != null && !slots[i].occupied)
                {
                    dest = slots[i];
                    break;
                }
            }
        }

        if (dest == null)
            return;

        ResolutionManager.Instance.StartCoroutine(
            MoveToSlotRoutine(target, slot, dest, moveDuration)
        );

        // Apply Fatigued (2) to the pushed enemy.
        target.AddStatus(StatusTag.Fatigued, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigued +2",
            target.transform.position,
            Color.yellow
        );

        remainingRounds = 0;
    }

    private static IEnumerator MoveToSlotRoutine(
        Creature c,
        BoardSlot from,
        BoardSlot to,
        float duration
    )
    {
        if (c == null || from == null || to == null)
            yield break;

        Vector3 startPos = c.transform.position;
        Vector3 endPos = to.transform.position;

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI);
            c.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        c.transform.position = endPos;

        if (from.currentCreature == c)
            from.Vacate();
        to.Occupy(c);
    }
}
