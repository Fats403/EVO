using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Aerial Predator")]
public class AerialPredatorTrait : Trait
{
	public override bool CanTargetEqualBody(Creature self, Creature target)
	{
		return self.speed > target.speed;
	}

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(description))
        {
            description = "May target equal-body prey even if slower; if faster and equal-body, resolve as a normal hit.";
        }
    }
}


