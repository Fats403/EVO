using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Burrow")]
public class BurrowTrait : Trait
{
	public override void OnRoundStart(Creature self)
	{
		// Grant 1 Shield charge at the start of each round
		if (self != null) self.ApplyShield(1);
	}
}


