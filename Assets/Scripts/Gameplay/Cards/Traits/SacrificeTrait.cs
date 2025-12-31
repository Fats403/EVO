using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Sacrifice")]
public class SacrificeTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || amountTaken <= 0)
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
        if (target != null)
        {
            target.Heal(1);
            self.AddStatus(StatusTag.Malnourish, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "+1 HP",
                target.transform.position,
                GameColorPalette.Regen
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Sacrifice",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Malnourished +1",
                self.transform.position,
                GameColorPalette.Starvation
            );
        }
    }
}
