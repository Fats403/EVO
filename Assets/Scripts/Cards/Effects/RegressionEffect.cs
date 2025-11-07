using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Effects/Evolutionary Regression")]
public class RegressionEffect : EffectTraitBase
{
    [SerializeField] public int suppressionRounds = 2;

    public override void OnApply(Creature self)
    {
        if (self == null) return;
        self.ApplySuppressed(Mathf.Max(1, suppressionRounds));
        remainingRounds = 0;
    }
}


