using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Avians/Undying Spirit")]
public class UndyingSpiritTrait : Trait
{
	private static readonly HashSet<Creature> used = new HashSet<Creature>();

	public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		if (finalDamage <= 0) return;
		// Trigger on any lethal damage, once per creature
		if (self.currentHealth == 0 && !used.Contains(self))
		{
			self.currentHealth = 1;
			self.RefreshStatsUI();
			self.ApplyRegen(2);
            
			// Clear negative statuses
			self.ClearAllNegativeStatuses();

			used.Add(self);
			FeedbackManager.Instance?.ShowFloatingText("Undying Spirit", self.transform.position, new Color(1f, 0.6f, 0.2f));
		}
	}
}


