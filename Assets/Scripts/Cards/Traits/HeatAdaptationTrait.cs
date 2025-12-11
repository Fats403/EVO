using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Heat Adaptation")]
public class HeatAdaptationTrait : Trait
{
    public override int BodyBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return 0;

        return wm.CurrentWeather == WeatherType.Wildfire ? 2 : 0;
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

        if (wm.CurrentWeather == WeatherType.Wildfire)
        {
            self.AddStatus(StatusTag.Immune, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Heat Adaptation",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Immune",
                self.transform.position,
                GameColorPalette.Immune
            );
        }
    }
}
