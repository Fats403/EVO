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

        // Treat common debuff statuses as "negative effects".
        StatusTag[] negativeTags = new StatusTag[]
        {
            StatusTag.Infected,
            StatusTag.Fatigued,
            StatusTag.Starvation,
            StatusTag.Taunt,
            StatusTag.Stunned,
            StatusTag.Suppressed,
            StatusTag.NoForage,
            StatusTag.Bleeding,
            StatusTag.Malnourished,
        };

        bool hasNegative = negativeTags.Any(tag => target.GetStatus(tag) > 0);
        if (hasNegative)
        {
            return Mathf.Max(0, baseDamage + 1);
        }
        return baseDamage;
    }
}
