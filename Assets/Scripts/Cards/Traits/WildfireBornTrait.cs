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

        self.AddStatus(StatusTag.DamageUp, 1);
        self.ApplyImmune();
        FeedbackManager.Instance?.ShowFloatingText(
            "DamageUp +1, Immune",
            self.transform.position,
            new Color(1f, 0.5f, 0.2f)
        );
    }
}
