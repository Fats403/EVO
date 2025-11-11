using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Effects/Adaptive Camouflage")]
public class AdaptiveCamouflageTrait : EffectTraitBase
{
    public override bool TryNegateAttack(Creature self, Creature attacker) { return true; }
    public override bool CanAttack(Creature self) { return false; }
    public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile) { return 0; }

}


