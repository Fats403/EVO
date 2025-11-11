using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Scavenger Growth")]
public class ScavengerGrowthTrait : Trait
{
	public override void OnAnyDeath(Creature self, Creature dead)
	{
		if (self == null || dead == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		self.body += 1;
		self.RefreshStatsUI();
		FeedbackManager.Instance?.ShowFloatingText("Body +1", self.transform.position, Color.green);
	}
}
