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
    Extinction, // special end-of-game backdrop; never rolled during play
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

        // Only roll among the "normal" weathers. Extinction is triggered explicitly by game over.
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
        FeedbackManager.Instance?.Log($"Weather: {currentWeather}");

        // Screen-center flavor text for weather changes
        string msg = null;
        switch (currentWeather)
        {
            case WeatherType.Clear:
                // Never announce the very first clear (handled by isFirstRound guard above),
                // but do announce when harsh weather finally clears.
                if (lastWeather.HasValue && lastWeather.Value != WeatherType.Clear)
                    msg = "The weather clears.";
                else
                    msg = "Clear skies continue.";
                break;
            case WeatherType.Drought:
                msg =
                    lastWeather == WeatherType.Drought
                        ? "The drought drags on..."
                        : "A drought grips the land...";
                break;
            case WeatherType.Storm:
                msg =
                    lastWeather == WeatherType.Storm
                        ? "The storm continues to rage..."
                        : "A massive storm rolls in...";
                break;
            case WeatherType.Wildfire:
                msg =
                    lastWeather == WeatherType.Wildfire
                        ? "The wildfire continues to spread..."
                        : "A wildfire has erupted!";
                break;
            case WeatherType.Extinction:
                msg = "An extinction-level event darkens the skies.";
                break;
        }

        if (!string.IsNullOrEmpty(msg) && FeedbackManager.Instance != null)
        {
            Color alertColor = currentWeather switch
            {
                WeatherType.Clear => new Color(0.8f, 1f, 0.8f),
                WeatherType.Drought => new Color(0.95f, 0.8f, 0.4f),
                WeatherType.Storm => new Color(0.7f, 0.85f, 1f),
                WeatherType.Wildfire => new Color(1f, 0.6f, 0.3f),
                WeatherType.Extinction => new Color(1f, 0.4f, 0.4f),
                _ => Color.white,
            };
            FeedbackManager.Instance.ShowGlobalAlert(msg, alertColor);
        }
        OnWeatherChanged?.Invoke(currentWeather);
        return currentWeather;
    }

    public void ApplyRoundStartEffects(FoodPile pile)
    {
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
                    // Allow traits to negate storm fatigue as a weather penalty.
                    bool negated = false;
                    if (!a.HasStatus(StatusTag.Suppressed) && a.traits != null)
                    {
                        foreach (var tr in a.traits.ToArray())
                        {
                            if (tr != null && tr.NegateWeatherPenalty(a, currentWeather))
                            {
                                negated = true;
                                break;
                            }
                        }
                    }
                    if (negated)
                        continue;
                    a.AddStatus(StatusTag.Fatigued, 1);
                }
                break;
            }
            case WeatherType.Wildfire:
            default:
                break;
        }
    }

    /// <summary>
    /// Applies any end-of-round weather effects and returns true if anything
    /// visible or state-changing occurred (used to decide whether to pause).
    /// </summary>
    public bool ApplyEndOfRoundEffects()
    {
        bool didAny = false;
        switch (currentWeather)
        {
            case WeatherType.Wildfire:
            {
                didAny = true;
                FeedbackManager.Instance?.ShowGlobalAlert(
                    "The wildfire burns!",
                    new Color(1f, 0.5f, 0.2f)
                );

                var all = FindObjectsByType<Creature>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
                    .ToList();
                foreach (var c in all)
                {
                    // Allow traits to negate wildfire damage as a weather penalty.
                    bool negated = false;
                    if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                    {
                        foreach (var tr in c.traits.ToArray())
                        {
                            if (tr != null && tr.NegateWeatherPenalty(c, currentWeather))
                            {
                                negated = true;
                                break;
                            }
                        }
                    }
                    if (negated)
                        continue;
                    Vector3 pos = c.transform.position;
                    int applied = c.ApplyDamage(1, null, null, "Wildfire");
                    if (applied > 0)
                    {
                        FeedbackManager.Instance?.ShowFloatingText(
                            $"-{applied} HP",
                            pos,
                            new Color(1f, 0.5f, 0.2f)
                        );
                    }
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
        return didAny;
    }
}
