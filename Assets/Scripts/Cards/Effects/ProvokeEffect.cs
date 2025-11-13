using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Provoke")]
public class ProvokeEffect : EffectTraitBase
{
	public override void OnApply(Creature self)
	{
		if (self == null) return;
		self.AddStatus(StatusTag.Taunt, 1);
		remainingRounds = 0;
	}
}


