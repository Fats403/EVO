using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Arid Hunter")]
public class AridHunterTrait : Trait
{
    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm != null && wm.CurrentWeather == WeatherType.Drought)
            return 2;
        return 0;
    }

    public override int PreHerbivorePileSteal(Creature self, FoodPile pile)
    {
        if (self == null || pile == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null || wm.CurrentWeather != WeatherType.Drought)
            return 0;
        if (pile.count <= 0)
            return 0;

        // Steal 1 food before herbivores during Drought.
        return 1;
    }
}
