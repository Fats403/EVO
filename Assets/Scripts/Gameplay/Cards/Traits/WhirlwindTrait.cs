using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Whirlwind")]
public class WhirlwindTrait : Trait
{
    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        // Use per-creature flag instead of static HashSet for determinism
        if (self.traitUsedWhirlwind)
            return;
        if (ResolutionManager.Instance == null)
            return;

        // Find the best target (can be the same target if still alive)
        var next = ResolutionManager.Instance.FindBestTarget(self);
        if (next == null)
            return;

        self.traitUsedWhirlwind = true;
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

    // Per-creature flag is reset in Creature.ResetRoundBookkeeping() at round start
}
