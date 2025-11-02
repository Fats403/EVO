using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Quick Bite")]
public class QuickBiteTrait : Trait
{
    private readonly static HashSet<Creature> usedThisRound = new();

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


