using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Nutrient Surge")]
public class NutrientSurgeEffect : EffectTraitBase
{
    public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
    {
        return baseAmount + 1;
    }
}


