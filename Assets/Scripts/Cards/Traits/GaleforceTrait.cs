using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Galeforce")]
public class GaleforceTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null || self.HasStatus(StatusTag.Suppressed))
            return;

        var wm = WeatherManager.Instance;
        if (wm == null || wm.CurrentWeather != WeatherType.Storm)
            return;

        int currentSpeedUp = self.GetStatus(StatusTag.SpeedUp);

        self.ClearStatus(StatusTag.SpeedUp);
        self.AddStatus(StatusTag.SpeedUp, 2 + currentSpeedUp);
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;

        var wm = WeatherManager.Instance;
        if (wm == null || wm.CurrentWeather != WeatherType.Storm)
            return;

        target.AddStatus(StatusTag.Fatigued, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigue +1",
            target.transform.position,
            Color.yellow
        );
    }
}
