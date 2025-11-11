using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Reckless Charge")]
public class RecklessChargeTrait : Trait
{
	public override bool CanTargetEqualBody(Creature self, Creature target) { return true; }

	public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		self.ApplyDamage(1, self);
		FeedbackManager.Instance?.ShowFloatingText("-1 HP [Reckless]", self.transform.position, new Color(1f, 0.5f, 0.2f));
	}
}
