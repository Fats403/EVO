using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Windfall")]
public class WindfallTrait : Trait
{
	public override void OnAfterCarnivorePhase(Creature self, FoodPile pile)
	{
		if (pile == null) return;
		if (pile.count > 0 && self.eaten < self.body)
		{
			pile.Take(1);
			self.eaten += 1;
		}
	}
}


