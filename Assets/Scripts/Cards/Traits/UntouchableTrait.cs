using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Untouchable")]
public class UntouchableTrait : Trait
{
    public override bool TryNegateAttack(Creature self, Creature attacker)
    {
        if (self == null || attacker == null)
            return false;
        if (self.HasStatus(StatusTag.Suppressed))
            return false;

        int effSpeed = ResolutionManager.Instance?.GetEffectiveSpeed(attacker) ?? 0;

        if (effSpeed <= 2)
        {
            // Negate the entire attack.
            FeedbackManager.Instance?.ShowFloatingText(
                "Untouchable",
                self.transform.position,
                GameColorPalette.TextSpecial
            );
            return true;
        }
        return false;
    }
}
