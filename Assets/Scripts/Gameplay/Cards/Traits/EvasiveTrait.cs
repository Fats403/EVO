using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Evasive")]
public class EvasiveTrait : Trait
{
    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (finalDamage <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // Flag to gain Stealth next round (using per-creature state for determinism)
        self.traitGrantEvasiveStealth = true;
        FeedbackManager.Instance?.ShowFloatingText(
            "Evasive",
            self.transform.position,
            GameColorPalette.TextMuted
        );
    }

    // Stealth is granted in Creature.ResetRoundBookkeeping() at next round start
}
