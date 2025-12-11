using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Herd Caller")]
public class HerdCallerTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        var adj = BoardUtils.GetAdjacentAllies(self);
        var validAllies = adj.Where(c =>
                c != null && c.data != null && c.data.type == CardType.Herbivore
            )
            .ToList();
        if (validAllies.Count > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Herd Caller",
                self.transform.position,
                GameColorPalette.TextWarning
            );
        }
        foreach (var ally in validAllies)
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
