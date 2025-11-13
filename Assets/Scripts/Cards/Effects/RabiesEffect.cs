using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Rabies")]
public class RabiesEffect : EffectTraitBase
{
	public override void OnApply(Creature self)
	{
		if (self == null) return;
		self.ApplyInfected(2);
		self.AddStatus(StatusTag.NoForage, 1);
		remainingRounds = 0;
	}
}


