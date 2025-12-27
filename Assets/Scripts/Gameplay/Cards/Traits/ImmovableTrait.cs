using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Immovable")]
public class ImmovableTrait : Trait
{
    public override void OnWeatherChanged(
        Creature self,
        WeatherType newWeather,
        WeatherType? lastWeather
    )
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (self.HasStatus(StatusTag.Shield))
            return;

        if (!lastWeather.HasValue)
            return;

        if (lastWeather.Value != newWeather)
        {
            self.AddStatus(StatusTag.Shield, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Immovable",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Shield",
                self.transform.position,
                GameColorPalette.Shield
            );
        }
    }
}
