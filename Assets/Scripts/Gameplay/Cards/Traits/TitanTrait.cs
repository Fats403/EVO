using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Titan")]
public class TitanTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
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
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // All allied creatures gain Regen +1 (deterministic ordering for consistency)
        var allies = DeterministicHelpers.GetCreaturesSorted(c => c.owner == self.owner);

        if (allies.Count > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Titan",
                self.transform.position,
                GameColorPalette.TextWarning
            );
        }

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
