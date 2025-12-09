using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Solar Recovery")]
public class SolarRecoveryGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;
        if (WeatherManager.Instance == null)
            return;
        if (WeatherManager.Instance.CurrentWeather != WeatherType.Clear)
            return;

        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Solar Recovery: all creatures gain +2 Regen.",
                new Color(0.8f, 1f, 0.7f)
            );
            FeedbackManager.Instance.Log(
                "Global Effect: Solar Recovery grants Regen 2 to all creatures."
            );
        }

        var all = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .ToList();
        foreach (var c in all)
        {
            c.AddStatus(StatusTag.Regen, 2);
        }
        remainingRounds = 0;
    }
}
