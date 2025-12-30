using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only controller for the in-game HUD:
/// - Phase / round labels
/// - Momentum labels
/// - End Turn button state + label
/// - Log toggle button
///
/// It listens to events from GameManager and updates UI accordingly,
/// without mutating core game state.
/// </summary>
public class GameHUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameManager gameManager;

    [Header("Phase & Round")]
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI roundText;

    [Header("End Turn")]
    public Button endTurnButton;
    public TextMeshProUGUI endTurnLabel;
    public string endTurnIdleText = "End Turn";
    public string endTurnBusyText = "Resolving...";

    [Header("Momentum")]
    public TextMeshProUGUI localMomentumLabel;
    public TextMeshProUGUI opponentMomentumLabel;

    [Header("Log")]
    public Button toggleLogButton;

    [Header("Canvas Groups")]
    public CanvasGroup mainCanvasGroup;
    public CanvasGroup worldCanvasGroup;
    public CanvasGroup gameOverCanvasGroup;

    [Header("Game Over UI")]
    public TextMeshProUGUI gameOverOutcomeText;

    [Tooltip("Left score display - always shows the local player's score.")]
    public TextMeshProUGUI localScoreText;

    [Tooltip("Right score display - always shows the opponent's score.")]
    public TextMeshProUGUI opponentScoreText;
    public float gameOverFadeDuration = 0.75f;

    [Header("Opponent Deck (Networked)")]
    [Tooltip("Label showing the opponent's current hand size in networked games.")]
    public TextMeshProUGUI opponentHandLabel;

    [Tooltip("Label showing the opponent's remaining deck size in networked games.")]
    public TextMeshProUGUI opponentDeckLabel;

    // Cached state from GameManager events
    private GamePhase _currentPhase;
    private int _currentRound;
    private Era _currentEra;
    private SlotOwner? _awaitingOwner;
    private bool _isGameOver;
    private int _p1Momentum;
    private int _p2Momentum;

    private OpponentDeckTracker _opponentTracker;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }
    }

    private void OnEnable()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("GameHUDController: No GameManager reference found.");
            return;
        }

        gameManager.OnPhaseChanged += HandlePhaseChanged;
        gameManager.OnRoundChanged += HandleRoundChanged;
        gameManager.OnAwaitingTurnOwnerChanged += HandleAwaitingTurnOwnerChanged;
        gameManager.OnGameOverChanged += HandleGameOverChanged;
        gameManager.OnMomentumChanged += HandleMomentumChanged;
        gameManager.OnShowGameOverRequested += HandleShowGameOverRequested;

        // Wire up buttons
        if (endTurnButton != null)
        {
            if (endTurnLabel == null)
            {
                endTurnLabel = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            endTurnButton.onClick.AddListener(gameManager.OnEndTurnClicked);
        }

        if (toggleLogButton != null)
        {
            toggleLogButton.onClick.AddListener(OnToggleLogClicked);
        }

        // Subscribe to opponent deck tracker in networked games
        if (NetworkSessionStore.IsNetworkedGame)
        {
            _opponentTracker = OpponentDeckTracker.Instance;
            if (_opponentTracker != null)
            {
                _opponentTracker.OnStateChanged += HandleOpponentDeckChanged;
            }
        }

        InitializeCanvasVisibility();

        // Initialize HUD from current GameManager state (in case we enable mid-game).
        _currentPhase = gameManager.currentPhase;
        _currentRound = gameManager.currentRound;
        _currentEra = gameManager.currentEra;
        _isGameOver = gameManager.isGameOver;
        _awaitingOwner = null; // will be updated via event when a turn starts
        _p1Momentum = gameManager.GetMomentum(SlotOwner.Player1);
        _p2Momentum = gameManager.GetMomentum(SlotOwner.Player2);

        RefreshAll();
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnPhaseChanged -= HandlePhaseChanged;
            gameManager.OnRoundChanged -= HandleRoundChanged;
            gameManager.OnAwaitingTurnOwnerChanged -= HandleAwaitingTurnOwnerChanged;
            gameManager.OnGameOverChanged -= HandleGameOverChanged;
            gameManager.OnMomentumChanged -= HandleMomentumChanged;
            gameManager.OnShowGameOverRequested -= HandleShowGameOverRequested;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(gameManager.OnEndTurnClicked);
        }

        if (toggleLogButton != null)
        {
            toggleLogButton.onClick.RemoveListener(OnToggleLogClicked);
        }

        if (_opponentTracker != null)
        {
            _opponentTracker.OnStateChanged -= HandleOpponentDeckChanged;
            _opponentTracker = null;
        }
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        _currentPhase = phase;
        UpdatePhaseStatusText();
        UpdateEndTurnButtonState();
    }

    private void HandleRoundChanged(int round, Era era)
    {
        _currentRound = round;
        _currentEra = era;
        UpdatePhaseLabel();
    }

    private void HandleAwaitingTurnOwnerChanged(SlotOwner? owner)
    {
        _awaitingOwner = owner;
        UpdatePhaseStatusText();
        UpdateEndTurnButtonState();
    }

    private void HandleGameOverChanged(bool isOver)
    {
        _isGameOver = isOver;
        UpdatePhaseStatusText();
        UpdateEndTurnButtonState();
    }

    private void HandleMomentumChanged(Era era, int p1, int p2)
    {
        _currentEra = era;
        _p1Momentum = p1;
        _p2Momentum = p2;
        UpdateMomentumUI();
        UpdateEndTurnButtonState();
    }

    private void HandleShowGameOverRequested()
    {
        // Use network-aware local/opponent scores
        int localScore = ScoreManager.LocalScore;
        int opponentScore = ScoreManager.OpponentScore;

        // Determine outcome colors
        Color winColor = GameColorPalette.TextPositive;
        Color loseColor = GameColorPalette.TextNegative;
        Color drawColor = GameColorPalette.TextNeutral;

        if (gameOverOutcomeText != null)
        {
            if (localScore > opponentScore)
            {
                gameOverOutcomeText.text = "Victory";
                gameOverOutcomeText.color = winColor;
            }
            else if (localScore < opponentScore)
            {
                gameOverOutcomeText.text = "Defeat";
                gameOverOutcomeText.color = loseColor;
            }
            else
            {
                gameOverOutcomeText.text = "Draw";
                gameOverOutcomeText.color = drawColor;
            }
        }

        // Local score on left, opponent score on right
        if (localScoreText != null)
        {
            localScoreText.text = localScore.ToString();
            if (localScore > opponentScore)
                localScoreText.color = winColor;
            else if (localScore < opponentScore)
                localScoreText.color = loseColor;
            else
                localScoreText.color = drawColor;
        }

        if (opponentScoreText != null)
        {
            opponentScoreText.text = opponentScore.ToString();
            if (opponentScore > localScore)
                opponentScoreText.color = winColor;
            else if (opponentScore < localScore)
                opponentScoreText.color = loseColor;
            else
                opponentScoreText.color = drawColor;
        }

        if (isActiveAndEnabled)
        {
            StartCoroutine(FadeToGameOverCoroutine());
        }
    }

    private void OnToggleLogClicked()
    {
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ToggleLogPanel();
        }
    }

    private void RefreshAll()
    {
        UpdatePhaseLabel();
        UpdatePhaseStatusText();
        UpdateMomentumUI();
        UpdateEndTurnButtonState();
        RefreshOpponentDeckUI();
    }

    private void HandleOpponentDeckChanged()
    {
        RefreshOpponentDeckUI();
    }

    /// <summary>
    /// Updates opponent hand/deck UI in networked games, if labels are assigned.
    /// </summary>
    private void RefreshOpponentDeckUI()
    {
        if (!NetworkSessionStore.IsNetworkedGame)
            return;

        var tracker = OpponentDeckTracker.Instance;
        if (tracker == null)
            return;

        if (opponentHandLabel != null)
        {
            opponentHandLabel.text = tracker.HandSize.ToString();
        }

        if (opponentDeckLabel != null)
        {
            opponentDeckLabel.text = tracker.DeckRemaining.ToString();
        }
    }

    private void InitializeCanvasVisibility()
    {
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
        }
        else
        {
            // Non-constructed startup paths now also begin with the gameplay UI
            // visible, since the draft flow has been moved entirely into the
            // DeckHubScene.
            SetCanvasGroupVisible(mainCanvasGroup, true);
            SetCanvasGroupVisible(worldCanvasGroup, true);
            if (worldCanvasGroup != null)
                worldCanvasGroup.gameObject.SetActive(true);
            SetCanvasGroupVisible(gameOverCanvasGroup, false);
        }
    }

    private void UpdatePhaseLabel()
    {
        if (roundText == null)
            return;

        string eraLabel = _currentEra.ToString();
        roundText.text = $"Round {_currentRound} – {eraLabel}";
    }

    private void UpdatePhaseStatusText()
    {
        if (phaseText == null)
            return;

        if (_isGameOver)
        {
            phaseText.text = "Game Over";
            return;
        }

        switch (_currentPhase)
        {
            case GamePhase.Setup:
                phaseText.text = "Setup";
                break;
            case GamePhase.Draw:
                phaseText.text = "Draw";
                break;
            case GamePhase.Place:
                if (_awaitingOwner.HasValue)
                {
                    // Use network-aware check for whose turn it is
                    bool isLocalPlayerTurn = NetworkRoleHelper.IsLocalPlayer(_awaitingOwner.Value);
                    phaseText.text = isLocalPlayerTurn ? "Your Turn" : "Opponent Turn";
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

    private void UpdateEndTurnButtonState()
    {
        if (endTurnButton == null || endTurnLabel == null)
            return;

        if (_isGameOver)
        {
            endTurnButton.interactable = false;
            endTurnLabel.text = "Game Over";
            return;
        }

        // Use network-aware check: is it the local player's turn and do they have momentum?
        bool isLocalPlayerTurn =
            _awaitingOwner.HasValue && NetworkRoleHelper.IsLocalPlayer(_awaitingOwner.Value);
        int localMomentum =
            NetworkRoleHelper.LocalRole == SlotOwner.Player1 ? _p1Momentum : _p2Momentum;

        bool playerTurnActive =
            _currentPhase == GamePhase.Place && isLocalPlayerTurn && localMomentum > 0;

        endTurnButton.interactable = playerTurnActive;

        if (_currentPhase == GamePhase.Resolve)
        {
            endTurnLabel.text = string.IsNullOrEmpty(endTurnBusyText)
                ? "Resolving..."
                : endTurnBusyText;
        }
        else if (playerTurnActive)
        {
            endTurnLabel.text = "Pass";
        }
        else if (_currentPhase == GamePhase.Place)
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

    private void UpdateMomentumUI()
    {
        int perRound = gameManager != null ? gameManager.GetMomentumForEra(_currentEra) : 0;

        // Map momentum to local/opponent based on network role
        int localMomentum =
            NetworkRoleHelper.LocalRole == SlotOwner.Player1 ? _p1Momentum : _p2Momentum;
        int opponentMomentum =
            NetworkRoleHelper.LocalRole == SlotOwner.Player1 ? _p2Momentum : _p1Momentum;

        if (localMomentumLabel != null)
        {
            localMomentumLabel.text = $"{localMomentum} / {perRound}";
        }

        if (opponentMomentumLabel != null)
        {
            opponentMomentumLabel.text = $"{opponentMomentum} / {perRound}";
        }
    }

    private void SetCanvasGroupVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null)
            return;
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }

    private void SetCanvasGroupAlpha(CanvasGroup cg, float alpha)
    {
        if (cg == null)
            return;
        cg.alpha = alpha;
    }

    private System.Collections.IEnumerator FadeToGameOverCoroutine()
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
}
