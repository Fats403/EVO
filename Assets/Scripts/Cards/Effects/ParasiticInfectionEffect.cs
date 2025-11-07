using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Effects/Parasitic Infection")]
public class ParasiticInfectionEffect : EffectTraitBase
{
    [SerializeField] public int stacksOnApply = 2;

    public override void OnApply(Creature self)
    {
        if (self == null) return;
        int n = Mathf.Max(1, stacksOnApply);
        self.ApplyInfected(n);
        remainingRounds = 0;
    }
}


