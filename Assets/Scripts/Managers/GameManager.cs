using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    public DraftManager draftManager;

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

    [Header("UI - Canvas Groups")]
    public CanvasGroup mainCanvasGroup;
    public CanvasGroup gameOverCanvasGroup;

    [Tooltip("World-space gameplay canvas (Canvas_World).")]
    public CanvasGroup worldCanvasGroup;

    [Header("Game Over UI")]
    public TextMeshProUGUI gameOverOutcomeText;
    public TextMeshProUGUI player1ScoreText;
    public TextMeshProUGUI player2ScoreText;
    public float gameOverFadeDuration = 0.75f;
    public float postExtinctionUIPauseSeconds = 1.5f;

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

    private List<BoardSlot> allSlots = new List<BoardSlot>();

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

        InitializeBoardSlots();
    }

    public BoardSlot GetSlotByIndex(int index)
    {
        return allSlots.FirstOrDefault(s => s.index == index);
    }

    private void InitializeBoardSlots()
    {
        allSlots.Clear();
        // Find and index all slots deterministically
        var p1Slots = player1SlotContainer
            .GetComponentsInChildren<BoardSlot>()
            .OrderBy(s => s.transform.position.x)
            .ToList();
        var p2Slots = player2SlotContainer
            .GetComponentsInChildren<BoardSlot>()
            .OrderBy(s => s.transform.position.x)
            .ToList();

        for (int i = 0; i < p1Slots.Count; i++)
        {
            p1Slots[i].index = i;
            allSlots.Add(p1Slots[i]);
        }
        for (int i = 0; i < p2Slots.Count; i++)
        {
            p2Slots[i].index = p1Slots.Count + i;
            allSlots.Add(p2Slots[i]);
        }
    }

    public int GetIndexForSlot(BoardSlot slot)
    {
        return slot != null ? slot.index : -1;
    }

    [Tooltip("Delay between showing an effect card preview and applying its logic.")]
    public float effectRevealDelaySeconds = 1f;

    [Tooltip("Delay between AI taking its action and the next turn starting.")]
    public float aiTurnDelaySeconds = 1f;

    [Tooltip("Delay after announcing that the round is resolving before combat begins.")]
    public float resolveStartDelaySeconds = 1f;

    [Header("Deck Sources")]
    [Tooltip("Global card database used to resolve cardIds when starting a constructed game.")]
    public CardDatabase cardDatabase;

    private Coroutine placePhaseRoutine;
    private SlotOwner currentPlaceTurnOwner = SlotOwner.Player1;
    private SlotOwner? awaitingTurnOwner;
    private bool p1PassedThisRound;
    private bool p2PassedThisRound;

    // Prevents Player 1 from queuing multiple actions while a card preview is still resolving.
    private bool p1ActionLocked;

    // Controllers
    private IPlayerController player1Controller;
    private IPlayerController player2Controller;

    private GameAction lastActionReceived;

    private void Start()
    {
        Debug.Log("[GameManager] Initialized in Phase: " + currentPhase + " | Seed: " + rngSeed);

        // Initialize Controllers
        player1Controller = new LocalHumanController();
        player2Controller = new AIPlayerController(AIManager.Instance);

        player1Controller.OnActionDecided += (action) => lastActionReceived = action;
        player2Controller.OnActionDecided += (action) => lastActionReceived = action;

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (toggleLogButton != null)
            toggleLogButton.onClick.AddListener(OnToggleLogClicked);
        // Auto-wire end turn label if not assigned
        if (endTurnLabel == null && endTurnButton != null)
            endTurnLabel = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();

        // Decide startup mode: constructed deck vs draft.
        bool useConstructed =
            SelectedDeckStore.Mode == GameStartMode.Constructed
            && SelectedDeckStore.HasConstructedDeck;

        if (useConstructed)
        {
            // Show gameplay UI immediately; no draft phase.
            SetCanvasGroupVisible(mainCanvasGroup, true);
            SetCanvasGroupVisible(worldCanvasGroup, true);
            if (worldCanvasGroup != null)
                worldCanvasGroup.gameObject.SetActive(true);
            SetCanvasGroupVisible(gameOverCanvasGroup, false);

            // Explicitly hide any draft overlay if the scene still has one.
            if (draftManager != null && draftManager.draftCanvasGroup != null)
            {
                draftManager.draftCanvasGroup.alpha = 0f;
                draftManager.draftCanvasGroup.interactable = false;
                draftManager.draftCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            // Ensure initial canvas visibility states for draft mode.
            bool usingDraft = draftManager != null;
            // Hide the main gameplay UI and world canvas while we are in the draft, show them otherwise.
            SetCanvasGroupVisible(mainCanvasGroup, !usingDraft);
            SetCanvasGroupVisible(worldCanvasGroup, !usingDraft);
            if (worldCanvasGroup != null)
                worldCanvasGroup.gameObject.SetActive(!usingDraft);
            SetCanvasGroupVisible(gameOverCanvasGroup, false);
        }

        // Manual effect selection UI starts hidden.
        SetManualEffectSelectionUIVisible(false);
        if (manualEffectConfirmButton != null)
            manualEffectConfirmButton.onClick.AddListener(OnManualEffectConfirmClicked);
        if (manualEffectCancelButton != null)
            manualEffectCancelButton.onClick.AddListener(OnManualEffectCancelClicked);

        UpdatePhaseLabel();
        weatherVideoBackground?.ForceTo(WeatherType.Clear);

        // Initialize AI deck/hand before the first round begins so both players follow the same rules.
        AIManager.Instance?.BuildDeckAndDrawStartingHand();

        if (useConstructed)
        {
            // Build the player's deck from the SelectedDeckStore and jump straight into the game.
            InitializeConstructedPlayerDeck();
            SelectedDeckStore.Clear();
            BeginSetup();
        }
        else
        {
            // Draft-based start (existing behaviour).
            if (draftManager != null)
            {
                draftManager.BeginDraft();
            }
            else
            {
                Debug.LogWarning(
                    "GameManager: DraftManager not assigned; starting game without draft."
                );
                BeginSetup();
            }
        }
    }

    /// <summary>
    /// Builds the local player's deck from SelectedDeckStore using the CardDatabase
    /// and initializes the DeckManager.
    /// </summary>
    private void InitializeConstructedPlayerDeck()
    {
        if (!SelectedDeckStore.HasConstructedDeck)
        {
            Debug.LogError("GameManager: InitializeConstructedPlayerDeck called with no deck.");
            return;
        }

        if (DeckManager.Instance == null || cardDatabase == null)
        {
            Debug.LogError(
                "GameManager: Cannot initialize constructed deck – missing DeckManager or CardDatabase."
            );
            return;
        }

        var cards = new List<ScriptableObject>();
        foreach (var entry in SelectedDeckStore.Cards)
        {
            if (string.IsNullOrEmpty(entry.cardId) || entry.count <= 0)
                continue;

            var def = cardDatabase.GetById(entry.cardId);
            if (def == null)
            {
                Debug.LogWarning(
                    $"GameManager: Card with id '{entry.cardId}' not found in CardDatabase."
                );
                continue;
            }

            for (int i = 0; i < entry.count; i++)
            {
                cards.Add(def);
            }
        }

        if (cards.Count == 0)
        {
            Debug.LogError("GameManager: Constructed deck contained no valid cards.");
            return;
        }

        DeckManager.Instance.InitializeFromDraft(cards);
        DeckManager.Instance.InitializeAndDraw();
    }

    void SetCanvasGroupVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null)
            return;
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }

    void SetCanvasGroupAlpha(CanvasGroup cg, float alpha)
    {
        if (cg == null)
            return;
        cg.alpha = alpha;
    }

    void OnDestroy()
    {
        if (endTurnButton != null)
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (toggleLogButton != null)
            toggleLogButton.onClick.RemoveListener(OnToggleLogClicked);
        if (manualEffectConfirmButton != null)
            manualEffectConfirmButton.onClick.RemoveListener(OnManualEffectConfirmClicked);
        if (manualEffectCancelButton != null)
            manualEffectCancelButton.onClick.RemoveListener(OnManualEffectCancelClicked);
    }

    void OnEndTurnClicked()
    {
        if (isGameOver || currentPhase != GamePhase.Place)
            return;
        if (!awaitingTurnOwner.HasValue || awaitingTurnOwner.Value != SlotOwner.Player1)
            return;

        if (player1Controller is LocalHumanController human)
        {
            human.RequestPass();
        }
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
                            ? "Your Turn"
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
            GameColorPalette.AlertInfo
        );
        BeginDraw();
    }

    /// <summary>
    /// Called by the DraftManager once the local player's draft is complete
    /// and the DeckManager has been initialized from the drafted list.
    /// </summary>
    public void OnDraftCompleted()
    {
        // When draft ends, reveal the main gameplay UI.
        SetCanvasGroupVisible(mainCanvasGroup, true);
        SetCanvasGroupVisible(worldCanvasGroup, true);
        if (worldCanvasGroup != null)
            worldCanvasGroup.gameObject.SetActive(true);

        // Let the DeckManager shuffle and draw the starting hand for the drafted deck.
        if (DeckManager.Instance != null)
        {
            DeckManager.Instance.InitializeAndDraw();
        }

        // Proceed into the normal game setup / round flow.
        BeginSetup();
    }

    void BeginDraw()
    {
        // Draw per-round cards respecting max hand size.
        // Round 1 already has its starting hands dealt explicitly:
        // - Player: via DeckManager.InitializeAndDraw() after draft/random deck.
        // - AI: via AIManager.BuildDeckAndDrawStartingHand() in Start().
        // To avoid double-drawing on the first round, only perform the
        // per-round draws from round 2 onward.
        if (currentRound > 1)
        {
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
        // Hand off to resolve intro + resolution coroutine.
        yield return StartCoroutine(BeginResolve());
    }

    IEnumerator ExecuteTurn(SlotOwner owner)
    {
        if (OwnerFinished(owner))
            yield break;

        awaitingTurnOwner = owner;
        UpdateEndTurnButtonState();
        NotifyYourMove(owner);

        // Get the relevant controller
        IPlayerController controller =
            (owner == SlotOwner.Player1) ? player1Controller : player2Controller;

        bool turnSlotFinished = false;

        while (!turnSlotFinished)
        {
            lastActionReceived = null;

            // Signal turn start to controller
            controller.OnTurnStarted();

            // Wait for an action from the controller
            while (lastActionReceived == null)
            {
                controller.OnTurnUpdate();

                // Auto-pass when out of momentum (P1 ONLY, AI handles this in its brain)
                if (owner == SlotOwner.Player1 && !HasMomentum(owner) && !p1ActionLocked)
                {
                    HandlePass(owner);
                    yield break;
                }

                // Safety break if round ends or something else happens
                if (awaitingTurnOwner != owner)
                    yield break;

                yield return null;
            }

            // Process the received action
            GameAction action = lastActionReceived;
            lastActionReceived = null;

            if (action.type == GameActionType.ManualSelectionCancel)
            {
                yield return StartCoroutine(ProcessReceivedAction(action));
                // turnSlotFinished remains false, allowing the player to pick another card
            }
            else
            {
                // This is a "real" action (Play or Pass)
                yield return StartCoroutine(ProcessReceivedAction(action));

                // For this implementation, once a non-cancel action is processed,
                // we consider the single turn slot finished (turns alternate).
                // Note: If Play failed validation, ProcessReceivedAction handles the cleanup.
                turnSlotFinished = true;
            }
        }
    }

    private IEnumerator ProcessReceivedAction(GameAction action)
    {
        if (action == null)
            yield break;

        switch (action.type)
        {
            case GameActionType.Pass:
                HandlePass(action.owner);
                break;

            case GameActionType.PlayCreature:
                var cc = (CreatureCard)cardDatabase.GetById(action.cardId);
                var slot = GetSlotByIndex(action.slotIndex);
                if (CanPlayCreatureCard(cc, action.owner))
                {
                    // Success! Spawn the creature
                    var creature = DeckManager.Instance.SpawnCreature(cc, slot);
                    if (creature != null)
                    {
                        // Wait for the preview routine to complete before ending the turn
                        yield return StartCoroutine(
                            OnCreaturePlayedDuringPlacementRoutine(creature)
                        );

                        // Remove from hand (handled differently for P1 vs AI)
                        if (action.owner == SlotOwner.Player1)
                        {
                            // CardUI is already destroyed by the drag script
                        }
                        else
                        {
                            AIManager.Instance.RemoveCardFromHand(action.cardId);
                        }
                    }
                }
                break;

            case GameActionType.PlayEffect:
                var ec = (EffectCard)cardDatabase.GetById(action.cardId);
                var targets = action
                    .targetSlotIndices.Select(idx => GetSlotByIndex(idx))
                    .Where(s => s != null && s.currentCreature != null)
                    .Select(s => s.currentCreature)
                    .ToList();

                // If it's a human playing a manual effect, momentum was spent in TryBeginManualEffectSelection
                bool shouldSpend = !(
                    ec.requiresManualSelection && action.owner == SlotOwner.Player1
                );

                if (
                    TryPlayEffectCard(
                        ec,
                        action.owner,
                        targets,
                        out string failureReason,
                        shouldSpend
                    )
                )
                {
                    // If this was a manual selection confirmation, clear the state now
                    if (manualEffectSelection != null && manualEffectSelection.card == ec)
                    {
                        manualEffectSelection = null;
                    }

                    // Wait for the effect routine to complete before ending the turn
                    yield return StartCoroutine(PlayEffectCardRoutine(ec, action.owner, targets));

                    if (action.owner == SlotOwner.Player1)
                    {
                        // CardUI is already destroyed by the drag script
                    }
                    else
                    {
                        AIManager.Instance.RemoveCardFromHand(action.cardId);
                    }
                }
                else
                {
                    Debug.LogWarning($"GameManager: Effect action failed: {failureReason}");
                    if (action.owner == SlotOwner.Player1)
                        p1ActionLocked = false;
                    if (manualEffectSelection != null && manualEffectSelection.card == ec)
                    {
                        manualEffectSelection = null;
                    }
                    CompleteTurnAction(action.owner); // Unlock turn
                }
                break;

            case GameActionType.ManualSelectionCancel:
                CancelManualEffectSelection();
                break;
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

        if (owner == SlotOwner.Player1)
        {
            FeedbackManager.Instance?.ShowGlobalAlert("Your Turn", GameColorPalette.AlertInfo);
        }
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
                phaseText.text = owner == SlotOwner.Player1 ? "You pass" : "Player 2 passes";

            // For the AI, also surface a brief global alert so it's obvious that it passed.
            if (owner == SlotOwner.Player2 && FeedbackManager.Instance != null)
            {
                FeedbackManager.Instance.ShowGlobalAlert(
                    "Player 2 passes",
                    GameColorPalette.AlertInfo
                );
            }
        }

        if (awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner)
        {
            awaitingTurnOwner = null;
            lastActionReceived = GameAction.CreatePass(owner); // Mark as passed for the loop
        }

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

    public IPlayerController GetPlayerController(SlotOwner owner)
    {
        return (owner == SlotOwner.Player1) ? player1Controller : player2Controller;
    }

    public void OnCreaturePlayedDuringPlacement(Creature creature)
    {
        if (creature == null || creature.data == null)
            return;
        if (currentPhase != GamePhase.Place)
            return;

        // Once a player card has been played, lock out additional P1 actions until
        // its preview/resolution finishes so turns truly alternate actions.
        if (creature.owner == SlotOwner.Player1)
            p1ActionLocked = true;

        // NOTE: We no longer start the coroutine here because the turn loop (ExecuteTurn)
        // now yield-returns the routine to ensure proper timing.
    }

    public IEnumerator OnCreaturePlayedDuringPlacementRoutine(Creature creature)
    {
        if (creature == null || creature.data == null)
            yield break;

        CardPreviewManager.Instance?.ShowForcedCreature(creature);
        AnnounceCardPlay(creature.owner, creature.data.cardName);

        float hold = Mathf.Max(0f, cardPreviewHoldSeconds);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        if (creature.owner == SlotOwner.Player1)
            p1ActionLocked = false;
    }

    void AnnounceCardPlay(SlotOwner owner, string cardName)
    {
        if (FeedbackManager.Instance == null || string.IsNullOrEmpty(cardName))
            return;
        FeedbackManager.Instance.Log($"{FeedbackManager.TagOwner(owner)} played {cardName}");
    }

    // --- Manual effect-card target selection state ---

    private class ManualEffectSelectionState
    {
        public EffectCard card;
        public SlotOwner owner;
        public List<Creature> candidates = new List<Creature>();
        public HashSet<Creature> selected = new HashSet<Creature>();
        public int minCount;
        public int maxCount;
        public bool allowFewerThanMax;
    }

    private ManualEffectSelectionState manualEffectSelection;

    public bool HasActiveManualEffectSelection =>
        manualEffectSelection != null && manualEffectSelection.card != null;

    void SetManualEffectSelectionUIVisible(bool visible)
    {
        if (manualEffectSelectionGroup == null)
            return;
        manualEffectSelectionGroup.alpha = visible ? 1f : 0f;
        manualEffectSelectionGroup.interactable = visible;
        manualEffectSelectionGroup.blocksRaycasts = visible;
    }

    void UpdateManualEffectSelectionUIState()
    {
        if (!HasActiveManualEffectSelection)
        {
            SetManualEffectSelectionUIVisible(false);
            return;
        }

        var state = manualEffectSelection;
        if (state == null)
        {
            SetManualEffectSelectionUIVisible(false);
            return;
        }

        SetManualEffectSelectionUIVisible(true);

        int selectedCount = state.selected != null ? state.selected.Count : 0;
        bool canConfirm;

        if (state.allowFewerThanMax)
        {
            canConfirm =
                selectedCount >= Mathf.Max(1, state.minCount)
                && selectedCount <= Mathf.Max(1, state.maxCount);
        }
        else
        {
            canConfirm = selectedCount == Mathf.Max(1, state.maxCount);
        }

        if (manualEffectConfirmButton != null)
            manualEffectConfirmButton.interactable = canConfirm;

        if (manualEffectCancelButton != null)
            manualEffectCancelButton.interactable = true;
    }

    void OnManualEffectConfirmClicked()
    {
        ConfirmManualEffectSelection();
    }

    void OnManualEffectCancelClicked()
    {
        if (player1Controller is LocalHumanController human)
        {
            human.RequestManualSelectionCancel();
        }
        else
        {
            CancelManualEffectSelection();
        }
    }

    void ConfirmManualEffectSelection()
    {
        if (manualEffectSelection == null)
            return;

        var state = manualEffectSelection;
        if (state.card == null)
            return;

        int selectedCount = state.selected != null ? state.selected.Count : 0;
        if (selectedCount == 0)
            return;

        bool canConfirm;
        if (state.allowFewerThanMax)
        {
            canConfirm =
                selectedCount >= Mathf.Max(1, state.minCount)
                && selectedCount <= Mathf.Max(1, state.maxCount);
        }
        else
        {
            canConfirm = selectedCount == Mathf.Max(1, state.maxCount);
        }

        if (!canConfirm)
            return;

        // Manual finalize: clear all highlights and resolve the effect.
        var finalTargets = state.selected.Where(t => t != null).ToList();
        var targetIndices = finalTargets
            .Select(t => GetIndexForSlot(BoardUtils.GetSlotOf(t)))
            .ToList();

        foreach (var cand in state.candidates)
        {
            if (cand == null)
                continue;
            var h = cand.GetComponent<TargetHighlightController>();
            if (h != null)
                h.SetHighlighted(false);
        }

        var finalCard = state.card;
        var finalOwner = state.owner;

        SetManualEffectSelectionUIVisible(false);

        if (GetPlayerController(finalOwner) is LocalHumanController human)
        {
            var finalTargetIndices = finalTargets
                .Select(t => GetIndexForSlot(BoardUtils.GetSlotOf(t)))
                .ToList();
            human.RequestPlayEffect(finalCard.cardId, finalTargetIndices);
        }
        else
        {
            // Fallback for non-human or if needed
            manualEffectSelection = null;
            StartCoroutine(PlayEffectCardRoutine(finalCard, finalOwner, finalTargets));
        }
    }

    void CancelManualEffectSelection()
    {
        if (manualEffectSelection == null)
            return;

        var state = manualEffectSelection;
        manualEffectSelection = null;

        // Clear highlights on all candidates.
        foreach (var cand in state.candidates)
        {
            if (cand == null)
                continue;
            var h = cand.GetComponent<TargetHighlightController>();
            if (h != null)
                h.SetHighlighted(false);
        }

        // Hide preview and selection UI.
        CardPreviewManager.Instance?.HideAll();
        SetManualEffectSelectionUIVisible(false);

        // Refund momentum and return the card to hand for the local player.
        if (state.owner == SlotOwner.Player1)
        {
            int refund = Mathf.Max(0, state.card != null ? state.card.momentumCost : 0);
            if (refund > 0)
            {
                RefundMomentum(state.owner, refund);
            }

            if (DeckManager.Instance != null && state.card != null)
            {
                DeckManager.Instance.CreateCardUI(state.card, triggerLayoutAndUI: true);
            }

            // Unlock action so the player can act again.
            p1ActionLocked = false;
            UpdateEndTurnButtonState();
            UpdatePhaseStatusText();
        }
    }

    [Header("Manual Effect Selection UI")]
    [Tooltip("Container for confirm/cancel buttons shown while choosing manual effect targets.")]
    public CanvasGroup manualEffectSelectionGroup;
    public UnityEngine.UI.Button manualEffectConfirmButton;
    public UnityEngine.UI.Button manualEffectCancelButton;

    public bool TryPlayEffectCard(
        EffectCard card,
        SlotOwner owner,
        IEnumerable<Creature> targets,
        out string failureReason,
        bool spendMomentum = true
    )
    {
        failureReason = null;
        if (card == null)
        {
            failureReason = "Invalid effect card.";
            return false;
        }

        // Manual-selection cards should not be resolved through this path for the
        // human player unless targets are provided (which they are during confirmation).
        if (
            card.requiresManualSelection
            && owner == SlotOwner.Player1
            && (targets == null || !targets.Any())
        )
        {
            failureReason = "This effect requires you to select targets manually.";
            return false;
        }

        if (spendMomentum)
        {
            // Normal play path: perform a full rules check that also spends momentum.
            if (!CanPlayEffectCard(card, owner, out failureReason))
                return false;
        }
        else
        {
            // Preview / confirmation path (no additional momentum spend).
            //
            // For manual-selection effects for the local player, momentum was already
            // spent when TryBeginManualEffectSelection succeeded. If we call the
            // preview check again here, it will see 0 remaining momentum and wrongly
            // reject the confirm with "Not enough Momentum." even though we already
            // paid the cost.
            bool isManualConfirmForLocalPlayer =
                card.requiresManualSelection
                && owner == SlotOwner.Player1
                && manualEffectSelection != null
                && manualEffectSelection.card == card;

            if (!isManualConfirmForLocalPlayer)
            {
                if (!CanPlayEffectCardPreview(card, owner, out failureReason))
                    return false;
            }
        }

        var list = targets != null ? targets.Where(c => c != null).ToList() : new List<Creature>();

        if (owner == SlotOwner.Player1)
            p1ActionLocked = true;

        // NOTE: We no longer start the coroutine here because the turn loop (ExecuteTurn)
        // now yield-returns the routine to ensure proper timing.
        return true;
    }

    public IEnumerator PlayEffectCardRoutine(
        EffectCard card,
        SlotOwner owner,
        List<Creature> targets
    )
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

        if (owner == SlotOwner.Player1)
            p1ActionLocked = false;
    }

    /// <summary>
    /// Begins a manual-selection effect card flow where the player clicks up to
    /// a fixed number of valid targets before the effect resolves.
    /// </summary>
    public bool TryBeginManualEffectSelection(
        EffectCard card,
        SlotOwner owner,
        out string failureReason
    )
    {
        failureReason = null;

        if (card == null)
        {
            failureReason = "Invalid effect card.";
            return false;
        }

        if (!card.requiresManualSelection)
        {
            failureReason = "This effect does not use manual target selection.";
            return false;
        }

        if (manualEffectSelection != null)
        {
            failureReason = "You are already choosing targets for another effect.";
            return false;
        }

        // Check rules and momentum without spending yet.
        if (!CanPlayEffectCardPreview(card, owner, out failureReason))
            return false;

        // Discover all valid, living candidates for this effect.
        IEnumerable<Creature> allCreatures;
        if (resolutionManager != null)
        {
            allCreatures = resolutionManager.AllCreatures();
        }
        else
        {
            allCreatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);
        }

        var candidates = allCreatures
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .Where(c =>
                EffectsManager.Instance != null
                && EffectsManager.Instance.IsValidTarget(card, c, owner)
            )
            .ToList();

        // Determine manual selection min/max and availability requirements.
        int maxCount = 1;
        int minCount = 1;
        bool allowFewerThanMax = card.allowFewerThanMax;

        switch (card.targetCount)
        {
            case EffectTargetCount.One:
                maxCount = 1;
                minCount = 1;
                allowFewerThanMax = false;
                break;
            case EffectTargetCount.ManySelectUpToN:
                maxCount = Mathf.Max(1, card.maxTargets);
                if (card.minTargets > 0)
                {
                    minCount = Mathf.Clamp(card.minTargets, 1, maxCount);
                }
                else
                {
                    // If no explicit min is set, default to 1 when the effect
                    // allows fewer than max, otherwise require the full max.
                    minCount = allowFewerThanMax ? 1 : maxCount;
                }
                break;
            default:
                maxCount = 1;
                minCount = 1;
                allowFewerThanMax = false;
                break;
        }

        int requiredAvailable = allowFewerThanMax ? minCount : maxCount;

        if (candidates.Count < requiredAvailable)
        {
            failureReason =
                requiredAvailable == 1
                    ? "There are no valid targets for this effect."
                    : $"You need at least {requiredAvailable} valid targets for this effect.";
            return false;
        }

        // Spend momentum and perform final rules check now that we know it is playable.
        if (!CanPlayEffectCard(card, owner, out failureReason))
            return false;

        if (owner == SlotOwner.Player1)
            p1ActionLocked = true;

        manualEffectSelection = new ManualEffectSelectionState
        {
            card = card,
            owner = owner,
            candidates = candidates,
            selected = new HashSet<Creature>(),
            minCount = minCount,
            maxCount = maxCount,
            allowFewerThanMax = allowFewerThanMax,
        };

        if (FeedbackManager.Instance != null)
        {
            string ownerTag = FeedbackManager.TagOwner(owner);
            string msg;
            if (maxCount == 1)
            {
                msg = $"{ownerTag}: Choose a target for {card.effectName}.";
            }
            else if (allowFewerThanMax && minCount < maxCount)
            {
                msg =
                    $"{ownerTag}: Choose {minCount}-{maxCount} targets for {card.effectName} (then Confirm).";
            }
            else
            {
                msg = $"{ownerTag}: Choose {maxCount} targets for {card.effectName}.";
            }
            FeedbackManager.Instance.Log(msg);
        }

        // Show the effect card preview with an instructional caption so the
        // player has clear context while choosing targets.
        if (CardPreviewManager.Instance != null)
        {
            string caption;
            if (maxCount == 1)
            {
                caption = "Select 1 Creature";
            }
            else if (allowFewerThanMax && minCount < maxCount)
            {
                caption = $"Select {minCount}-{maxCount} Creatures";
            }
            else
            {
                caption = $"Select {maxCount} Creatures";
            }
            CardPreviewManager.Instance.ShowEffectSelection(card, owner, caption);
        }

        // Show and initialize confirm/cancel UI.
        UpdateManualEffectSelectionUIState();

        return true;
    }

    /// <summary>
    /// Called by creatures when clicked; only active while a manual-selection
    /// effect is in progress. Clicks outside the candidate set are ignored.
    /// </summary>
    public void HandleManualEffectCreatureClicked(Creature c)
    {
        if (c == null || manualEffectSelection == null)
            return;

        var state = manualEffectSelection;

        if (state.card == null)
            return;

        if (!state.candidates.Contains(c))
            return;

        if (c.currentHealth <= 0 || c.isDying)
            return;

        bool nowSelected;
        if (state.selected.Contains(c))
        {
            state.selected.Remove(c);
            nowSelected = false;
        }
        else
        {
            state.selected.Add(c);
            nowSelected = true;
        }

        var th = c.GetComponent<TargetHighlightController>();
        if (th != null)
        {
            th.SetHighlighted(nowSelected);
        }
        // Update confirm button state after any change.
        UpdateManualEffectSelectionUIState();
    }

    IEnumerator BeginResolve()
    {
        if (resolutionManager == null)
        {
            Debug.LogError("ResolutionManager not assigned to GameManager");
            yield break;
        }
        // Single global alert when resolution begins so players can track pacing.
        FeedbackManager.Instance?.ShowGlobalAlert(
            $"Round {currentRound} Begins!",
            GameColorPalette.AlertInfo
        );

        // Let the alert breathe before combat resolves.
        float introHold = Mathf.Max(0f, resolveStartDelaySeconds);
        if (introHold > 0f)
            yield return new WaitForSeconds(introHold);

        UpdateEndTurnButtonState();
        // Run the actual resolution sequence.
        yield return StartCoroutine(ResolveRoundCoroutine());
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
                        GameColorPalette.TextWarning
                    );
                }
            }
            else
            {
                FeedbackManager.Instance?.ShowGlobalAlert(
                    $"The {currentEra} Era Has Started",
                    GameColorPalette.AlertInfo
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
            GameColorPalette.Damage
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

        // Let the extinction VFX play out before switching to the game over UI.
        float delay = Mathf.Max(0f, postExtinctionUIPauseSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        ShowGameOverScreen();

        yield break;
    }

    void ShowGameOverScreen()
    {
        int p1 = ScoreManager.player1;
        int p2 = ScoreManager.player2;

        // Decide outcome from Player 1's perspective
        if (gameOverOutcomeText != null)
        {
            if (p1 > p2)
            {
                gameOverOutcomeText.text = "Victory";
            }
            else if (p1 < p2)
            {
                gameOverOutcomeText.text = "Defeat";
            }
            else
            {
                gameOverOutcomeText.text = "Draw";
            }
        }

        if (player1ScoreText != null)
            player1ScoreText.text = p1.ToString();
        if (player2ScoreText != null)
            player2ScoreText.text = p2.ToString();

        StartCoroutine(FadeToGameOverCoroutine());
    }

    IEnumerator FadeToGameOverCoroutine()
    {
        float duration = Mathf.Max(0.01f, gameOverFadeDuration);
        float t = 0f;

        // Prepare initial states
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
        }

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
            gameOverCanvasGroup.alpha = 0f;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);

            SetCanvasGroupAlpha(mainCanvasGroup, 1f - u);
            SetCanvasGroupAlpha(gameOverCanvasGroup, u);

            yield return null;
        }

        // Final visibility and input states
        SetCanvasGroupAlpha(mainCanvasGroup, 0f);
        SetCanvasGroupAlpha(gameOverCanvasGroup, 1f);

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.interactable = true;
            gameOverCanvasGroup.blocksRaycasts = true;
        }
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

    public void RefundMomentum(SlotOwner owner, int amount)
    {
        if (amount <= 0)
            return;
        if (owner == SlotOwner.Player1)
            p1Momentum += amount;
        else
            p2Momentum += amount;
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

    public int GetCreatureCost(CreatureCard card)
    {
        if (card == null)
            return 0;
        // Creature momentum cost is explicitly defined on the card asset. Tiers no longer
        // gate availability by era; they are used purely as design metadata / to inform
        // how expensive a creature should be.
        return Mathf.Max(0, card.momentumCost);
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

        // For the local player, also prevent queuing multiple actions while a previous
        // card preview is still resolving.
        if (owner == SlotOwner.Player1 && p1ActionLocked)
        {
            // If we are currently in manual selection for THIS card, allow it to continue
            // (this happens during confirmation).
            bool isConfirmingManual =
                manualEffectSelection != null && manualEffectSelection.card == card;
            if (!isConfirmingManual)
            {
                failureReason = "You have already taken an action. Wait for the other player.";
                return false;
            }
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

        // For the local player, also prevent queuing multiple actions while a previous
        // card preview is still resolving.
        if (owner == SlotOwner.Player1 && p1ActionLocked)
        {
            // If we are currently in manual selection for THIS card, allow it to continue
            // (this happens during confirmation).
            bool isConfirmingManual =
                manualEffectSelection != null && manualEffectSelection.card == card;
            if (!isConfirmingManual)
            {
                failureReason = "You have already taken an action. Wait for the other player.";
                return false;
            }
        }

        // Era requirement
        if (currentEra < card.minEraAllowed)
        {
            failureReason = $"This card cannot be played before the {card.minEraAllowed} era.";
            return false;
        }

        // Weather requirements: if any allowed-weather flags are set on the card, it can
        // only be played while the current weather matches one of those. If none are set,
        // the card may be played in any weather.
        bool hasWeatherRestriction =
            card.allowInClear || card.allowInDrought || card.allowInStorm || card.allowInWildfire;
        if (hasWeatherRestriction)
        {
            if (WeatherManager.Instance == null)
            {
                failureReason =
                    "This card has weather requirements, but no WeatherManager is present.";
                return false;
            }

            var currentWeather = WeatherManager.Instance.CurrentWeather;
            bool allowed =
                (currentWeather == WeatherType.Clear && card.allowInClear)
                || (currentWeather == WeatherType.Drought && card.allowInDrought)
                || (currentWeather == WeatherType.Storm && card.allowInStorm)
                || (currentWeather == WeatherType.Wildfire && card.allowInWildfire);

            if (!allowed)
            {
                // Build a human-readable description of the allowed weathers for failure text.
                System.Collections.Generic.List<string> names =
                    new System.Collections.Generic.List<string>();
                if (card.allowInClear)
                    names.Add("Clear");
                if (card.allowInDrought)
                    names.Add("Drought");
                if (card.allowInStorm)
                    names.Add("Storm");
                if (card.allowInWildfire)
                    names.Add("Wildfire");

                string weatherLabel;
                if (names.Count == 1)
                {
                    weatherLabel = names[0] + " weather";
                }
                else if (names.Count == 2)
                {
                    weatherLabel = $"{names[0]} or {names[1]} weather";
                }
                else
                {
                    // e.g., "Clear, Drought, or Storm weather"
                    var allButLast = names.GetRange(0, names.Count - 1);
                    string joined = string.Join(", ", allButLast);
                    weatherLabel = $"{joined}, or {names[names.Count - 1]} weather";
                }

                failureReason = $"This card can only be played in {weatherLabel}.";
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

    public void OnGameOverResetClicked()
    {
        // Reload the current scene for a clean restart
        // Scene activeScene = SceneManager.GetActiveScene();
        SceneTransitionManager.Instance.LoadScene("MainMenu");
    }
}
