using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Relentless")]
public class RelentlessTrait : Trait
{
	public override bool IgnoreAvianSpeedRequirement(Creature self, Creature target)
	{
		return true;
	}
}


