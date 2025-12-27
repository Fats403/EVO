using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Aerial Superiority")]
public class AerialSuperiorityTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppress))
            return baseDamage;
        if (target.data != null && target.data.type == CardType.Avian)
        {
            return Mathf.Max(0, baseDamage + 1);
        }
        return baseDamage;
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        var adj = BoardUtils.GetAdjacentAllies(self);
        var validAllies = adj.Where(c =>
                c != null && c.data != null && c.data.type == CardType.Avian
            )
            .ToList();

        if (validAllies != null && validAllies.Count > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Aerial Superiority",
                self.transform.position,
                GameColorPalette.TextWarning
            );
        }

        foreach (var ally in validAllies)
        {
            ally.AddStatus(StatusTag.SpeedUp, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Speed +1",
                ally.transform.position,
                GameColorPalette.TextPositive
            );
        }
    }
}
