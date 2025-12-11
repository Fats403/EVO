using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Armored Counter")]
public class ArmoredCounterTrait : Trait
{
    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null || attacker == null)
            return;
        if (self.currentHealth <= 0 || self.isDying)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        // Immediate counter if still alive
        ResolutionManager.Instance?.PerformImmediateAttack(
            self,
            attacker,
            ignoreBodyRules: false,
            onComplete: (success) =>
            {
                if (success)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Armored Counter",
                        self.transform.position,
                        GameColorPalette.TextWarning
                    );
                }
            }
        );
    }
}
