using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Pursuit")]
public class PursuitTrait : Trait
{
	public override bool CanTargetEqualBody(Creature self, Creature target)
	{
		return self.speed >= target.speed + 2;
	}
}


