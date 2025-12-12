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

    public override int ActionPriorityBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null || wm.CurrentWeather != WeatherType.Drought)
            return 0;

        // During Drought, this avian acts before others in the mixed action phase.
        // This replaces the old "pre-herbivore steal" sub-phase.
        return 100;
    }
}
