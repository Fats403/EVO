using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Phalanx Leader")]
public class PhalanxLeaderTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        // Apply Bulk +1 aura to allied herbivores for this round using deterministic ordering
        var allies = DeterministicHelpers.GetCreaturesSorted(c =>
            c.owner == self.owner && c.data != null && c.data.type == CardType.Herbivore
        );
        foreach (var a in allies)
        {
            a.AddStatus(StatusTag.Bulk, 1);
        }
    }

    public override void OnTargetedByAttack(Creature self, Creature attacker)
    {
        if (self == null || attacker == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        // Attribute Bleed to this creature so ticks contribute to its
        // roundDamageDealt scoring.
        attacker.AddStatus(StatusTag.Bleed, 1, self);
        FeedbackManager.Instance?.ShowFloatingText(
            "Phalanx Leader",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Bleed +1",
            attacker.transform.position,
            GameColorPalette.Bleed
        );
    }
}
