using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Swoop")]
public class SwoopTrait : Trait
{
	public int stealAmount = 1;
	public override int PreHerbivorePileSteal(Creature self, FoodPile pile)
	{
		return stealAmount;
	}
}


