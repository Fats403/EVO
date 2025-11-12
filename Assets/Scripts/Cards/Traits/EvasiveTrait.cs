using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Avians/Evasive")]
public class EvasiveTrait : Trait
{
	private static readonly HashSet<Creature> grantNextRound = new HashSet<Creature>();

	public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
	{
		if (self == null) return;
		if (finalDamage <= 0) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		// Flag to gain Stealth next round
		grantNextRound.Add(self);
	}

	public override void OnRoundStart(Creature self)
	{
		if (self == null) return;
		if (grantNextRound.Contains(self))
		{
			self.AddStatus(StatusTag.Stealth, 1);
			grantNextRound.Remove(self);
		}
	}
}


