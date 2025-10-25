using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Pack")]
public class PackTrait : Trait
{
	public override int PredatorBodyBonusForTargeting(Creature self)
	{
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c != self && c.owner == self.owner && c.data != null && c.data.type == CardType.Carnivore);
		return allies.Any() ? 1 : 0;
	}
}


