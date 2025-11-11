using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Effects/Reinforced Carapace")]
public class ReinforcedCarapaceTrait : EffectTraitBase
{
    private bool consumed = false;

    public override int ModifyIncomingDamage(Creature self, Creature attacker, int baseDamage)
    {
        if (consumed) return baseDamage;
        int reduced = Mathf.Max(0, baseDamage - 2);
        consumed = true;
        return reduced;
    }
}


