using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Storm Rider")]
public class StormRiderTrait : Trait
{
    public override bool NegateWeatherPenalty(Creature self, WeatherType weather)
    {
        if (self == null)
            return false;
        if (self.HasStatus(StatusTag.Suppressed))
            return false;
        // Immune to negative weather penalties from all weathers.
        return true;
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return;

        if (wm.CurrentWeather == WeatherType.Storm || wm.CurrentWeather == WeatherType.Drought)
        {
            self.AddStatus(StatusTag.DamageUp, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "DamageUp +1",
                self.transform.position,
                new Color(1f, 0.7f, 0.3f)
            );
        }
    }
}
