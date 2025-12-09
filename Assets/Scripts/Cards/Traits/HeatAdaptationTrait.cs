using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Heat Adaptation")]
public class HeatAdaptationTrait : Trait
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

        // Refresh wildfire adaptation each round while Wildfire is active.
        int currentBodyUp = self.GetStatus(StatusTag.BodyUp);
        int currentImmune = self.GetStatus(StatusTag.Immune);

        self.ClearStatus(StatusTag.BodyUp);
        self.AddStatus(StatusTag.BodyUp, 2 + currentBodyUp);
        self.ApplyImmune();
    }
}
