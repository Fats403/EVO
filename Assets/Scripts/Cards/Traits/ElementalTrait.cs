using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Elemental")]
public class ElementalTrait : Trait
{
    // Tracks the extra max HP granted while Wildfire is active so we can cleanly revert it.
    private static readonly Dictionary<Creature, int> wildfireHpBonus = new();

    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppress))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return 0;

        // Drought: +2 Speed while Drought is active
        return wm.CurrentWeather == WeatherType.Drought ? 2 : 0;
    }

    // Passive, always-on body bonus during Storm.
    public override int BodyBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppress))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return 0;

        // Storm: +2 Size (Body) while Storm is active
        return wm.CurrentWeather == WeatherType.Storm ? 2 : 0;
    }

    public override void OnWeatherChanged(
        Creature self,
        WeatherType newWeather,
        WeatherType? lastWeather
    )
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // --- Wildfire max HP bonus should exist only while Wildfire is active. ---
        if (newWeather == WeatherType.Wildfire)
        {
            if (!wildfireHpBonus.ContainsKey(self))
            {
                int bonus = 2;
                self.maxHealth += bonus;
                self.currentHealth += bonus;
                self.currentHealth = Mathf.Min(self.currentHealth, self.maxHealth);
                wildfireHpBonus[self] = bonus;
            }
        }
        else
        {
            if (wildfireHpBonus.TryGetValue(self, out int bonus))
            {
                self.maxHealth = Mathf.Max(1, self.maxHealth - bonus);
                // When removing the bonus, clamp current health but never below 1.
                self.currentHealth = Mathf.Max(1, Mathf.Min(self.currentHealth, self.maxHealth));
                wildfireHpBonus.Remove(self);
            }
        }

        self.RefreshStatsUI();
    }

    public override void OnAnyDeath(Creature self, Creature dead)
    {
        if (dead == null)
            return;
        wildfireHpBonus.Remove(dead);
    }
}
