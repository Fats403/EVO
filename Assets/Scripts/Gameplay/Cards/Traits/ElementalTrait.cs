using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Elemental")]
public class ElementalTrait : Trait
{
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

        // --- Wildfire max HP bonus using per-creature state for determinism ---
        if (newWeather == WeatherType.Wildfire)
        {
            if (self.traitElementalHpBonus == 0)
            {
                int bonus = 2;
                self.maxHealth += bonus;
                self.currentHealth += bonus;
                self.currentHealth = Mathf.Min(self.currentHealth, self.maxHealth);
                self.traitElementalHpBonus = bonus;
            }
        }
        else
        {
            if (self.traitElementalHpBonus > 0)
            {
                int bonus = self.traitElementalHpBonus;
                self.maxHealth = Mathf.Max(1, self.maxHealth - bonus);
                // When removing the bonus, clamp current health but never below 1.
                self.currentHealth = Mathf.Max(1, Mathf.Min(self.currentHealth, self.maxHealth));
                self.traitElementalHpBonus = 0;
            }
        }

        self.RefreshStatsUI();
    }

    // traitElementalHpBonus is automatically cleared when creature dies/is reinitialized
}
