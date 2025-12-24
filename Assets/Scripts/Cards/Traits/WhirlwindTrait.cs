using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Whirlwind")]
public class WhirlwindTrait : Trait
{
    private static readonly HashSet<Creature> usedThisRound = new HashSet<Creature>();

    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (usedThisRound.Contains(self))
            return;
        if (ResolutionManager.Instance == null)
            return;

        // Find the best target (can be the same target if still alive)
        var next = ResolutionManager.Instance.FindBestTarget(self);
        if (next == null)
            return;

        usedThisRound.Add(self);
        ResolutionManager.Instance.PerformImmediateAttack(
            self,
            next,
            ignoreBodyRules: false,
            onComplete: (success) =>
            {
                if (success)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Whirlwind",
                        self.transform.position,
                        GameColorPalette.TextWarning
                    );
                }
            }
        );
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        usedThisRound.Remove(self);
    }
}
