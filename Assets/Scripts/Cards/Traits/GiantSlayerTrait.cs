using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Giant Slayer")]
public class GiantSlayerTrait : Trait
{
	public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
	{
		if (self == null || target == null) return baseDamage;
		if (self.HasStatus(StatusTag.Suppressed)) return baseDamage;
		// Effective bodies: body + BodyUp - Malnourished
		int selfEff = self.body + self.GetStatus(StatusTag.BodyUp) - self.GetStatus(StatusTag.Malnourished);
		int tgtEff = target.body + target.GetStatus(StatusTag.BodyUp) - target.GetStatus(StatusTag.Malnourished);
		if (tgtEff > selfEff)
		{
			return Mathf.Max(0, baseDamage + 1);
		}
		return baseDamage;
	}
}


