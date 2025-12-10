using System.Collections;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Updraft")]
public class UpdraftTrait : Trait
{
    public float moveDuration = 0.45f;

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (ResolutionManager.Instance == null)
            return;

        // Find nearest enemy that is not immovable.
        var enemy = BoardUtils.GetClosestEnemy(self);
        if (enemy == null || enemy.IsImmovable)
            return;

        var fromSlot = BoardUtils.GetSlotOf(enemy);
        if (fromSlot == null)
            return;

        // Find random empty slot on the enemy's side.
        var toSlot = BoardUtils.GetRandomEmptySlot(fromSlot.owner);
        if (toSlot == null)
            return;

        FeedbackManager.Instance?.ShowFloatingText("Updraft", enemy.transform.position, Color.cyan);

        ResolutionManager.Instance.StartCoroutine(
            MoveToSlotRoutine(enemy, fromSlot, toSlot, moveDuration)
        );
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

        // Update slots: vacate old and occupy new.
        if (from.currentCreature == c)
            from.Vacate();
        to.Occupy(c);
    }
}
