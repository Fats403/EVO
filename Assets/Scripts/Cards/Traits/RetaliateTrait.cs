using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Retaliate")]
public class RetaliateTrait : Trait
{
	public override void OnTargetedByAttack(Creature self, Creature attacker)
	{
		if (self == null || attacker == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		attacker.ApplyDamage(1, self);
		FeedbackManager.Instance?.ShowFloatingText("-1 HP [Retaliate]", attacker.transform.position, new Color(1f, 0.5f, 0.2f));
	}
}