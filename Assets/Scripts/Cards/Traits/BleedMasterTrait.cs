using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Bleed Master")]
public class BleedMasterTrait : Trait
{
	public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
	{
		if (self == null || target == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		if (finalDamage <= 0) return;
		target.ApplyBleeding(1);
	}
}


