using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Traits/Bloodthirsty")]
public class BloodthirstyTrait : Trait
{
	public override void OnAfterKill(Creature self, Creature target)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		if (ResolutionManager.Instance == null) return;
		var next = ResolutionManager.Instance.FindBestTarget(self);
		if (next == null) return;
		// If the killed target is already gone, we just attack the next best available
		if (next == target) return; // avoid pointless call if somehow still same reference
		ResolutionManager.Instance.PerformImmediateAttack(self, next, ignoreBodyRules: false);
	}
}
