using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Traits/Avians/Confusion Strike")]
public class ConfusionStrikeTrait : Trait
{
	public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
	{
		if (self == null || target == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		if (finalDamage <= 0) return;
		// If controller controls another avian, apply Suppressed to target
		var anyOtherAvian = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Any(c => c != null && c != self && c.currentHealth > 0 && !c.isDying && c.owner == self.owner && c.data != null && c.data.type == CardType.Avian);
		if (anyOtherAvian)
		{
			target.ApplySuppressed(1);
		}
	}
}


