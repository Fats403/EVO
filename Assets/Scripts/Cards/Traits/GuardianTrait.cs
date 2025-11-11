using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Guardian")]
public class GuardianTrait : Trait
{
	public override void OnAnyDamage(Creature self, Creature victim, Creature attacker, int finalDamage)
	{
		if (self == null || victim == null) return;
		if (finalDamage <= 0) return;
		if (self == victim) return;
		if (self.owner != victim.owner) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// Adjacent ally took damage
		var adj = BoardUtils.GetAdjacentAllies(self);
		if (adj != null && adj.Contains(victim))
		{
			self.ApplyShield(1);
			FeedbackManager.Instance?.ShowFloatingText("Shield +1", self.transform.position, Color.cyan);
		}
	}
}


