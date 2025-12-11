using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Drought Bringer")]
public class DroughtBringerGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        // Smoothly crossfade the visual backdrop and sync the logical weather.
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.ForceWeather(WeatherType.Drought);
        }
        else if (
            GameManager.Instance != null
            && GameManager.Instance.weatherVideoBackground != null
        )
        {
            // Fallback: visual-only if no WeatherManager is present.
            GameManager.Instance.weatherVideoBackground.ForceTo(WeatherType.Drought);
        }

        var all = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .ToList();
        foreach (var c in all)
        {
            c.AddStatus(StatusTag.Malnourished, 1);
        }

        // Visual feedback for all affected creatures, unless the source card suppressed it.
        if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(all);
        }

        remainingRounds = 0;
    }
}
