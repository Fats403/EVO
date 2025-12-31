using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Confusion Strike")]
public class ConfusionStrikeTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;
        // If controller controls another avian, apply Suppress to target (deterministic creature set)
        var anyOtherAvian = DeterministicHelpers
            .GetCreaturesSorted(c =>
                c != self
                && c.owner == self.owner
                && c.data != null
                && c.data.type == CardType.Avian
            )
            .Any();
        if (anyOtherAvian)
        {
            target.AddStatus(StatusTag.Suppress, 2);
            FeedbackManager.Instance?.ShowFloatingText(
                "Confusion Strike",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Suppress +2",
                target.transform.position,
                GameColorPalette.TextWarning
            );
        }
    }
}
