using UnityEngine;

/// <summary>
/// Bloodthirsty: After killing a target, attack another enemy (once per round).
/// Uses the follow-up attack queue for sequential processing with other
/// trait-triggered attacks.
/// </summary>
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

        self.traitUsedBloodthirsty = true;

        // Use QueueFollowUpAttack for sequential processing with other trait attacks
        var selfRef = self; // Capture for closure
        ResolutionManager.Instance.QueueFollowUpAttack(
            self,
            next,
            ignoreBodyRules: false,
            onComplete: (success) =>
            {
                if (success)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Bloodthirsty",
                        selfRef.transform.position,
                        GameColorPalette.TextWarning
                    );
                }
            },
            sourceTraitName: "Bloodthirsty"
        );
    }

    // Per-creature flag is reset in Creature.ResetRoundBookkeeping() at round start
}
