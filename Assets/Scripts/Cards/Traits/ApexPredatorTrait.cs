using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Traits/Carnivores/Apex Predator")]
public class ApexPredatorTrait : Trait
{
	public override void OnRoundStart(Creature self)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.owner == self.owner && c.currentHealth > 0 && !c.isDying)
			.Where(c => c.data != null && c.data.type == CardType.Carnivore)
			.ToList();
		foreach (var ally in allies)
		{
			ally.ApplyRage();
			FeedbackManager.Instance?.ShowFloatingText("Rage", ally.transform.position, new Color(1f, 0.4f, 0.2f));
		}
	}

	public override bool CanTargetEqualBody(Creature self, Creature target) { return true; }
}
