using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herding")]
public class HerdingTrait : Trait
{
	public override int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
	{
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c != self && c.owner == self.owner && c.data != null && c.data.type == CardType.Herbivore);
		if (allies.Any()) return baseAmount + 1;
		return baseAmount;
	}
}


