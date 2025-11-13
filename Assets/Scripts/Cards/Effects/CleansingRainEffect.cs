using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Cleansing Rain")]
public class CleansingRainEffect : EffectTraitBase
{
	public override void OnApply(Creature self)
	{
		if (self == null) return;
		self.ClearAllNegativeStatuses();
		remainingRounds = 0;
	}
}


