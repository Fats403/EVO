using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Night Hunter")]
public class NightHunterTrait : Trait
{
    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        var wm = WeatherManager.Instance;
        bool storm = wm != null && wm.CurrentWeather == WeatherType.Storm;
        bool hasStealth = self.HasStatus(StatusTag.Stealth);
        if (storm || hasStealth)
            return 2;
        return 0;
    }
}
