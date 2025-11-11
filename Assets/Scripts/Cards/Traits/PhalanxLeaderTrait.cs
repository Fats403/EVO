using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Phalanx Leader")]
public class PhalanxLeaderTrait : Trait
{
	public override void OnRoundStart(Creature self)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// Apply BodyUp +1 aura to allied herbivores for this round
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.currentHealth > 0 && !c.isDying && c.owner == self.owner && c.data != null && c.data.type == CardType.Herbivore);
		foreach (var a in allies)
		{
			a.AddStatus(StatusTag.BodyUp, 1);
		}
	}

	public override void OnTargetedByAttack(Creature self, Creature attacker)
	{
		if (self == null || attacker == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		attacker.ApplyBleeding(1);
		FeedbackManager.Instance?.ShowFloatingText("Bleeding +1", attacker.transform.position, new Color(1f, 0.4f, 0.4f));
	}
}


