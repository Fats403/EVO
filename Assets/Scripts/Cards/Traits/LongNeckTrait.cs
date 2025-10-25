using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Long Neck")]
public class LongNeckTrait : Trait
{
	public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
	{
		if (pile != null && pile.count > 0 && baseAmount == Mathf.Max(0, self.body - self.eaten))
			return Mathf.Max(1, baseAmount);
		return baseAmount;
	}
}


