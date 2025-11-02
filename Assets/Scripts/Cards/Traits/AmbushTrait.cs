using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Ambush")]
public class AmbushTrait : Trait
{
	public override int SpeedBonus(Creature self) => 1;

    private static HashSet<Creature> usedThisRound = new HashSet<Creature>();

    public override void OnRoundStart(Creature self)
    {
        usedThisRound.Remove(self);
    }

    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (!usedThisRound.Contains(self))
        {
            usedThisRound.Add(self);
            return baseDamage + 1;
        }
        return baseDamage;
    }
}


