using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WeatherType
{
    Clear,
    Drought,
    Wildfire,
    Storm,
}

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    [Header("Weights (percent-like)")]
    public int weightClear = 50;
    public int weightDrought = 20;
    public int weightStorm = 20;
    public int weightWildfire = 10;

    [Header("State")]
    [SerializeField]
    private WeatherType currentWeather = WeatherType.Clear;

    [SerializeField]
    private WeatherType? lastWeather = null;

    [SerializeField]
    private bool isFirstRound = true;
    private int? starveDamageOverride = null;

    public Action<WeatherType> OnWeatherChanged;

    public WeatherType CurrentWeather => currentWeather;
    public WeatherType? LastWeather => lastWeather;
    public bool IsFirstRound => isFirstRound;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
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
            if (lastWeather == WeatherType.Wildfire && t == WeatherType.Wildfire)
                return true; // no wildfire twice
            if (lastWeather == WeatherType.Storm && t == WeatherType.Wildfire)
                return true; // no storm -> wildfire
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
            if (roll < acc)
            {
                picked = c.type;
                break;
            }
        }

        lastWeather = currentWeather;
        currentWeather = picked;
        starveDamageOverride = null;
        FeedbackManager.Instance?.Log($"Weather: {currentWeather}");
        // Screen-center alert for weather changes
        Color alertColor = currentWeather switch
        {
            WeatherType.Clear => new Color(0.8f, 1f, 0.8f),
            WeatherType.Drought => new Color(0.95f, 0.8f, 0.4f),
            WeatherType.Storm => new Color(0.7f, 0.85f, 1f),
            WeatherType.Wildfire => new Color(1f, 0.6f, 0.3f),
            _ => Color.white,
        };
        FeedbackManager.Instance?.ShowGlobalAlert($"Weather: {currentWeather}", alertColor);
        OnWeatherChanged?.Invoke(currentWeather);
        return currentWeather;
    }

    public void ApplyRoundStartEffects(FoodPile pile)
    {
        starveDamageOverride = null;
        if (pile == null)
            return;
        switch (currentWeather)
        {
            case WeatherType.Clear:
            {
                int add = Next(1, 3); // +1 to +2
                pile.count = Mathf.Max(0, pile.count + add);
                pile.UpdateUI();

                break;
            }
            case WeatherType.Drought:
            {
                int remove = Next(1, 3); // -1 to -2
                pile.count = Mathf.Max(0, pile.count - remove);
                pile.UpdateUI();
                starveDamageOverride = 3;
                break;
            }
            case WeatherType.Storm:
            {
                int remove = 1;
                pile.count = Mathf.Max(0, pile.count - remove);
                pile.UpdateUI();

                // Apply 1 stack of Fatigued to all Avians at storm start
                var avians = FindObjectsByType<Creature>(FindObjectsSortMode.None)
                    .Where(c =>
                        c != null
                        && c.currentHealth > 0
                        && !c.isDying
                        && c.data != null
                        && c.data.type == CardType.Avian
                    )
                    .ToList();
                foreach (var a in avians)
                {
                    a.ApplyFatigued(1);
                }
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
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Wildfire -1 HP",
                        pos,
                        new Color(1f, 0.5f, 0.2f)
                    );
                }
                break;
            }
            case WeatherType.Storm:
            {
                // Storm no longer deals end-of-round damage; effect applied at round start
                break;
            }
            default:
                break;
        }
    }
}
