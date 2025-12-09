using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Parasitic Infection")]
public class ParasiticInfectionEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Infected, 2);
        remainingRounds = 0;
    }
}
