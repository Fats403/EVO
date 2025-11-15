using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GamePhase
{
    Setup,
    Draw,
    Place,
    Resolve,
    End,
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene References")]
    public Transform player1SlotContainer;
    public Transform player2SlotContainer;
    public Button endTurnButton;
    public Button toggleLogButton;
    public TextMeshProUGUI phaseText;
    public ResolutionManager resolutionManager;
    public FoodPile foodPile;
    public WeatherManager weatherManager;
    public WeatherVideoBackgroundController weatherVideoBackground;

    [Header("UI")]
    public TextMeshProUGUI endTurnLabel;
    public string endTurnIdleText = "End Turn";
    public string endTurnBusyText = "Resolving...";

    [Header("Round & Era")]
    public int currentRound = 1;
    public Era currentEra = Era.Triassic;

    [Header("Momentum")]
    public int p1Momentum;
    public int p2Momentum;
    public TextMeshProUGUI p1MomentumLabel;
    public TextMeshProUGUI p2MomentumLabel;

    [Header("Debug")]
    public GamePhase currentPhase = GamePhase.Setup;
    public int rngSeed = 0;
    private System.Random rng;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        if (rngSeed == 0)
        {
            rngSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }
        rng = new System.Random(rngSeed);
        UnityEngine.Random.InitState(rngSeed);
    }

    void Start()
    {
        Debug.Log("[GameManager] Initialized in Phase: " + currentPhase + " | Seed: " + rngSeed);
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (toggleLogButton != null)
            toggleLogButton.onClick.AddListener(OnToggleLogClicked);
        // Auto-wire end turn label if not assigned
        if (endTurnLabel == null && endTurnButton != null)
            endTurnLabel = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
        UpdatePhaseLabel();
        if (weatherVideoBackground != null)
            weatherVideoBackground.ForceTo(WeatherType.Clear);
        BeginSetup();
    }

    void OnDestroy()
    {
        if (endTurnButton != null)
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (toggleLogButton != null)
            toggleLogButton.onClick.RemoveListener(OnToggleLogClicked);
    }

    void OnEndTurnClicked()
    {
        // Only allow ending the turn during the Place phase
        if (currentPhase != GamePhase.Place)
            return;

        if (endTurnButton != null)
            endTurnButton.interactable = false;
        if (endTurnLabel != null)
            endTurnLabel.text = string.IsNullOrEmpty(endTurnBusyText)
                ? "Resolving..."
                : endTurnBusyText;

        AdvancePhase();
    }

    void OnToggleLogClicked()
    {
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ToggleLogPanel();
        }
    }

    void AdvancePhase()
    {
        currentPhase = (GamePhase)(
            ((int)currentPhase + 1) % System.Enum.GetValues(typeof(GamePhase)).Length
        );
        Debug.Log("[GameManager] New Phase: " + currentPhase);
        UpdatePhaseLabel();

        switch (currentPhase)
        {
            case GamePhase.Draw:
                BeginDraw();
                break;
            case GamePhase.Place:
                BeginPlace();
                break;
            case GamePhase.Resolve:
                BeginResolve();
                break;
            case GamePhase.End:
                BeginEndRound();
                break;
        }
    }

    void UpdatePhaseLabel()
    {
        if (phaseText != null)
        {
            string eraLabel = currentEra.ToString();
            // string phaseLabel = currentPhase.ToString();
            phaseText.text = $"Round {currentRound} – {eraLabel}";
        }
        UpdateMomentumUI();
    }

    void BeginSetup()
    {
        // Seed already set; initialize round/era then move to Draw
        currentRound = 1;
        currentEra = GetEraForRound(currentRound);
        currentPhase = GamePhase.Draw;
        UpdatePhaseLabel();
        FeedbackManager.Instance?.ShowGlobalAlert(
            $"The {currentEra} Era Has begun",
            new Color(0.9f, 0.9f, 0.6f)
        );
        BeginDraw();
    }

    void BeginDraw()
    {
        // Draw per-round cards respecting max hand size
        var dm = DeckManager.Instance;
        if (dm != null)
        {
            dm.DrawCardsForRoundStart();
        }
        if (foodPile != null)
            foodPile.RefillStartOfRound();
        // Weather: roll (first call keeps Clear), then apply start-of-round effects
        if (weatherManager != null)
        {
            var next = weatherManager.RollNextWeather();
            if (weatherVideoBackground != null)
                StartCoroutine(weatherVideoBackground.CrossfadeTo(next, 0.7f));
            weatherManager.ApplyRoundStartEffects(foodPile);
        }
        currentPhase = GamePhase.Place;
        UpdatePhaseLabel();
        BeginPlace();
    }

    void BeginPlace()
    {
        // Reset per-round momentum at the start of the Place phase
        ResetMomentumForRound();

        // Re-enable End Turn for the player
        if (endTurnButton != null)
            endTurnButton.interactable = true;
        if (endTurnLabel != null)
            endTurnLabel.text = string.IsNullOrEmpty(endTurnIdleText)
                ? "End Turn"
                : endTurnIdleText;

        // Trigger simple AI placement for Player2
        if (AIManager.Instance != null)
        {
            AIManager.Instance.TakeTurnPlace();
        }
    }

    void BeginResolve()
    {
        if (resolutionManager == null)
        {
            Debug.LogError("ResolutionManager not assigned to GameManager");
            return;
        }
        StartCoroutine(ResolveRoundCoroutine());
    }

    IEnumerator ResolveRoundCoroutine()
    {
        yield return StartCoroutine(resolutionManager.RevealAndResolveRound());
        currentPhase = GamePhase.End;
        UpdatePhaseLabel();
        BeginEndRound();
    }

    void BeginEndRound()
    {
        // After resolution, advance round/era and prepare next round
        Era previousEra = currentEra;
        currentRound = Mathf.Max(1, currentRound + 1);
        currentEra = GetEraForRound(currentRound);
        if (currentEra != previousEra)
        {
            FeedbackManager.Instance?.ShowGlobalAlert(
                $"The {currentEra} Era Has begun",
                new Color(0.9f, 0.9f, 0.6f)
            );
        }
        currentPhase = GamePhase.Draw;
        UpdatePhaseLabel();
        BeginDraw();
    }

    public Era GetEraForRound(int round)
    {
        if (round <= 4)
            return Era.Triassic;
        if (round <= 8)
            return Era.Jurassic;
        if (round <= 12)
            return Era.Cretaceous;
        return Era.Extinction;
    }

    public int GetMomentumForEra(Era era)
    {
        switch (era)
        {
            case Era.Triassic:
                return 2;
            case Era.Jurassic:
                return 3;
            case Era.Cretaceous:
                return 5;
            case Era.Extinction:
                return 7;
            default:
                return 2;
        }
    }

    public int GetMomentum(SlotOwner owner)
    {
        return owner == SlotOwner.Player1 ? p1Momentum : p2Momentum;
    }

    public bool TrySpendMomentum(SlotOwner owner, int cost)
    {
        if (cost <= 0)
            return true;

        int current = owner == SlotOwner.Player1 ? p1Momentum : p2Momentum;
        if (current < cost)
            return false;

        if (owner == SlotOwner.Player1)
            p1Momentum -= cost;
        else
            p2Momentum -= cost;

        UpdateMomentumUI();
        return true;
    }

    public void ResetMomentumForRound()
    {
        int perRound = GetMomentumForEra(currentEra);
        p1Momentum = perRound;
        p2Momentum = perRound;
        UpdateMomentumUI();
    }

    public void UpdateMomentumUI()
    {
        if (p1MomentumLabel != null)
            p1MomentumLabel.text = $"X {p1Momentum}";
        if (p2MomentumLabel != null)
            p2MomentumLabel.text = $"X {p2Momentum}";
    }

    public bool IsTierAllowedInEra(int tier, Era era)
    {
        switch (era)
        {
            case Era.Triassic:
                // Baseline: only Tier 1; higher tiers must be enabled by special effects
                return tier == 1;
            case Era.Jurassic:
                // Tier 1–2 normally available
                return tier >= 1 && tier <= 2;
            case Era.Cretaceous:
            case Era.Extinction:
                // All tiers available
                return tier >= 1 && tier <= 3;
            default:
                return true;
        }
    }

    public int GetCreatureCost(CreatureCard card)
    {
        if (card == null)
            return 0;
        // Default: cost equals tier, clamped between 1 and 3
        return Mathf.Clamp(card.tier, 1, 3);
    }

    public bool CanPlayCreatureCard(CreatureCard card, SlotOwner owner)
    {
        return CanPlayCreatureCard(card, owner, out _);
    }

    public bool CanPlayCreatureCard(CreatureCard card, SlotOwner owner, out string failureReason)
    {
        failureReason = null;

        if (card == null)
        {
            failureReason = "Invalid creature card.";
            return false;
        }

        if (currentPhase != GamePhase.Place)
        {
            failureReason = "You can only play creatures during the Place phase.";
            return false;
        }

        if (!IsTierAllowedInEra(card.tier, currentEra))
        {
            failureReason =
                $"Tier {card.tier} creatures are not available in the {currentEra} era.";
            return false;
        }

        int cost = GetCreatureCost(card);
        if (!TrySpendMomentum(owner, cost))
        {
            failureReason = "Not enough Momentum.";
            return false;
        }

        return true;
    }

    public bool CanPlayEffectCard(EffectCard card, SlotOwner owner)
    {
        return CanPlayEffectCard(card, owner, out _);
    }

    public bool CanPlayEffectCard(EffectCard card, SlotOwner owner, out string failureReason)
    {
        failureReason = null;

        if (card == null)
        {
            failureReason = "Invalid effect card.";
            return false;
        }

        if (currentPhase != GamePhase.Place)
        {
            failureReason = "You can only play effects during the Place phase.";
            return false;
        }

        // Era requirement
        if (currentEra < card.minEraAllowed)
        {
            failureReason = $"This card cannot be played before the {card.minEraAllowed} era.";
            return false;
        }

        // Weather requirement (e.g., Solar Recovery)
        if (card.requiresClearWeather)
        {
            if (
                WeatherManager.Instance == null
                || WeatherManager.Instance.CurrentWeather != WeatherType.Clear
            )
            {
                failureReason = "This card can only be played in Clear weather.";
                return false;
            }
        }

        // Momentum requirement
        int cost = Mathf.Max(0, card.momentumCost);
        if (!TrySpendMomentum(owner, cost))
        {
            failureReason = "Not enough Momentum.";
            return false;
        }

        return true;
    }

    public int NextRandomInt(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }
}
