using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Aerial Predator")]
public class AerialPredatorTrait : Trait
{
	public override bool CanTargetEqualBody(Creature self, Creature target)
	{
		return self.speed > target.speed;
	}
}


