using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Drought Bringer")]
public class DroughtBringerGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        GameManager.Instance.weatherVideoBackground.ForceTo(WeatherType.Drought);

        var all = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .ToList();
        foreach (var c in all)
        {
            c.AddStatus(StatusTag.Malnourished, 1);
        }

        remainingRounds = 0;
    }
}

