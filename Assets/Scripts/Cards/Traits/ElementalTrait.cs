using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Elemental")]
public class ElementalTrait : Trait
{
    // Tracks the extra max HP granted while Wildfire is active so we can cleanly revert it.
    private static readonly Dictionary<Creature, int> wildfireHpBonus = new();

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return;

        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // --- Wildfire: +2 HP (max + current) while Wildfire is active ---
        if (wm.CurrentWeather == WeatherType.Wildfire)
        {
            if (!wildfireHpBonus.ContainsKey(self))
            {
                int bonus = 2;
                self.maxHealth += bonus;
                self.currentHealth += bonus;
                self.currentHealth = Mathf.Min(self.currentHealth, self.maxHealth);
                self.RefreshStatsUI();
                wildfireHpBonus[self] = bonus;
            }
        }
        else
        {
            if (wildfireHpBonus.TryGetValue(self, out int bonus))
            {
                self.maxHealth = Mathf.Max(1, self.maxHealth - bonus);
                self.currentHealth = Mathf.Min(self.currentHealth, self.maxHealth);
                self.RefreshStatsUI();
                wildfireHpBonus.Remove(self);
            }
        }

        // --- Drought: +2 Speed ---
        if (wm.CurrentWeather == WeatherType.Drought)
        {
            int currentSpeedUp = self.GetStatus(StatusTag.SpeedUp);
            self.ClearStatus(StatusTag.SpeedUp);
            self.AddStatus(StatusTag.SpeedUp, 2 + currentSpeedUp);
        }

        // --- Storm: +2 Size (Body) ---
        if (wm.CurrentWeather == WeatherType.Storm)
        {
            int currentBodyUp = self.GetStatus(StatusTag.BodyUp);
            self.ClearStatus(StatusTag.BodyUp);
            self.AddStatus(StatusTag.BodyUp, 2 + currentBodyUp);
        }
    }
}
