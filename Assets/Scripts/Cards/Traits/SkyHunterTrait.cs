using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Sky Hunter")]
public class SkyHunterTrait : Trait
{
	public override bool IgnoreAvianSpeedRequirement(Creature self, Creature target)
	{
		if (self == null || self.data == null || target == null || target.data == null) return false;
		return self.data.type == CardType.Carnivore && target.data.type == CardType.Avian;
	}
}
