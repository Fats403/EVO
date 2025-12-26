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
    public TextMeshProUGUI p1MomentumLabel;
    public TextMeshProUGUI p2MomentumLabel;

    [Header("Log")]
    public Button toggleLogButton;

    [Header("Canvas Groups")]
    public CanvasGroup mainCanvasGroup;
    public CanvasGroup worldCanvasGroup;
    public CanvasGroup gameOverCanvasGroup;

    [Header("Game Over UI")]
    public TextMeshProUGUI gameOverOutcomeText;
    public TextMeshProUGUI player1ScoreText;
    public TextMeshProUGUI player2ScoreText;
    public float gameOverFadeDuration = 0.75f;

    // Cached state from GameManager events
    private GamePhase _currentPhase;
    private int _currentRound;
    private Era _currentEra;
    private SlotOwner? _awaitingOwner;
    private bool _isGameOver;
    private int _p1Momentum;
    private int _p2Momentum;

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
        int p1 = ScoreManager.player1;
        int p2 = ScoreManager.player2;

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
    }

    private void InitializeCanvasVisibility()
    {
        bool useConstructed =
            SelectedDeckStore.Mode == GameStartMode.Constructed
            && SelectedDeckStore.HasConstructedDeck;
        bool usingDraft = gameManager != null && gameManager.draftManager != null;

        if (useConstructed)
        {
            // Show gameplay UI immediately; no draft phase.
            SetCanvasGroupVisible(mainCanvasGroup, true);
            SetCanvasGroupVisible(worldCanvasGroup, true);
            if (worldCanvasGroup != null)
                worldCanvasGroup.gameObject.SetActive(true);
            SetCanvasGroupVisible(gameOverCanvasGroup, false);

            // Explicitly hide any draft overlay if the scene still has one.
            if (
                gameManager != null
                && gameManager.draftManager != null
                && gameManager.draftManager.draftCanvasGroup != null
            )
            {
                var cg = gameManager.draftManager.draftCanvasGroup;
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
        else
        {
            // Ensure initial canvas visibility states for draft mode.
            // Hide the main gameplay UI and world canvas while we are in the draft,
            // show them otherwise.
            SetCanvasGroupVisible(mainCanvasGroup, !usingDraft);
            SetCanvasGroupVisible(worldCanvasGroup, !usingDraft);
            if (worldCanvasGroup != null)
                worldCanvasGroup.gameObject.SetActive(!usingDraft);
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
                    phaseText.text =
                        _awaitingOwner.Value == SlotOwner.Player1 ? "Your Turn" : "Player 2 Turn";
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

        bool playerTurnActive =
            _currentPhase == GamePhase.Place
            && _awaitingOwner.HasValue
            && _awaitingOwner.Value == SlotOwner.Player1
            && _p1Momentum > 0;

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
        if (p1MomentumLabel != null)
        {
            int perRound = gameManager != null ? gameManager.GetMomentumForEra(_currentEra) : 0;
            p1MomentumLabel.text = $"{_p1Momentum} / {perRound}";
        }

        if (p2MomentumLabel != null)
        {
            int perRound = gameManager != null ? gameManager.GetMomentumForEra(_currentEra) : 0;
            p2MomentumLabel.text = $"{_p2Momentum} / {perRound}";
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
