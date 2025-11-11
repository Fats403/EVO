using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Effects/Nutrient Surge Herbivore Buff")]
public class NutrientSurgeHerbivoreBuffTrait : EffectTraitBase
{
    public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
    {
        return baseAmount + 1;
    }
}


