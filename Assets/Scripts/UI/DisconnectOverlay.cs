using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI overlay that displays when the opponent disconnects.
/// Shows a waiting message with timer and a forfeit button.
/// </summary>
public class DisconnectOverlay : MonoBehaviour
{
    public static DisconnectOverlay Instance { get; private set; }

    [Header("UI References")]
    [SerializeField]
    private GameObject overlayRoot;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private Button forfeitButton;

    [SerializeField]
    private TextMeshProUGUI forfeitButtonText;

    [Header("Settings")]
    [SerializeField]
    private float minSecondsBeforeForfeit = 10f;

    [SerializeField]
    private string waitingMessage = "Connection Lost\n\nWaiting for opponent...";

    [SerializeField]
    private string waitingWithTimerMessage = "Connection Lost\n\nWaiting for opponent...\n\n{0}s";

    [SerializeField]
    private string forfeitAvailableMessage =
        "Connection Lost\n\nOpponent appears disconnected.\n\n{0}s";

    private bool _isShowing;
    private float _waitingSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Start hidden
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        // Setup forfeit button
        if (forfeitButton != null)
        {
            forfeitButton.onClick.AddListener(OnForfeitClicked);
            forfeitButton.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // Subscribe to NetworkSyncValidator events
        if (NetworkSyncValidator.Instance != null)
        {
            NetworkSyncValidator.Instance.OnPeerDisconnected += Show;
            NetworkSyncValidator.Instance.OnPeerReconnected += Hide;
            NetworkSyncValidator.Instance.OnWaitingForPeer += UpdateWaitingTime;
        }
    }

    private void OnEnable()
    {
        if (NetworkSyncValidator.Instance != null)
        {
            NetworkSyncValidator.Instance.OnPeerDisconnected -= Show;
            NetworkSyncValidator.Instance.OnPeerReconnected -= Hide;
            NetworkSyncValidator.Instance.OnWaitingForPeer -= UpdateWaitingTime;

            NetworkSyncValidator.Instance.OnPeerDisconnected += Show;
            NetworkSyncValidator.Instance.OnPeerReconnected += Hide;
            NetworkSyncValidator.Instance.OnWaitingForPeer += UpdateWaitingTime;
        }
    }

    private void OnDisable()
    {
        if (NetworkSyncValidator.Instance != null)
        {
            NetworkSyncValidator.Instance.OnPeerDisconnected -= Show;
            NetworkSyncValidator.Instance.OnPeerReconnected -= Hide;
            NetworkSyncValidator.Instance.OnWaitingForPeer -= UpdateWaitingTime;
        }
    }

    public void Show()
    {
        if (_isShowing)
            return;

        _isShowing = true;
        _waitingSeconds = 0f;

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
        }

        if (statusText != null)
        {
            statusText.text = waitingMessage;
        }

        if (forfeitButton != null)
        {
            forfeitButton.interactable = false;
        }

        if (forfeitButtonText != null)
        {
            forfeitButtonText.text = $"Wait {minSecondsBeforeForfeit:0}s...";
        }

        Debug.Log("[DisconnectOverlay] Showing disconnect overlay");
    }

    public void Hide()
    {
        if (!_isShowing)
            return;

        _isShowing = false;

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        Debug.Log("[DisconnectOverlay] Hiding disconnect overlay");
    }

    private void UpdateWaitingTime(float seconds)
    {
        _waitingSeconds = seconds;

        if (!_isShowing)
            return;

        bool canForfeit = seconds >= minSecondsBeforeForfeit;

        // Update status text
        if (statusText != null)
        {
            if (canForfeit)
            {
                statusText.text = string.Format(forfeitAvailableMessage, Mathf.FloorToInt(seconds));
            }
            else
            {
                statusText.text = string.Format(waitingWithTimerMessage, Mathf.FloorToInt(seconds));
            }
        }

        // Update forfeit button
        if (forfeitButton != null)
        {
            forfeitButton.interactable = canForfeit;
        }

        if (forfeitButtonText != null)
        {
            if (canForfeit)
            {
                forfeitButtonText.text = "Forfeit Match";
            }
            else
            {
                float remaining = minSecondsBeforeForfeit - seconds;
                forfeitButtonText.text = $"Wait {remaining:0}s...";
            }
        }
    }

    private void OnForfeitClicked()
    {
        Debug.Log("[DisconnectOverlay] Forfeit button clicked");

        // Hide this overlay
        Hide();

        // Tell the validator to handle the forfeit
        NetworkSyncValidator.Instance?.ForfeitMatch();
    }

    /// <summary>
    /// Can be called manually to show the overlay (e.g., for testing).
    /// </summary>
    [ContextMenu("Test Show")]
    public void TestShow()
    {
        Show();
        UpdateWaitingTime(5f);
    }

    /// <summary>
    /// Can be called manually to hide the overlay (e.g., for testing).
    /// </summary>
    [ContextMenu("Test Hide")]
    public void TestHide()
    {
        Hide();
    }
}
