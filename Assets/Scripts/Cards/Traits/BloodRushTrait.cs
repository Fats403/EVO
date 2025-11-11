using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Blood Rush")]
public class BloodRushTrait : Trait
{
	public override void OnAfterKill(Creature self, Creature target)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		self.ApplyRegen(2);
		self.GrantNextRoundDamageUp(1);
		FeedbackManager.Instance?.ShowFloatingText("Regen +2", self.transform.position, new Color(0.3f, 1f, 0.3f));
		FeedbackManager.Instance?.ShowFloatingText("DamageUp (next)", self.transform.position, new Color(1f, 0.7f, 0.3f));
	}
}
