using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Ghost Strike")]
public class GhostStrikeTrait : Trait
{
	public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// Gain Stealth after attacking regardless of negation
		self.AddStatus(StatusTag.Stealth, 1);
	}
}


