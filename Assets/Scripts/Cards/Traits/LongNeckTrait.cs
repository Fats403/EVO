using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Long Neck")]
public class LongNeckTrait : Trait
{
	public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
	{
        // Always try to take +1 more when feeding (actual take is still limited by pile)
        return baseAmount + 1;
	}
}


