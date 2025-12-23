using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Generous")]
public class GenerousTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        var adj = BoardUtils.GetAdjacentAllies(self);
        var validAllies = adj
            ?.Where(c => c != null && c.data != null && c.data.type == CardType.Herbivore)
            .ToList();

        if (validAllies != null && validAllies.Count > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Generous",
                self.transform.position,
                GameColorPalette.TextWarning
            );
        }

        foreach (var ally in validAllies)
        {
            ally.eaten += 1;
            FeedbackManager.Instance?.ShowFloatingText(
                "+1 Food",
                ally.transform.position,
                GameColorPalette.TextPositive
            );
        }
    }
}
