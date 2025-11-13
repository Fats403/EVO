using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Territorial Display")]
public class TerritorialDisplayEffect : EffectTraitBase
{
	public override void OnApply(Creature self)
	{
		if (self == null) return;
		self.ApplyFatigued(1);
		remainingRounds = 0;
	}
}


