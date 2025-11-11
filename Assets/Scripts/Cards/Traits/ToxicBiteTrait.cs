using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Toxic Bite")]
public class ToxicBiteTrait : Trait
{
	public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
	{
		if (self == null || target == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		target.ApplyInfected(2);
		FeedbackManager.Instance?.ShowFloatingText("Infected +2", target.transform.position, new Color(0.6f, 1f, 0.6f));
		var adj = BoardUtils.GetAdjacentAllies(target);
		if (adj != null)
		{
			foreach (var c in adj.Where(c => c != null))
			{
				c.ApplyInfected(1);
				FeedbackManager.Instance?.ShowFloatingText("Infected +1", c.transform.position, new Color(0.6f, 1f, 0.6f));
			}
		}
	}
}
