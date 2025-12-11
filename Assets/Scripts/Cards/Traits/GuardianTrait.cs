using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Guardian")]
public class GuardianTrait : Trait
{
    public override void OnAnyDamage(
        Creature self,
        Creature victim,
        Creature attacker,
        int finalDamage
    )
    {
        if (self == null || victim == null)
            return;
        if (finalDamage <= 0)
            return;
        if (self == victim)
            return;
        if (self.owner != victim.owner)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // Adjacent ally took damage
        var adj = BoardUtils.GetAdjacentAllies(self);
        if (adj != null && adj.Contains(victim))
        {
            self.AddStatus(StatusTag.Shielded, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Guardian",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Shield",
                self.transform.position,
                GameColorPalette.Shield
            );
        }
    }
}
