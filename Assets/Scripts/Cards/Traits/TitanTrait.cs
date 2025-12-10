using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Titan")]
public class TitanTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // Gains Taunt each round while alive.
        self.AddStatus(StatusTag.Taunt, 1);
    }

    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null)
            return;
        if (amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying && c.owner == self.owner)
            .ToList();
        foreach (var ally in allies)
        {
            ally.AddStatus(StatusTag.Regen, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Regen +1",
                ally.transform.position,
                GameColorPalette.Regen
            );
        }
    }
}
