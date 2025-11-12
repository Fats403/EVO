using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Opportunist")]
public class OpportunistTrait : Trait
{
	public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
	{
		if (self == null || target == null) return baseDamage;
		if (self.HasStatus(StatusTag.Suppressed)) return baseDamage;
		if (target.maxHealth <= 0) return baseDamage;
		// +1 damage vs targets below 50% max HP
		if (target.currentHealth * 2 < target.maxHealth)
		{
			return Mathf.Max(0, baseDamage + 1);
		}
		return baseDamage;
	}
}


