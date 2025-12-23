using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Galeforce")]
public class GaleforceTrait : Trait
{
    // Storm: +2 Speed while storm is active. Implemented as a passive speed bonus
    // so it updates instantly on weather change and shows in both UI and ordering.
    public override int SpeedBonus(Creature self)
    {
        if (self == null || self.HasStatus(StatusTag.Suppress))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return 0;

        return wm.CurrentWeather == WeatherType.Storm ? 2 : 0;
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;

        var wm = WeatherManager.Instance;
        if (wm == null || wm.CurrentWeather != WeatherType.Storm)
            return;

        target.AddStatus(StatusTag.Fatigue, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Galeforce",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigued +1",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
