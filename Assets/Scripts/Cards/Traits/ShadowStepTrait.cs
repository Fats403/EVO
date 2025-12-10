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
            BoardMovement.SwapCreatures(self, target, slotSelf, slotTarget, duration)
        );
    }
}
