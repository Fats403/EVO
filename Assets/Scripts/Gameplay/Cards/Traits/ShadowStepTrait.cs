using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Shadow Step")]
public class ShadowStepTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (self.IsImmovable)
            return;

        // Use centralized helpers for deterministic ally selection
        var allies = DeterministicHelpers.GetCreaturesSorted(c =>
            c != self && c.owner == self.owner && !c.IsImmovable
        );
        if (allies.Count == 0)
            return;

        // Find lowest HP ally deterministically (slot index tie-breaker built-in)
        var target = DeterministicHelpers.FindMinBy(allies, c => c.currentHealth);
        if (target == null)
            return;

        var slotSelf = BoardUtils.GetSlotOf(self);
        var slotTarget = BoardUtils.GetSlotOf(target);
        if (slotSelf == null || slotTarget == null)
            return;

        const float duration = 0.45f;

        ResolutionManager.Instance?.EnqueueStartOfRoundAnimation(
            BoardMovement.SwapCreatures(self, target, slotSelf, slotTarget, duration)
        );

        FeedbackManager.Instance?.ShowFloatingText(
            "Shadow Step",
            self.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
