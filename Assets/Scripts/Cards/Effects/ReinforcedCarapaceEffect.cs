using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Effects/Reinforced Carapace")]
public class ReinforcedCarapaceEffect : EffectTraitBase
{
    [SerializeField] public int shieldCharges = 1;

    public override void OnApply(Creature self)
    {
        if (self == null) return;
        self.ApplyShield(Mathf.Max(1, shieldCharges));
        remainingRounds = 0;
    }
}


