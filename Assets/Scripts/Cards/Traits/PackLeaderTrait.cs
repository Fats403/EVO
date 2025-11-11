using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Traits/Pack Leader")]
public class PackLeaderTrait : Trait
{
	public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
	{
		if (self == null || target == null) return;
		if (wasNegated) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// Order: other allied carnivores (excluding self)
		var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c != self && c.owner == self.owner && c.currentHealth > 0 && !c.isDying)
			.Where(c => c.data != null && c.data.type == CardType.Carnivore)
			.ToList();
		foreach (var ally in allies)
		{
			if (ResolutionManager.Instance == null) break;
			if (!ResolutionManager.Instance.IsValidAttackTarget(ally, target)) continue;
			ResolutionManager.Instance.PerformImmediateAttack(ally, target, ignoreBodyRules: false);
		}
	}
}
