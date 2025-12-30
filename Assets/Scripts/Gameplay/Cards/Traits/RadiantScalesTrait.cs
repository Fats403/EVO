using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Radiant Scales")]
public class RadiantScalesTrait : Trait
{
    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (finalDamage <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // Flag to gain Shielded next round (using per-creature state for determinism)
        self.traitGrantRadiantShield = true;
        FeedbackManager.Instance?.ShowFloatingText(
            "Radiant Scales",
            self.transform.position,
            GameColorPalette.Shield
        );
    }

    // Shield is granted in Creature.ResetRoundBookkeeping() at next round start
}
