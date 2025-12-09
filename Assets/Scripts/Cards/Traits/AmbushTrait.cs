using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Ambush")]
public class AmbushTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppressed))
            return baseDamage;

        bool hasNegative = StatusTagGroups.Negative.Any(tag => target.GetStatus(tag) > 0);
        if (hasNegative)
        {
            return Mathf.Max(0, baseDamage + 1);
        }
        return baseDamage;
    }
}
