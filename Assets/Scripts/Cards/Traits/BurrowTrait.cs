using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Burrow")]
public class BurrowTrait : Trait
{
	public override bool TryNegateAttack(Creature self, Creature attacker)
	{
		if (!self.defendedThisRound)
		{
			self.defendedThisRound = true;
			return true;
		}
		return false;
	}
}


