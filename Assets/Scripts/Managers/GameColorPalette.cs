using UnityEngine;

/// <summary>
/// Centralized, semantic color palette for status / feedback text.
/// </summary>
public static class GameColorPalette
{
    // --- Core neutral text ---

    /// <summary>Default neutral text color.</summary>
    public static readonly Color TextNeutral = Color.white;

    /// <summary>Muted / secondary text (eg. "Revealed").</summary>
    public static readonly Color TextMuted = new Color(0.8f, 0.8f, 0.8f);

    // --- Core semantic buckets ---

    /// <summary>Strong positive feedback: heals, big buffs, food gains.</summary>
    public static readonly Color TextPositive = new Color(0.3f, 1f, 0.3f);

    /// <summary>Softer positive, used for regen ticks and gentle healing.</summary>
    public static readonly Color TextPositiveSoft = new Color(0.4f, 1f, 0.4f);

    /// <summary>Direct damage / lethal hits.</summary>
    public static readonly Color TextNegative = new Color(1f, 0.3f, 0.3f);

    /// <summary>Damage over time / lingering harm (bleed, starvation, toxins).</summary>
    public static readonly Color TextNegativeDot = new Color(1f, 0.4f, 0.4f);

    /// <summary>Attention-grabbing warnings (blocked, wildfire burns, big penalties).</summary>
    public static readonly Color TextWarning = new Color(1f, 0.5f, 0.2f);

    /// <summary>Special / defensive mechanics (shield, reflect, immune, absorb, toxins).</summary>
    public static readonly Color TextSpecial = new Color(0.7f, 0.9f, 1f);

    /// <summary>Score / resource gains.</summary>
    public static readonly Color TextScore = Color.cyan;

    /// <summary>Poison / disease themed DoT (Infected, Rabies-style effects).</summary>
    public static readonly Color TextDoTPoison = new Color(0.8f, 0.5f, 0.9f);

    // --- Aliases by concept so call sites read clearly ---

    public static Color Heal => TextPositive;
    public static Color Regen => TextPositiveSoft;
    public static Color Damage => TextNegative;
    public static Color DamageOverTime => TextNegativeDot;
    public static Color Starvation => TextNegativeDot;
    public static Color Bleed => TextNegativeDot;
    public static Color Poison => TextDoTPoison;
    public static Color Shield => TextSpecial;
    public static Color Reflect => TextSpecial;
    public static Color Immune => TextSpecial;
    public static Color Absorb => TextSpecial;
    public static Color ScoreGain => TextScore;
    public static Color ScavengeGain => TextScore;
    public static Color Reveal => TextMuted;

    /// <summary>Fiery offensive buff (Rage, wildfire-leaning traits).</summary>
    public static readonly Color Rage = new Color(1f, 0.7f, 0.3f);

    // --- Global alerts / banners ---

    /// <summary>Informational alert (weather changes, global rules text).</summary>
    public static readonly Color AlertInfo = TextSpecial;

    /// <summary>Error / invalid play alerts.</summary>
    public static readonly Color AlertError = new Color(1f, 0.5f, 0.5f);

    // --- Weather themed alert tints (used by WeatherManager + global effects) ---

    public static readonly Color WeatherClear = new Color(0.8f, 1f, 0.8f);
    public static readonly Color WeatherDrought = new Color(0.95f, 0.8f, 0.4f);
    public static readonly Color WeatherStorm = new Color(0.7f, 0.85f, 1f);
    public static readonly Color WeatherWildfire = new Color(1f, 0.6f, 0.3f);
    public static readonly Color WeatherExtinction = new Color(1f, 0.4f, 0.4f);
}




