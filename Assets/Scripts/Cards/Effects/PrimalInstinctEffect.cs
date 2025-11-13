using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Primal Instinct")]
public class PrimalInstinctEffect : EffectTraitBase
{
	public override void OnApply(Creature self)
	{
		if (self == null) return;
		self.AddStatus(StatusTag.SpeedUp, 2);
		self.AddStatus(StatusTag.DamageUp, 1);
	}

	public override void OnRoundEnd(Creature self)
	{
		if (self != null)
		{
			self.ClearStatus(StatusTag.SpeedUp);
		}
		base.OnRoundEnd(self);
	}
}


