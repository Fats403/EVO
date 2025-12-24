using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Protector's Fury")]
public class ProtectorsFuryTrait : Trait
{
    /// <summary>
    /// Protector's Fury triggers *after* a protected herbivore ally actually
    /// takes damage, instead of pre-emptively when they are merely targeted.
    /// This makes the counter-attack feel fairer and less confusing, and the
    /// immediate strike is still resolved with a visible attack animation.
    /// </summary>
    public override void OnAnyDamage(
        Creature self,
        Creature victim,
        Creature attacker,
        int finalDamage
    )
    {
        if (self == null || victim == null || attacker == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;
        if (self.currentHealth <= 0 || self.isDying)
            return;
        if (self.owner != victim.owner)
            return;
        if (self == victim)
            return;
        // Don't retaliate against our own team or ourselves (prevents proccing on self-attacks)
        if (attacker == null || self.owner == attacker.owner)
            return;
        if (victim.data == null || victim.data.type != CardType.Herbivore)
            return;
        // Immediate retaliatory strike ignoring body rules, with animation.
        ResolutionManager.Instance?.PerformImmediateAttack(
            self,
            attacker,
            ignoreBodyRules: true,
            onComplete: (success) =>
            {
                if (success)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Protector's Fury",
                        self.transform.position,
                        GameColorPalette.TextWarning
                    );
                }
            }
        );
    }
}
