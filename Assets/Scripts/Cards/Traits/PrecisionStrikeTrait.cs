using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Precision Strike")]
public class PrecisionStrikeTrait : Trait
{
	public override bool CanTargetAny(Creature self) { return true; }

	public override bool TryOverrideFinalDamage(Creature self, Creature target, out int fixedDamage)
	{
		fixedDamage = 1;
		return true;
	}

	public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
	{
		if (target == null) return;
		target.ApplyStunned(1);
		FeedbackManager.Instance?.ShowFloatingText("Stunned", target.transform.position, Color.yellow);
	}
}
