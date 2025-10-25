using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Scavenge")]
public class ScavengeTrait : Trait
{
	public override void OnAnyDeath(Creature self, Creature dead)
	{
		if (self.data != null && self.data.type == CardType.Avian)
		{
			if (self.eaten < self.body)
				self.eaten += 1;
		}
	}
}


