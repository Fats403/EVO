using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Effects/Global/Solar Recovery")]
public class SolarRecoveryGlobalEffect : GlobalEffectBase
{
	public override void OnPlay(ResolutionManager rm)
	{
		if (rm == null) return;
		if (WeatherManager.Instance == null) return;
		if (WeatherManager.Instance.CurrentWeather != WeatherType.Clear) return;

		var all = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.currentHealth > 0 && !c.isDying)
			.ToList();
		foreach (var c in all)
		{
			c.ApplyRegen(2);
		}
		remainingRounds = 0;
	}
}


