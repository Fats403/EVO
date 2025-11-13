using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Titan's Might")]
public class TitansMightEffect : EffectTraitBase
{
	private int appliedBonus = 0;

	public override void OnApply(Creature self)
	{
		if (self == null) return;
		appliedBonus = Mathf.Max(0, self.body);
		if (appliedBonus > 0)
		{
			self.AddStatus(StatusTag.BodyUp, appliedBonus);
		}
	}

	public override void OnRoundEnd(Creature self)
	{
		if (self != null)
		{
			self.ClearStatus(StatusTag.BodyUp);
		}
		base.OnRoundEnd(self);
	}
}


