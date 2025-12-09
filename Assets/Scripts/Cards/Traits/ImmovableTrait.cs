using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Immovable")]
public class ImmovableTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // Gain +1 Shield whenever the weather changes.
        var wm = WeatherManager.Instance;
        if (wm == null)
            return;
        if (!wm.LastWeather.HasValue)
            return;
        if (wm.LastWeather.Value != wm.CurrentWeather)
        {
            self.AddStatus(StatusTag.Shielded, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Shield +1",
                self.transform.position,
                Color.cyan
            );
        }
    }
}
