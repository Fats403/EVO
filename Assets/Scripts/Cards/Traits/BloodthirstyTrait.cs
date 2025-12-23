using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Bloodthirsty")]
public class BloodthirstyTrait : Trait
{
    private static readonly HashSet<Creature> usedThisRound = new();

    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (usedThisRound.Contains(self))
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
        usedThisRound.Add(self);
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        // Reset per-round extra-attack flag for this creature.
        usedThisRound.Remove(self);
    }
}
