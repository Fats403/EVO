using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Apex Predator")]
public class ApexPredatorTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c =>
                c != null
                && c != self
                && c.owner == self.owner
                && c.currentHealth > 0
                && !c.isDying
                && c.data != null
                && c.data.type == CardType.Carnivore
            )
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - self.transform.position))
            .Take(2)
            .ToList();
        if (allies != null && allies.Count > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Apex Predator",
                self.transform.position,
                GameColorPalette.TextWarning
            );
        }

        foreach (var ally in allies)
        {
            ally.AddStatus(StatusTag.Rage, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Rage",
                ally.transform.position,
                GameColorPalette.Rage
            );
        }
    }

    // Apex Predator: ignore body-size restrictions when this creature attacks.
    public override bool IgnoreBodySizeRequirement(Creature self, Creature target)
    {
        if (self == null || self.HasStatus(StatusTag.Suppressed))
            return false;
        return true;
    }
}
