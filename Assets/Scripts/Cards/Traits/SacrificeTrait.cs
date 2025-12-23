using System.Linq;
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
        // Find lowest HP ally (not self)
        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c =>
                c != null && c != self && c.currentHealth > 0 && !c.isDying && c.owner == self.owner
            )
            .ToList();
        if (allies.Count == 0)
            return;
        var target = allies
            .OrderBy(c => c.currentHealth)
            .ThenBy(_ => GameManager.Instance.NextRandomInt(0, allies.Count))
            .FirstOrDefault();
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
