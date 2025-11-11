using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Protector's Fury")]
public class ProtectorsFuryTrait : Trait
{
	public override void OnAllyTargeted(Creature self, Creature ally, Creature attacker)
	{
		if (self == null || ally == null || attacker == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		if (self.owner != ally.owner) return;
		if (ally.data == null || ally.data.type != CardType.Herbivore) return;
		// Immediate strike ignoring body rules
		if (ResolutionManager.Instance != null)
		{
			ResolutionManager.Instance.PerformImmediateAttack(self, attacker, ignoreBodyRules: true);
		}
	}
}


