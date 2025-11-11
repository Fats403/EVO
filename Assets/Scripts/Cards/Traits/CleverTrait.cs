using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Clever")]
public class CleverTrait : Trait
{
	public override int SpeedBonus(Creature self)
	{
		if (self == null || self.data == null) return 0;
		if (self.HasStatus(StatusTag.Suppressed)) return 0;
		var adj = BoardUtils.GetAdjacentAllies(self);
		if (adj == null) return 0;
		return adj.Count(c => c != null && c.data != null && c.data.type == CardType.Carnivore);
	}
}
