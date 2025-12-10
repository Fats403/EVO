using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Aerial Superiority")]
public class AerialSuperiorityTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppressed))
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
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var adj = BoardUtils.GetAdjacentAllies(self);
        foreach (var ally in adj)
        {
            if (ally == null || ally.data == null)
                continue;
            if (ally.data.type != CardType.Avian)
                continue;

            ally.AddStatus(StatusTag.SpeedUp, 1);
        }
    }
}
