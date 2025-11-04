using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Swoop")]
public class SwoopTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (target != null && target.IsWounded) return baseDamage + 1;
        return baseDamage;
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(description))
        {
            description = "+1 damage vs wounded targets.";
        }
    }
}


