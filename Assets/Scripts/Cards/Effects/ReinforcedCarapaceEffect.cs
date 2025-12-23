using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Reinforced Carapace")]
public class ReinforcedCarapaceEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Shield, 1);
        remainingRounds = 0;
    }
}
