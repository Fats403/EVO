using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Storm Rider")]
public class StormRiderTrait : Trait
{
    public override bool NegateWeatherPenalty(Creature self, WeatherType weather)
    {
        if (self == null)
            return false;
        if (self.HasStatus(StatusTag.Suppress))
            return false;
        // Immune to negative weather penalties from all weathers.
        return true;
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return;

        if (wm.CurrentWeather == WeatherType.Storm || wm.CurrentWeather == WeatherType.Drought)
        {
            self.AddStatus(StatusTag.DamageUp, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Storm Rider",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "DamageUp +1",
                self.transform.position,
                GameColorPalette.Rage
            );
        }
    }
}
