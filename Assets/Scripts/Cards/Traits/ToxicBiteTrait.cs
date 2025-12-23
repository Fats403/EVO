using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Toxic Bite")]
public class ToxicBiteTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        target.AddStatus(StatusTag.Infection, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Toxic Bite",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Infected +2",
            target.transform.position,
            GameColorPalette.Poison
        );
        var adj = BoardUtils.GetAdjacentAllies(target);
        if (adj != null)
        {
            foreach (var c in adj.Where(c => c != null))
            {
                c.AddStatus(StatusTag.Infection, 1);
                FeedbackManager.Instance?.ShowFloatingText(
                    "Infected +1",
                    c.transform.position,
                    GameColorPalette.Poison
                );
            }
        }
    }
}
