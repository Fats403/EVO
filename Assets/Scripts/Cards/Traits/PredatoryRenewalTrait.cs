using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Predatory Renewal")]
public class PredatoryRenewalTrait : Trait
{
	public override void OnAfterKill(Creature self, Creature target)
	{
		self?.ApplyRegen(1);
	}
}


