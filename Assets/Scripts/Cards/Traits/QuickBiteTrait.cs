using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Quick Bite")]
public class QuickBiteTrait : Trait
{
	public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
	{
		return baseAmount + 1; // simple version; once-per-round can be added later
	}
}


