using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum GamePhase { Setup, Draw, Place, Resolve, End }

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

    [Header("Debug")]
    public GamePhase currentPhase = GamePhase.Setup;
    public int rngSeed = 0;
    private System.Random rng;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

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
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (toggleLogButton != null) toggleLogButton.onClick.AddListener(OnToggleLogClicked);
        UpdatePhaseLabel();
        BeginSetup();
    }

    void OnDestroy()
    {
        if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (toggleLogButton != null) toggleLogButton.onClick.RemoveListener(OnToggleLogClicked);
    }

    void OnEndTurnClicked()
    {
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
        currentPhase = (GamePhase)(((int)currentPhase + 1) % System.Enum.GetValues(typeof(GamePhase)).Length);
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
            phaseText.text = $"Phase: {currentPhase}";
    }

    void BeginSetup()
    {
        // Seed already set; move to Draw
        currentPhase = GamePhase.Draw;
        UpdatePhaseLabel();
        BeginDraw();
    }

    void BeginDraw()
    {
        // Simple: draw up to a hand size of 5
        var dm = DeckManager.Instance;
        if (dm != null)
        {
            int toDraw = Mathf.Max(0, 5 - dm.CurrentHandCount());
            for (int i = 0; i < toDraw; i++) dm.DrawCard();
        }
        if (foodPile != null) foodPile.RefillStartOfRound();
        currentPhase = GamePhase.Place;
        UpdatePhaseLabel();
        BeginPlace();
    }

    void BeginPlace()
    {
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

    System.Collections.IEnumerator ResolveRoundCoroutine()
    {
        yield return StartCoroutine(resolutionManager.RevealAndResolveRound());
        currentPhase = GamePhase.End;
        UpdatePhaseLabel();
        BeginEndRound();
    }

    void BeginEndRound()
    {
        // After resolution, prepare next round
        currentPhase = GamePhase.Draw;
        UpdatePhaseLabel();
        BeginDraw();
    }

    public int NextRandomInt(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }
}
