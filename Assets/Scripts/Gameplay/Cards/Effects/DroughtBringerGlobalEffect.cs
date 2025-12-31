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

        // Apply Malnourish to all living creatures in deterministic slot order
        var all = DeterministicHelpers.GetAllCreaturesSorted();
        foreach (var c in all)
        {
            c.AddStatus(StatusTag.Malnourish, 1);
        }

        // Visual feedback for all affected creatures, unless the source card suppressed it.
        if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(all);
        }

        remainingRounds = 0;
    }
}
