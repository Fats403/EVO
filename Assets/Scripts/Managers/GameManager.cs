using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public TextMeshProUGUI roundText;
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
    public int finalRound = 15;
    public Era currentEra = Era.Triassic;

    [Header("Momentum")]
    public int p1Momentum;
    public int p2Momentum;
    public TextMeshProUGUI p1MomentumLabel;
    public TextMeshProUGUI p2MomentumLabel;

    [Header("Game Over")]
    public bool isGameOver;

    [Header("Debug")]
    public GamePhase currentPhase = GamePhase.Setup;
    public int rngSeed = 0;
    private System.Random rng;

    [Header("Turn Order")]
    [Tooltip("Determines which player starts the Place phase for this round.")]
    public SlotOwner startingPlayerForRound = SlotOwner.Player1;

    [Header("Presentation")]
    [Tooltip("Minimum time that a played creature stays spotlighted before the turn can advance.")]
    public float cardPreviewHoldSeconds = 3.0f;

    [Tooltip("Delay between showing an effect card preview and applying its logic.")]
    public float effectRevealDelaySeconds = 1f;

    [Tooltip("Delay between AI taking its action and the next turn starting.")]
    public float aiTurnDelaySeconds = 1f;

    [Tooltip("Delay after announcing that the round is resolving before combat begins.")]
    public float resolveStartDelaySeconds = 1f;

    private Coroutine placePhaseRoutine;
    private SlotOwner currentPlaceTurnOwner = SlotOwner.Player1;
    private SlotOwner? awaitingTurnOwner;
    private bool p1PassedThisRound;
    private bool p2PassedThisRound;

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
        // Initialize AI deck/hand before the first round begins so both players follow the same rules.
        if (AIManager.Instance != null)
        {
            AIManager.Instance.BuildDeckAndDrawStartingHand();
        }
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
        if (isGameOver || currentPhase != GamePhase.Place)
            return;
        if (!awaitingTurnOwner.HasValue || awaitingTurnOwner.Value != SlotOwner.Player1)
            return;
        HandlePass(SlotOwner.Player1);
    }

    void OnToggleLogClicked()
    {
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ToggleLogPanel();
        }
    }

    void UpdatePhaseLabel()
    {
        string eraLabel = currentEra.ToString();

        // Round/era display goes to roundText if assigned; otherwise fall back to phaseText
        if (roundText != null)
        {
            roundText.text = $"Round {currentRound} – {eraLabel}";
        }

        UpdatePhaseStatusText();
        UpdateMomentumUI();
    }

    void UpdatePhaseStatusText()
    {
        if (phaseText == null)
            return;

        if (isGameOver)
        {
            phaseText.text = "Game Over";
            return;
        }

        switch (currentPhase)
        {
            case GamePhase.Setup:
                phaseText.text = "Setup";
                break;
            case GamePhase.Draw:
                phaseText.text = "Draw";
                break;
            case GamePhase.Place:
                if (awaitingTurnOwner.HasValue)
                {
                    phaseText.text =
                        awaitingTurnOwner.Value == SlotOwner.Player1
                            ? "Player 1 Turn"
                            : "Player 2 Turn";
                }
                else
                {
                    phaseText.text = "Place Creatures";
                }
                break;
            case GamePhase.Resolve:
                phaseText.text = "Resolving...";
                break;
            case GamePhase.End:
                phaseText.text = "End of Round";
                break;
        }
    }

    void BeginSetup()
    {
        isGameOver = false;

        // Seed already set; initialize round/era then move to Draw
        currentRound = 1;
        currentEra = GetEraForRound(currentRound);
        currentPhase = GamePhase.Draw;
        UpdatePhaseLabel();
        FeedbackManager.Instance?.ShowGlobalAlert(
            $"The {currentEra} Era Has began!",
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
        // Mirror per-round draws for the AI using the same rules from DeckManager.
        if (AIManager.Instance != null)
        {
            AIManager.Instance.DrawCardsForRoundStart();
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
        ResetMomentumForRound();
        CardPreviewManager.Instance?.HideAll();
        p1PassedThisRound = false;
        p2PassedThisRound = false;
        awaitingTurnOwner = null;
        startingPlayerForRound = (currentRound % 2 == 1) ? SlotOwner.Player1 : SlotOwner.Player2;
        currentPlaceTurnOwner = startingPlayerForRound;

        if (placePhaseRoutine != null)
            StopCoroutine(placePhaseRoutine);
        placePhaseRoutine = StartCoroutine(PlacePhaseCoroutine());
        UpdateEndTurnButtonState();
    }

    IEnumerator PlacePhaseCoroutine()
    {
        while (!BothPlayersFinished())
        {
            if (OwnerFinished(currentPlaceTurnOwner))
            {
                currentPlaceTurnOwner = Opponent(currentPlaceTurnOwner);
                continue;
            }

            yield return StartCoroutine(ExecuteTurn(currentPlaceTurnOwner));
            currentPlaceTurnOwner = Opponent(currentPlaceTurnOwner);
        }

        awaitingTurnOwner = null;
        UpdateEndTurnButtonState();
        placePhaseRoutine = null;
        currentPhase = GamePhase.Resolve;
        UpdatePhaseLabel();

        float delay = Mathf.Max(0f, resolveStartDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        BeginResolve();
    }

    IEnumerator ExecuteTurn(SlotOwner owner)
    {
        if (OwnerFinished(owner))
            yield break;

        awaitingTurnOwner = owner;
        UpdateEndTurnButtonState();
        NotifyYourMove(owner);

        if (owner == SlotOwner.Player1)
        {
            while (awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner)
            {
                if (!HasMomentum(owner))
                {
                    HandlePass(owner);
                    break;
                }
                yield return null;
            }
        }
        else
        {
            // Give a brief moment after announcing the AI's turn before it acts.
            float introDelay = Mathf.Max(0f, aiTurnDelaySeconds * 0.5f);
            if (introDelay > 0f)
                yield return new WaitForSeconds(introDelay);

            if (!HasMomentum(owner))
            {
                HandlePass(owner);
                yield break;
            }

            bool played = AIManager.Instance != null && AIManager.Instance.TryPlaySingleAction();
            if (!played)
            {
                HandlePass(owner);
            }
            else
            {
                while (awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner)
                    yield return null;
            }

            // Small pause after AI acts or passes so its move is readable.
            float pause = Mathf.Max(0f, aiTurnDelaySeconds);
            if (pause > 0f)
                yield return new WaitForSeconds(pause);
        }
    }

    bool BothPlayersFinished()
    {
        return OwnerFinished(SlotOwner.Player1) && OwnerFinished(SlotOwner.Player2);
    }

    bool OwnerFinished(SlotOwner owner)
    {
        if (!HasMomentum(owner))
            return true;
        return owner == SlotOwner.Player1 ? p1PassedThisRound : p2PassedThisRound;
    }

    bool HasMomentum(SlotOwner owner)
    {
        return GetMomentum(owner) > 0;
    }

    void NotifyYourMove(SlotOwner owner)
    {
        UpdatePhaseStatusText();
        FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(owner)}: Your move");
    }

    void UpdateEndTurnButtonState()
    {
        if (endTurnButton == null || endTurnLabel == null)
            return;

        if (isGameOver)
        {
            endTurnButton.interactable = false;
            endTurnLabel.text = "Game Over";
            return;
        }

        bool playerTurnActive =
            currentPhase == GamePhase.Place
            && awaitingTurnOwner.HasValue
            && awaitingTurnOwner.Value == SlotOwner.Player1
            && !p1PassedThisRound
            && HasMomentum(SlotOwner.Player1);

        endTurnButton.interactable = playerTurnActive;

        if (currentPhase == GamePhase.Resolve)
        {
            endTurnLabel.text = string.IsNullOrEmpty(endTurnBusyText)
                ? "Resolving..."
                : endTurnBusyText;
        }
        else if (playerTurnActive)
        {
            endTurnLabel.text = "Pass";
        }
        else if (currentPhase == GamePhase.Place)
        {
            endTurnLabel.text = "Waiting...";
        }
        else
        {
            endTurnLabel.text = string.IsNullOrEmpty(endTurnIdleText)
                ? "End Turn"
                : endTurnIdleText;
        }
    }

    void HandlePass(SlotOwner owner)
    {
        bool alreadyPassed = owner == SlotOwner.Player1 ? p1PassedThisRound : p2PassedThisRound;
        if (!alreadyPassed)
        {
            if (owner == SlotOwner.Player1)
                p1PassedThisRound = true;
            else
                p2PassedThisRound = true;
            if (FeedbackManager.Instance != null)
            {
                string ownerTag = FeedbackManager.TagOwner(owner);
                FeedbackManager.Instance.Log($"{ownerTag} passed.");
            }
            // Show pass information in the phase text instead of a global alert.
            if (phaseText != null)
                phaseText.text = owner == SlotOwner.Player1 ? "Player 1 passes" : "Player 2 passes";
        }

        if (awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner)
            awaitingTurnOwner = null;

        UpdateEndTurnButtonState();
        UpdatePhaseStatusText();
    }

    void CompleteTurnAction(SlotOwner owner)
    {
        if (awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner)
        {
            awaitingTurnOwner = null;
            UpdateEndTurnButtonState();
        }
    }

    SlotOwner Opponent(SlotOwner owner)
    {
        return owner == SlotOwner.Player1 ? SlotOwner.Player2 : SlotOwner.Player1;
    }

    bool IsTurnOwner(SlotOwner owner)
    {
        return awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner;
    }

    public void OnCreaturePlayedDuringPlacement(Creature creature)
    {
        if (creature == null || creature.data == null)
            return;
        if (currentPhase != GamePhase.Place)
            return;

        StartCoroutine(OnCreaturePlayedDuringPlacementRoutine(creature));
    }

    IEnumerator OnCreaturePlayedDuringPlacementRoutine(Creature creature)
    {
        if (creature == null || creature.data == null)
            yield break;

        CardPreviewManager.Instance?.ShowForcedCreature(creature);
        AnnounceCardPlay(creature.owner, creature.data.cardName);

        float hold = Mathf.Max(0f, cardPreviewHoldSeconds);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        CompleteTurnAction(creature.owner);
    }

    void AnnounceCardPlay(SlotOwner owner, string cardName)
    {
        if (FeedbackManager.Instance == null || string.IsNullOrEmpty(cardName))
            return;
        FeedbackManager.Instance.Log($"{FeedbackManager.TagOwner(owner)} played {cardName}");
    }

    public bool TryPlayEffectCard(
        EffectCard card,
        SlotOwner owner,
        IEnumerable<Creature> targets,
        out string failureReason
    )
    {
        failureReason = null;
        if (card == null)
        {
            failureReason = "Invalid effect card.";
            return false;
        }

        if (!CanPlayEffectCard(card, owner, out failureReason))
            return false;

        var list = targets != null ? targets.Where(c => c != null).ToList() : new List<Creature>();
        StartCoroutine(PlayEffectCardRoutine(card, owner, list));
        return true;
    }

    IEnumerator PlayEffectCardRoutine(EffectCard card, SlotOwner owner, List<Creature> targets)
    {
        AnnounceCardPlay(owner, card.effectName);
        CardPreviewManager.Instance?.ShowForcedEffect(card, owner);
        // First: wait for the effect reveal delay, then actually apply the effect logic.
        float revealDelay = Mathf.Max(0f, effectRevealDelaySeconds);
        if (revealDelay > 0f)
            yield return new WaitForSeconds(revealDelay);

        EffectsManager.Instance?.PlayOnTargets(card, targets, owner);

        // Second: keep the preview visible until the total preview time has elapsed.
        // We want the card to stay spotlighted for cardPreviewHoldSeconds from the start,
        // with the effect resolving part-way through at revealDelay.
        float totalPreview = Mathf.Max(0f, cardPreviewHoldSeconds);
        float remaining = Mathf.Max(0f, totalPreview - revealDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        CompleteTurnAction(owner);
    }

    void BeginResolve()
    {
        if (resolutionManager == null)
        {
            Debug.LogError("ResolutionManager not assigned to GameManager");
            return;
        }
        UpdateEndTurnButtonState();
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

        // Hard cap: at or beyond final round, trigger a game-ending extinction event.
        if (currentRound >= finalRound)
        {
            StartCoroutine(HandleGameOverExtinction());
            return;
        }

        if (currentEra != previousEra)
        {
            if (currentEra == Era.Extinction)
            {
                int remainingRounds = Mathf.Max(0, finalRound - currentRound);
                if (remainingRounds > 0)
                {
                    FeedbackManager.Instance?.ShowGlobalAlert(
                        $"{remainingRounds} rounds until extinction event...",
                        new Color(1f, 0.6f, 0.4f)
                    );
                }
            }
            else
            {
                FeedbackManager.Instance?.ShowGlobalAlert(
                    $"The {currentEra} Era Has Started",
                    new Color(0.9f, 0.9f, 0.6f)
                );
            }
        }
        currentPhase = GamePhase.Draw;
        UpdatePhaseLabel();
        BeginDraw();
    }

    private IEnumerator HandleGameOverExtinction()
    {
        isGameOver = true;

        // Lock out further input
        if (endTurnButton != null)
            endTurnButton.interactable = false;
        if (endTurnLabel != null)
            endTurnLabel.text = "Game Over";

        currentPhase = GamePhase.End;
        UpdatePhaseLabel();

        // Crossfade background to the special extinction weather, if configured.
        if (weatherVideoBackground != null)
        {
            StartCoroutine(weatherVideoBackground.CrossfadeTo(WeatherType.Extinction, 0.7f));
        }

        FeedbackManager.Instance?.ShowGlobalAlert(
            "A final extinction event has wiped out all life.\nGame Over.",
            new Color(1f, 0.4f, 0.3f)
        );

        // Drive the visual/gameplay extinction through the VFXManager if available.
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.TriggerGameEndingExtinction();
        }
        else if (resolutionManager != null)
        {
            var creatures = resolutionManager
                .AllCreatures()
                .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
                .ToList();

            foreach (var c in creatures)
            {
                if (c != null && !c.isDying && c.currentHealth > 0)
                    c.Kill("Final Extinction");
            }
        }

        yield break;
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
        int currentEraMomentum = GetMomentumForEra(currentEra);

        if (p1MomentumLabel != null)
            p1MomentumLabel.text = $"{p1Momentum} / {currentEraMomentum}";
        if (p2MomentumLabel != null)
            p2MomentumLabel.text = $"{p2Momentum} / {currentEraMomentum}";
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

    // --- Creature card rules ---

    public bool CanPlayCreatureCard(CreatureCard card, SlotOwner owner)
    {
        return CanPlayCreatureCard(card, owner, out _);
    }

    public bool CanPlayCreatureCard(CreatureCard card, SlotOwner owner, out string failureReason)
    {
        // Full rules check that also spends momentum on success.
        return CanPlayCreatureCardInternal(card, owner, spendMomentum: true, out failureReason);
    }

    /// <summary>
    /// Preview-only check for whether a creature card could be played under current rules
    /// without actually spending momentum. Intended for AI and UI heuristics.
    /// </summary>
    public bool CanPlayCreatureCardPreview(
        CreatureCard card,
        SlotOwner owner,
        out string failureReason
    )
    {
        return CanPlayCreatureCardInternal(card, owner, spendMomentum: false, out failureReason);
    }

    bool CanPlayCreatureCardInternal(
        CreatureCard card,
        SlotOwner owner,
        bool spendMomentum,
        out string failureReason
    )
    {
        failureReason = null;

        if (card == null)
        {
            failureReason = "Invalid creature card.";
            return false;
        }

        if (isGameOver)
        {
            failureReason = "The game is over.";
            return false;
        }

        if (currentPhase != GamePhase.Place)
        {
            failureReason = "You can only play creatures during the Place phase.";
            return false;
        }

        if (currentPhase == GamePhase.Place && !IsTurnOwner(owner))
        {
            failureReason = "Wait for your turn.";
            return false;
        }

        if (!IsTierAllowedInEra(card.tier, currentEra))
        {
            failureReason =
                $"Tier {card.tier} creatures are not available in the {currentEra} era.";
            return false;
        }

        int cost = GetCreatureCost(card);
        if (cost < 0)
            cost = 0;

        if (spendMomentum)
        {
            if (!TrySpendMomentum(owner, cost))
            {
                failureReason = "Not enough Momentum.";
                return false;
            }
        }
        else
        {
            if (GetMomentum(owner) < cost)
            {
                failureReason = "Not enough Momentum.";
                return false;
            }
        }

        return true;
    }

    // --- Effect card rules ---

    public bool CanPlayEffectCard(EffectCard card, SlotOwner owner)
    {
        return CanPlayEffectCard(card, owner, out _);
    }

    public bool CanPlayEffectCard(EffectCard card, SlotOwner owner, out string failureReason)
    {
        // Full rules check that also spends momentum on success.
        return CanPlayEffectCardInternal(card, owner, spendMomentum: true, out failureReason);
    }

    /// <summary>
    /// Preview-only check for whether an effect card could be played under current rules
    /// without actually spending momentum. Intended for AI and UI heuristics.
    /// </summary>
    public bool CanPlayEffectCardPreview(EffectCard card, SlotOwner owner, out string failureReason)
    {
        return CanPlayEffectCardInternal(card, owner, spendMomentum: false, out failureReason);
    }

    bool CanPlayEffectCardInternal(
        EffectCard card,
        SlotOwner owner,
        bool spendMomentum,
        out string failureReason
    )
    {
        failureReason = null;

        if (card == null)
        {
            failureReason = "Invalid effect card.";
            return false;
        }

        if (isGameOver)
        {
            failureReason = "The game is over.";
            return false;
        }

        if (currentPhase != GamePhase.Place)
        {
            failureReason = "You can only play effects during the Place phase.";
            return false;
        }

        if (currentPhase == GamePhase.Place && !IsTurnOwner(owner))
        {
            failureReason = "Wait for your turn.";
            return false;
        }

        // Era requirement
        if (currentEra < card.minEraAllowed)
        {
            failureReason = $"This card cannot be played before the {card.minEraAllowed} era.";
            return false;
        }

        // Weather requirement (e.g., Solar Recovery)
        // TODO: change this to specific weather requirements
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
        if (spendMomentum)
        {
            if (!TrySpendMomentum(owner, cost))
            {
                failureReason = "Not enough Momentum.";
                return false;
            }
        }
        else
        {
            if (GetMomentum(owner) < cost)
            {
                failureReason = "Not enough Momentum.";
                return false;
            }
        }

        return true;
    }

    public int NextRandomInt(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }
}
