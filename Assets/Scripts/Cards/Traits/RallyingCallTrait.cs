using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Traits/Avians/Rallying Call")]
public class RallyingCallTrait : Trait
{
	public override void OnAfterKill(Creature self, Creature target)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// All allied avians: +1 Regen and remove 1 Fatigued
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.currentHealth > 0 && !c.isDying && c.owner == self.owner && c.data != null && c.data.type == CardType.Avian)
			.ToList();
		foreach (var ally in allies)
		{
			ally.ApplyRegen(1);
			if (ally.GetStatus(StatusTag.Fatigued) > 0) ally.DecrementStatus(StatusTag.Fatigued, 1);
		}
	}
}