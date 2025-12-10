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

        // Compute effective speed of the attacker similar to ResolutionManager.EffSpeed.
        int traitSpeed =
            (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
                ? attacker.traits.Sum(t => t != null ? t.SpeedBonus(attacker) : 0)
                : 0;
        int temp = attacker.GetStatus(StatusTag.SpeedUp) - attacker.GetStatus(StatusTag.Fatigued);
        int effSpeed = attacker.speed + temp + traitSpeed;

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
