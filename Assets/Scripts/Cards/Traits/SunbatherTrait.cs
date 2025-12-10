using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Sunbather")]
public class SunbatherTrait : Trait
{
    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        var wm = WeatherManager.Instance;
        if (wm == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        return wm.CurrentWeather == WeatherType.Clear ? 2 : 0;
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
        if (!wm.LastWeather.HasValue)
            return;
        if (wm.CurrentWeather == WeatherType.Clear && wm.LastWeather.Value != WeatherType.Clear)
        {
            self.AddStatus(StatusTag.Regen, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Regen +1",
                self.transform.position,
                GameColorPalette.Regen
            );
        }
    }
}
