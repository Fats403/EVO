using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Effects/Parasitic Infection")]
public class ParasiticInfectionTrait : EffectTraitBase
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null) return;
        self.ApplyDamage(1, null);
    }
}




