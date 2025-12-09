using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Galeforce")]
public class GaleforceTrait : Trait
{
    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm != null && wm.CurrentWeather == WeatherType.Storm)
            return 2;
        return 0;
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

        target.AddStatus(StatusTag.Fatigued, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigued +1",
            target.transform.position,
            Color.yellow
        );
    }
}
