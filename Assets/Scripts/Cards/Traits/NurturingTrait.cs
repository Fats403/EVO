using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Nurturing")]
public class NurturingTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var adj = BoardUtils.GetAdjacentAllies(self);
        var validAllies = adj.Where(c =>
                c != null
                && c.currentHealth > 0
                && !c.isDying
                && c.GetStatus(StatusTag.Fatigued) > 0
            )
            .ToList();

        if (validAllies.Count > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Nurturing",
                self.transform.position,
                GameColorPalette.TextWarning
            );
        }

        foreach (var ally in validAllies)
        {
            ally.DecrementStatus(StatusTag.Fatigued, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Fatigue -1",
                ally.transform.position,
                GameColorPalette.TextPositive
            );
        }
    }
}
