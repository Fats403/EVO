using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Illusionist")]
public class IllusionistTrait : Trait
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
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (self == victim)
            return;
        if (self.owner != victim.owner)
            return;

        var adj = BoardUtils.GetAdjacentAllies(self);
        if (adj != null && adj.Contains(victim))
        {
            self.AddStatus(StatusTag.Stealth, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Stealth",
                self.transform.position,
                Color.gray
            );
        }
    }
}

