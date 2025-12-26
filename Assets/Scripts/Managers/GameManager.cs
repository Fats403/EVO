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

    // Events for decoupling core game state from specific UI/visuals.
    // Other systems (HUD, VFX, networking, etc.) can subscribe to these
    // instead of GameManager directly mutating their state.
    public event System.Action<GamePhase> OnPhaseChanged;
    public event System.Action<int, Era> OnRoundChanged;
    public event System.Action<SlotOwner?> OnAwaitingTurnOwnerChanged;
    public event System.Action<bool> OnGameOverChanged;
    public event System.Action<Era, int, int> OnMomentumChanged;
    public event System.Action OnShowGameOverRequested;

    [Header("Scene References")]
    public Transform player1SlotContainer;
    public Transform player2SlotContainer;
    public ResolutionManager resolutionManager;
    public FoodPile foodPile;
    public WeatherManager weatherManager;
    public WeatherVideoBackgroundController weatherVideoBackground;
    public DraftManager draftManager;

    [Header("Round & Era")]
    public int currentRound = 1;
    public int finalRound = 15;
    public Era currentEra = Era.Triassic;

    [Header("Momentum")]
    public int p1Momentum;
    public int p2Momentum;

    [Header("Game Over")]
    public bool isGameOver;
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

    /// <summary>
    /// Enqueues a local action (typically from UI controllers) into the same
    /// pipeline used by IPlayerController implementations.
    /// </summary>
    public void EnqueueLocalAction(GameAction action)
    {
        lastActionReceived = action;
    }

    /// <summary>
    /// Helper for UI/controllers to lock or unlock the local player's ability
    /// to queue additional actions while an effect/preview is resolving.
    /// </summary>
    public void SetPlayerActionLocked(SlotOwner owner, bool locked)
    {
        if (owner == SlotOwner.Player1)
        {
            p1ActionLocked = locked;
        }
    }

    private void Start()
    {
        Debug.Log("[GameManager] Initialized in Phase: " + currentPhase + " | Seed: " + rngSeed);

        // Initialize Controllers
        player1Controller = new LocalHumanController();
        player2Controller = new AIPlayerController(AIManager.Instance);

        player1Controller.OnActionDecided += (action) => lastActionReceived = action;
        player2Controller.OnActionDecided += (action) => lastActionReceived = action;

        weatherVideoBackground?.ForceTo(WeatherType.Clear);

        // Initialize AI deck/hand before the first round begins so both players follow the same rules.
        AIManager.Instance?.BuildDeckAndDrawStartingHand();

        bool useConstructed =
            SelectedDeckStore.Mode == GameStartMode.Constructed
            && SelectedDeckStore.HasConstructedDeck;

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

    public void OnEndTurnClicked()
    {
        if (isGameOver || currentPhase != GamePhase.Place)
            return;
        if (!awaitingTurnOwner.HasValue || awaitingTurnOwner.Value != SlotOwner.Player1)
            return;

        // Treat the end-turn button as a local input source that queues a Pass
        // action into the same pipeline as other controller decisions.
        lastActionReceived = GameAction.CreatePass(SlotOwner.Player1);
    }

    void OnToggleLogClicked()
    {
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ToggleLogPanel();
        }
    }

    void BeginSetup()
    {
        isGameOver = false;
        OnGameOverChanged?.Invoke(isGameOver);

        // Seed already set; initialize round/era then move to Draw
        currentRound = 1;
        currentEra = GetEraForRound(currentRound);
        currentPhase = GamePhase.Draw;
        OnRoundChanged?.Invoke(currentRound, currentEra);
        OnPhaseChanged?.Invoke(currentPhase);
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
        OnPhaseChanged?.Invoke(currentPhase);
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
    }

    IEnumerator PlacePhaseCoroutine()
    {
        while (!BothPlayersFinished())
        {
            if (OwnerFinished(currentPlaceTurnOwner))
            {
                currentPlaceTurnOwner = Opponent(currentPlaceTurnOwner);
                OnAwaitingTurnOwnerChanged?.Invoke(null);
                continue;
            }

            yield return StartCoroutine(ExecuteTurn(currentPlaceTurnOwner));
            currentPlaceTurnOwner = Opponent(currentPlaceTurnOwner);
        }

        awaitingTurnOwner = null;
        OnAwaitingTurnOwnerChanged?.Invoke(awaitingTurnOwner);
        placePhaseRoutine = null;
        currentPhase = GamePhase.Resolve;
        OnPhaseChanged?.Invoke(currentPhase);
        // Hand off to resolve intro + resolution coroutine.
        yield return StartCoroutine(BeginResolve());
    }

    IEnumerator ExecuteTurn(SlotOwner owner)
    {
        if (OwnerFinished(owner))
            yield break;

        awaitingTurnOwner = owner;
        OnAwaitingTurnOwnerChanged?.Invoke(awaitingTurnOwner);
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

            // Process the received action (Play, Pass, PlayEffect, etc.)
            GameAction action = lastActionReceived;
            lastActionReceived = null;

            yield return StartCoroutine(ProcessReceivedAction(action));

            // For this implementation, once a non-null action is processed
            // we consider the single turn slot finished (turns alternate).
            // Note: If validation fails, ProcessReceivedAction is responsible
            // for cleaning up / unlocking the turn as needed.
            turnSlotFinished = true;
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

                // If this is a manual-selection confirmation for the local player,
                // we already validated rules and spent momentum in
                // TryBeginManualEffectSelection. We can bypass the normal
                // TryPlayEffectCard pipeline and go straight to applying the
                // effect using the chosen targets.
                bool isManualConfirmForLocalPlayer =
                    ec.requiresManualSelection
                    && action.owner == SlotOwner.Player1
                    && ManualEffectSelectionController.Instance != null
                    && ManualEffectSelectionController.Instance.IsConfirming(ec, action.owner);

                if (isManualConfirmForLocalPlayer)
                {
                    if (ManualEffectSelectionController.Instance != null)
                    {
                        ManualEffectSelectionController.Instance.ClearSelection();
                    }

                    // Wait for the effect routine to complete before ending the turn
                    yield return StartCoroutine(PlayEffectCardRoutine(ec, action.owner, targets));

                    if (action.owner != SlotOwner.Player1)
                    {
                        AIManager.Instance.RemoveCardFromHand(action.cardId);
                    }

                    break;
                }

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
                    // Wait for the effect routine to complete before ending the turn
                    yield return StartCoroutine(PlayEffectCardRoutine(ec, action.owner, targets));

                    if (action.owner != SlotOwner.Player1)
                    {
                        AIManager.Instance.RemoveCardFromHand(action.cardId);
                    }
                }
                else
                {
                    Debug.LogWarning($"GameManager: Effect action failed: {failureReason}");
                    if (action.owner == SlotOwner.Player1)
                        p1ActionLocked = false;
                    CompleteTurnAction(action.owner); // Unlock turn
                }
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
        FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(owner)}: Your move");

        if (owner == SlotOwner.Player1)
        {
            FeedbackManager.Instance?.ShowGlobalAlert("Your Turn", GameColorPalette.AlertInfo);
        }
    }

    void UpdateEndTurnButtonState()
    {
        // Intentionally left empty – end-turn button state is now managed
        // entirely by GameHUDController in response to GameManager events.
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
            OnAwaitingTurnOwnerChanged?.Invoke(awaitingTurnOwner);
        }
    }

    void CompleteTurnAction(SlotOwner owner)
    {
        if (awaitingTurnOwner.HasValue && awaitingTurnOwner.Value == owner)
        {
            awaitingTurnOwner = null;
            OnAwaitingTurnOwnerChanged?.Invoke(awaitingTurnOwner);
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

    // Manual effect selection UI has been moved to ManualEffectSelectionController.

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
            // Preview-only rules check; does not spend momentum.
            if (!CanPlayEffectCardPreview(card, owner, out failureReason))
                return false;
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

    // Manual effect selection flow has been extracted to ManualEffectSelectionController.

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
        OnPhaseChanged?.Invoke(currentPhase);
        BeginEndRound();
    }

    void BeginEndRound()
    {
        // After resolution, advance round/era and prepare next round
        Era previousEra = currentEra;
        currentRound = Mathf.Max(1, currentRound + 1);
        currentEra = GetEraForRound(currentRound);
        OnRoundChanged?.Invoke(currentRound, currentEra);

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
        OnPhaseChanged?.Invoke(currentPhase);
        BeginDraw();
    }

    private IEnumerator HandleGameOverExtinction()
    {
        isGameOver = true;
        OnGameOverChanged?.Invoke(isGameOver);

        currentPhase = GamePhase.End;
        OnPhaseChanged?.Invoke(currentPhase);

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

        // Hand off to HUD to display the final game-over screen.
        OnShowGameOverRequested?.Invoke();

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
        OnMomentumChanged?.Invoke(currentEra, p1Momentum, p2Momentum);
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
            failureReason = "You have already taken an action. Wait for the other player.";
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

        // For the local player, also prevent queuing multiple actions while a previous
        // card preview is still resolving.
        if (owner == SlotOwner.Player1 && p1ActionLocked)
        {
            failureReason = "You have already taken an action. Wait for the other player.";
            return false;
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
