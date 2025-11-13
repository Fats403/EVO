using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Effects/Global/Extinction Event")]
public class ExtinctionEventGlobalEffect : GlobalEffectBase
{
	public override void OnPlay(ResolutionManager rm)
	{
		if (rm == null) return;
		var all = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.currentHealth > 0 && !c.isDying)
			.ToList();
		foreach (var c in all)
		{
			c.ApplyDamage(3, null);
		}
		if (rm.foodPile != null)
		{
			rm.foodPile.count += 3;
			rm.foodPile.UpdateUI();
		}
		remainingRounds = 0;
	}
}


