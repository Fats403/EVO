using UnityEngine;

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

        // Get allied carnivores (excluding self) in deterministic order
        var allies = DeterministicHelpers.GetCreaturesSorted(c =>
            c != self
            && c.owner == self.owner
            && c.data != null
            && c.data.type == CardType.Carnivore
        );

        foreach (var ally in allies)
        {
            if (ResolutionManager.Instance == null)
                break;
            if (!ResolutionManager.Instance.IsValidAttackTarget(ally, target))
                continue;
            ResolutionManager.Instance.PerformImmediateAttack(
                ally,
                target,
                ignoreBodyRules: false,
                onComplete: (success) =>
                {
                    if (success)
                    {
                        FeedbackManager.Instance?.ShowFloatingText(
                            "Pack Leader",
                            ally.transform.position,
                            GameColorPalette.TextWarning
                        );
                    }
                }
            );
        }
    }
}
