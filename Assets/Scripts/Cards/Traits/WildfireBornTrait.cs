using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Wildfire Born")]
public class WildfireBornTrait : Trait
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
        if (wm.CurrentWeather != WeatherType.Wildfire)
            return;

        int currentDamageUp = self.GetStatus(StatusTag.DamageUp);

        self.AddStatus(StatusTag.DamageUp, 1 + currentDamageUp);
        self.AddStatus(StatusTag.Immune, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "DamageUp +1, Immune",
            self.transform.position,
            GameColorPalette.Rage
        );
    }
}
