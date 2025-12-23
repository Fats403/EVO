using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Parasitic Infection")]
public class ParasiticInfectionEffect : EffectTraitBase
{
    public override void OnApply(Creature target)
    {
        if (target == null)
            return;
        target.AddStatus(StatusTag.Infection, 2);
    }
}
