using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Sacrifice")]
public class SacrificeTrait : Trait
{
	public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
	{
		if (self == null || amountTaken <= 0) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// Find lowest HP ally (not self)
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c != self && c.currentHealth > 0 && !c.isDying && c.owner == self.owner)
			.ToList();
		if (allies.Count == 0) return;
		var target = allies.OrderBy(c => c.currentHealth).ThenBy(_ => GameManager.Instance.NextRandomInt(0, allies.Count)).FirstOrDefault();
		if (target != null)
		{
			target.ApplyRegen(2);
			FeedbackManager.Instance?.ShowFloatingText("Regen +2", target.transform.position, new Color(0.3f, 1f, 0.3f));
		}
		self.ApplyFatigued(2);
		FeedbackManager.Instance?.ShowFloatingText("Fatigued +2", self.transform.position, Color.yellow);
	}
}




