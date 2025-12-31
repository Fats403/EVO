using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Apex Predator")]
public class ApexPredatorTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // Get allied carnivores sorted by distance with slot tie-breaker, take closest 2
        var alliedCarnivores = DeterministicHelpers.GetCreaturesSorted(c =>
            c != self
            && c.owner == self.owner
            && c.data != null
            && c.data.type == CardType.Carnivore
        );

        var allies = DeterministicHelpers
            .OrderByDistanceWithTieBreaker(alliedCarnivores, self.transform.position)
            .Take(2)
            .ToList();

        if (allies.Count > 0)
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
        if (self == null || self.HasStatus(StatusTag.Suppress))
            return false;
        return true;
    }
}
