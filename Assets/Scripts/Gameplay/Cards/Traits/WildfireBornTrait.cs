using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Wildfire Born")]
public class WildfireBornTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return;
        if (wm.CurrentWeather != WeatherType.Wildfire)
            return;

        self.AddStatus(StatusTag.DamageUp, 1);
        self.AddStatus(StatusTag.Immune, 1);

        FeedbackManager.Instance?.ShowFloatingText(
            "Wildfire Born",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "DamageUp +1",
            self.transform.position,
            GameColorPalette.Rage
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Immune",
            self.transform.position,
            GameColorPalette.Immune
        );
    }
}
