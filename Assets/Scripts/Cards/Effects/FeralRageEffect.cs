using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Feral Rage")]
public class FeralRageEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.ApplyRage();
        remainingRounds = 0;
    }
}
