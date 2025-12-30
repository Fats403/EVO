using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Bloodthirsty")]
public class BloodthirstyTrait : Trait
{
    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        // Use per-creature flag instead of static HashSet for determinism
        if (self.traitUsedBloodthirsty)
            return;
        if (ResolutionManager.Instance == null)
            return;
        var next = ResolutionManager.Instance.FindBestTarget(self);
        if (next == null)
            return;
        // If the killed target is already gone, we just attack the next best available
        if (next == target)
            return; // avoid pointless call if somehow still same reference

        ResolutionManager.Instance.PerformImmediateAttack(
            self,
            next,
            ignoreBodyRules: false,
            onComplete: (success) =>
            {
                if (success)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Bloodthirsty",
                        self.transform.position,
                        GameColorPalette.TextWarning
                    );
                }
            }
        );
        self.traitUsedBloodthirsty = true;
    }

    // Per-creature flag is reset in Creature.ResetRoundBookkeeping() at round start
}
