using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Flame Dancer")]
public class FlameDancerTrait : Trait
{
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
        if (
            wm.CurrentWeather == WeatherType.Wildfire
            && wm.LastWeather.Value != WeatherType.Wildfire
        )
        {
            self.AddStatus(StatusTag.Regen, 2);
            FeedbackManager.Instance?.ShowFloatingText(
                "Regen +2",
                self.transform.position,
                new Color(1f, 0.5f, 0.2f)
            );
        }
    }
}
