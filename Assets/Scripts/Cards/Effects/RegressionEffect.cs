using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Evolutionary Regression")]
public class RegressionEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Suppress, 3);
        remainingRounds = 0;
    }
}
