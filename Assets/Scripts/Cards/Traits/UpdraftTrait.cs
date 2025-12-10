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
            BoardMovement.MoveCreatureToSlot(enemy, fromSlot, toSlot, moveDuration)
        );
    }
}
