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
    public TextMeshProUGUI phaseText;

    [Header("Debug")]
    public GamePhase currentPhase = GamePhase.Setup;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        Debug.Log("[GameManager] Initialized in Phase: " + currentPhase);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        UpdatePhaseLabel();
    }

    void OnDestroy()
    {
        if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
    }

    void OnEndTurnClicked()
    {
        AdvancePhase();
    }

    void AdvancePhase()
    {
        currentPhase = (GamePhase)(((int)currentPhase + 1) % System.Enum.GetValues(typeof(GamePhase)).Length);
        Debug.Log("[GameManager] New Phase: " + currentPhase);
        UpdatePhaseLabel();
    }

    void UpdatePhaseLabel()
    {
        if (phaseText != null)
            phaseText.text = $"Phase: {currentPhase}";
    }
}
