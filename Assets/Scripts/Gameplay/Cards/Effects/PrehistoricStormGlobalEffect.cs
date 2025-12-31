using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Prehistoric Storm")]
public class PrehistoricStormGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.ForceWeather(WeatherType.Storm);
        }
        else if (
            GameManager.Instance != null
            && GameManager.Instance.weatherVideoBackground != null
        )
        {
            GameManager.Instance.weatherVideoBackground.ForceTo(WeatherType.Storm);
        }

        if (rm == null)
            return;

        // Use deterministic helper for enemy selection and random choice
        var enemies = DeterministicHelpers.GetCreaturesSorted(c => c.owner != owner);
        if (enemies.Count == 0)
            return;

        Creature target = DeterministicHelpers.PickRandom(enemies);

        if (target == null)
            return;

        int applied = target.ApplyDamage(1, null, null, "Prehistoric Storm");
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP",
                target.transform.position,
                GameColorPalette.DamageOverTime
            );

            // Visual feedback on the damaged enemy, unless the source card suppressed it.
            if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
            {
                EffectsManager.Instance.PlayHitBounceOnCreatures(new[] { target });
            }
        }

        remainingRounds = 0;
    }
}
