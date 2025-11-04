using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WeatherType { Clear, Drought, Wildfire, Storm }

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    [Header("Weights (percent-like)")]
    public int weightClear = 50;
    public int weightDrought = 20;
    public int weightStorm = 20;
    public int weightWildfire = 10;

    [Header("State")]
    [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
    [SerializeField] private WeatherType? lastWeather = null;
    [SerializeField] private bool isFirstRound = true;
    private int? starveDamageOverride = null;

    public Action<WeatherType> OnWeatherChanged;

    public WeatherType CurrentWeather => currentWeather;
    public WeatherType? LastWeather => lastWeather;
    public bool IsFirstRound => isFirstRound;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void InitializeClearStart()
    {
        isFirstRound = true;
        lastWeather = null;
        currentWeather = WeatherType.Clear;
        starveDamageOverride = null;
        FeedbackManager.Instance?.Log("Weather: Clear (start)");
        OnWeatherChanged?.Invoke(currentWeather);
    }

    int Next(int minInclusive, int maxExclusive)
    {
        return GameManager.Instance != null
            ? GameManager.Instance.NextRandomInt(minInclusive, maxExclusive)
            : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    public WeatherType RollNextWeather()
    {
        // First round stays Clear
        if (isFirstRound)
        {
            isFirstRound = false;
            lastWeather = currentWeather;
            return currentWeather;
        }

        var candidates = new List<(WeatherType type, int weight)>
        {
            (WeatherType.Clear, weightClear),
            (WeatherType.Drought, weightDrought),
            (WeatherType.Storm, weightStorm),
            (WeatherType.Wildfire, weightWildfire),
        };

        bool Disallowed(WeatherType t)
        {
            if (lastWeather == WeatherType.Wildfire && t == WeatherType.Wildfire) return true; // no wildfire twice
            if (lastWeather == WeatherType.Storm && t == WeatherType.Wildfire) return true;   // no storm -> wildfire
            return false;
        }

        var filtered = candidates.Where(c => c.weight > 0 && !Disallowed(c.type)).ToList();
        if (filtered.Count == 0)
        {
            // Fallback: if constraints removed all, allow Clear
            filtered.Add((WeatherType.Clear, 1));
        }

        int total = filtered.Sum(c => c.weight);
        int roll = Next(0, total);
        int acc = 0;
        WeatherType picked = filtered[0].type;
        foreach (var c in filtered)
        {
            acc += c.weight;
            if (roll < acc) { picked = c.type; break; }
        }

        lastWeather = currentWeather;
        currentWeather = picked;
        starveDamageOverride = null;
        FeedbackManager.Instance?.Log($"Weather: {currentWeather}");
        OnWeatherChanged?.Invoke(currentWeather);
        return currentWeather;
    }

    public void ApplyRoundStartEffects(FoodPile pile)
    {
        starveDamageOverride = null;
        if (pile == null) return;
        switch (currentWeather)
        {
            case WeatherType.Clear:
            {
                int add = Next(1, 3); // +1 to +2
                pile.count = Mathf.Clamp(pile.count + add, 0, pile.maxCap);
                pile.UpdateUI();
                FeedbackManager.Instance?.ShowFloatingText($"Weather: Clear +{add} food", pile.transform.position, new Color(0.5f, 0.9f, 0.5f));
                break;
            }
            case WeatherType.Drought:
            {
                int remove = Next(1, 3); // -1 to -2
                pile.count = Mathf.Clamp(pile.count - remove, 0, pile.maxCap);
                pile.UpdateUI();
                starveDamageOverride = 3;
                FeedbackManager.Instance?.ShowFloatingText($"Drought: -{remove} food (Starve 3)", pile.transform.position, new Color(0.9f, 0.7f, 0.3f));
                break;
            }
            case WeatherType.Storm:
            {
                int remove = 1;
                pile.count = Mathf.Clamp(pile.count - remove, 0, pile.maxCap);
                pile.UpdateUI();
                FeedbackManager.Instance?.ShowFloatingText("Storm: -1 food", pile.transform.position, new Color(0.6f, 0.8f, 1f));
                break;
            }
            case WeatherType.Wildfire:
            default:
                break;
        }
    }

    public int GetStarvationDamageOrDefault(int defaultVal)
    {
        return starveDamageOverride.HasValue ? starveDamageOverride.Value : defaultVal;
    }

    public void ApplyEndOfRoundEffects()
    {
        switch (currentWeather)
        {
            case WeatherType.Wildfire:
            {
                var all = FindObjectsByType<Creature>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
                    .ToList();
                foreach (var c in all)
                {
                    Vector3 pos = c.transform.position;
                    c.ApplyDamage(1, null);
                    FeedbackManager.Instance?.ShowFloatingText("Wildfire -1 HP", pos, new Color(1f, 0.5f, 0.2f));
                }
                break;
            }
            case WeatherType.Storm:
            {
                var all = FindObjectsByType<Creature>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
                    .ToList();
                var p1 = all.Where(c => c.owner == SlotOwner.Player1).ToList();
                var p2 = all.Where(c => c.owner == SlotOwner.Player2).ToList();
                if (p1.Count > 0)
                {
                    var t = p1[Next(0, p1.Count)];
                    Vector3 pos = t.transform.position;
                    t.ApplyDamage(1, null);
                    FeedbackManager.Instance?.ShowFloatingText("Storm -1 HP", pos, new Color(0.6f, 0.8f, 1f));
                }
                if (p2.Count > 0)
                {
                    var t = p2[Next(0, p2.Count)];
                    Vector3 pos = t.transform.position;
                    t.ApplyDamage(1, null);
                    FeedbackManager.Instance?.ShowFloatingText("Storm -1 HP", pos, new Color(0.6f, 0.8f, 1f));
                }
                break;
            }
            default:
                break;
        }
    }
}


