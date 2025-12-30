using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Blood Rush")]
public class BloodRushTrait : Trait
{
    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // Immediate benefit: heal now.
        self.Heal(2);

        // Flag to gain Rage next round (using per-creature state for determinism)
        self.traitGrantBloodRush = true;
        FeedbackManager.Instance?.ShowFloatingText(
            "Blood Rush",
            self.transform.position,
            GameColorPalette.TextPositive
        );
    }

    // Rage is granted in Creature.ResetRoundBookkeeping() at next round start
}
