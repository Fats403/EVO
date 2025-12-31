using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Maternal Care")]
public class MaternalCareTrait : Trait
{
    public override void OnRoundEnd(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // Use centralized helpers for deterministic ally selection
        var allies = DeterministicHelpers.GetCreaturesSorted(c =>
            c != self && c.owner == self.owner
        );
        if (allies.Count == 0)
            return;

        // Find lowest HP ally deterministically
        var target = DeterministicHelpers.FindMinBy(allies, c => c.currentHealth);
        if (target == null)
            return;

        int before = target.currentHealth;
        target.Heal(2);
        int healed = target.currentHealth - before;
        if (healed > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Maternal Care",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                $"+{healed} HP",
                target.transform.position,
                GameColorPalette.Regen
            );
        }
    }
}
