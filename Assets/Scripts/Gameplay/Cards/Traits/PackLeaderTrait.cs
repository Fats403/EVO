using UnityEngine;

/// <summary>
/// Pack Leader: When this creature attacks, all other allied carnivores
/// also attack the same target (if valid). Follow-up attacks are queued
/// and processed sequentially for clear visual feedback.
///
/// Note: Follow-up attacks do NOT trigger other Pack Leaders to prevent
/// infinite recursion (handled by ResolutionManager.isResolvingFollowUpAttacks).
/// </summary>
[CreateAssetMenu(menuName = "Traits/Carnivores/Pack Leader")]
public class PackLeaderTrait : Trait
{
    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null || target == null)
            return;
        if (wasNegated)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (ResolutionManager.Instance == null)
            return;

        // Get allied carnivores (excluding self) in deterministic order
        var allies = DeterministicHelpers.GetCreaturesSorted(c =>
            c != self
            && c.owner == self.owner
            && c.data != null
            && c.data.type == CardType.Carnivore
            && c.currentHealth > 0
            && !c.isDying
        );

        foreach (var ally in allies)
        {
            if (!ResolutionManager.Instance.IsValidAttackTarget(ally, target))
                continue;

            // Use QueueFollowUpAttack for sequential processing
            // This prevents all attacks from firing simultaneously
            var allyRef = ally; // Capture for closure
            ResolutionManager.Instance.QueueFollowUpAttack(
                ally,
                target,
                ignoreBodyRules: false,
                onComplete: (success) =>
                {
                    if (success)
                    {
                        FeedbackManager.Instance?.ShowFloatingText(
                            "Pack Leader",
                            allyRef.transform.position,
                            GameColorPalette.TextWarning
                        );
                    }
                },
                sourceTraitName: "Pack Leader"
            );
        }
    }
}
