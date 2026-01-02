using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple disconnect overlay that shows when connection is lost.
/// After a timeout, returns player to lobby where they can wait for opponent to rejoin.
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
    private Button returnToLobbyButton;

    [SerializeField]
    private TextMeshProUGUI buttonText;

    [Header("Settings")]
    [SerializeField]
    [Tooltip("Seconds before auto-return to lobby (only on hard transport disconnect).")]
    private float timeoutSeconds = 30f;

    [SerializeField]
    [Tooltip("Minimum seconds before Return/Forfeit button becomes clickable.")]
    private float minSecondsBeforeReturn = 10f;

    [SerializeField]
    [Tooltip(
        "If true, auto-return only happens on hard transport disconnect. Soft disconnects (message timeout) only enable the forfeit button."
    )]
    private bool onlyAutoReturnOnHardDisconnect = true;

    private bool _isShowing;
    private float _elapsedTime;
    private Coroutine _countdownCoroutine;
    private bool _isHardDisconnect;

    // Tracks whether we've successfully subscribed to NetworkSyncValidator events.
    private bool _subscribedToValidator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Validate overlayRoot isn't this GameObject
        if (overlayRoot == gameObject)
        {
            Debug.LogError(
                "[DisconnectOverlay] overlayRoot should be a CHILD object, not this GameObject!"
            );
        }

        Hide();

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeFromValidator();
    }

    private void OnEnable()
    {
        // Attempt initial subscription to disconnect events. If the validator
        // does not exist yet (e.g., different script execution order), Update()
        // will keep trying until it appears.
        TrySubscribeToValidator();
    }

    private void OnDisable()
    {
        UnsubscribeFromValidator();
    }

    private void Update()
    {
        // If we weren't able to subscribe in OnEnable because the validator
        // didn't exist yet, keep trying until it does.
        if (!_subscribedToValidator)
        {
            TrySubscribeToValidator();
        }
    }

    private void TrySubscribeToValidator()
    {
        var validator = NetworkSyncValidator.Instance;
        if (validator == null)
            return;

        // Ensure we don't double-subscribe if this is called multiple times.
        validator.OnPeerDisconnected -= HandlePeerDisconnected;
        validator.OnPeerReconnected -= HandlePeerReconnected;
        validator.OnPeerDisconnected += HandlePeerDisconnected;
        validator.OnPeerReconnected += HandlePeerReconnected;

        _subscribedToValidator = true;
        Debug.Log("[DisconnectOverlay] Subscribed to NetworkSyncValidator events.");
    }

    private void UnsubscribeFromValidator()
    {
        if (!_subscribedToValidator)
            return;

        var validator = NetworkSyncValidator.Instance;
        if (validator != null)
        {
            validator.OnPeerDisconnected -= HandlePeerDisconnected;
            validator.OnPeerReconnected -= HandlePeerReconnected;
        }
        _subscribedToValidator = false;
    }

    /// <summary>
    /// Called when peer appears disconnected.
    /// </summary>
    /// <param name="isHardDisconnect">True if transport layer confirmed disconnect,
    /// false if just message timeout (peer may be backgrounded, not truly gone).</param>
    private void HandlePeerDisconnected(bool isHardDisconnect)
    {
        _isHardDisconnect = isHardDisconnect;
        string type = isHardDisconnect ? "HARD" : "SOFT (message timeout)";
        Debug.Log($"[DisconnectOverlay] Peer disconnected ({type}) - showing overlay.");
        Show();
    }

    private void HandlePeerReconnected()
    {
        Debug.Log("[DisconnectOverlay] Peer reconnected - hiding overlay.");
        Hide();
    }

    public void Show()
    {
        if (_isShowing)
            return;

        _isShowing = true;
        _elapsedTime = 0f;

        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        UpdateUI();

        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);
        _countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    public void Hide()
    {
        _isShowing = false;

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    private IEnumerator CountdownCoroutine()
    {
        // For soft disconnects (message timeout), don't auto-return.
        // Only auto-return on hard transport disconnects.
        bool shouldAutoReturn = _isHardDisconnect || !onlyAutoReturnOnHardDisconnect;

        if (shouldAutoReturn)
        {
            // Hard disconnect: countdown and auto-return
            while (_elapsedTime < timeoutSeconds)
            {
                _elapsedTime += Time.unscaledDeltaTime;
                UpdateUI();
                yield return null;
            }

            Debug.Log("[DisconnectOverlay] Timeout reached, returning to lobby.");
            ReturnToLobby();
        }
        else
        {
            // Soft disconnect: just keep updating UI, no auto-return
            Debug.Log("[DisconnectOverlay] Soft disconnect - waiting for reconnect or forfeit.");
            while (_isShowing)
            {
                _elapsedTime += Time.unscaledDeltaTime;
                UpdateUI();
                yield return null;
            }
        }
    }

    private void UpdateUI()
    {
        float remaining = Mathf.Max(0, timeoutSeconds - _elapsedTime);
        bool canReturn = _elapsedTime >= minSecondsBeforeReturn;
        bool shouldAutoReturn = _isHardDisconnect || !onlyAutoReturnOnHardDisconnect;

        if (statusText != null)
        {
            if (shouldAutoReturn && remaining <= 0)
            {
                statusText.text = "Connection Lost\n\nReturning to lobby...";
            }
            else if (shouldAutoReturn)
            {
                statusText.text =
                    $"Connection Lost\n\nReturning to lobby in {remaining:0}s\n\nYou can rejoin from there.";
            }
            else
            {
                // Soft disconnect - peer may be backgrounded/AFK
                statusText.text =
                    "Waiting for Opponent...\n\nThey may be temporarily away.\n\nClick Forfeit to leave.";
            }
        }

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.interactable = canReturn;
        }

        if (buttonText != null)
        {
            if (canReturn)
            {
                buttonText.text = shouldAutoReturn ? "Return Now" : "Forfeit Match";
            }
            else
            {
                float wait = minSecondsBeforeReturn - _elapsedTime;
                buttonText.text = $"Wait {wait:0}s...";
            }
        }
    }

    private void OnReturnToLobbyClicked()
    {
        if (_elapsedTime < minSecondsBeforeReturn)
            return;

        Debug.Log("[DisconnectOverlay] User clicked return to lobby.");
        ReturnToLobby();
    }

    private void ReturnToLobby()
    {
        Hide();

        // If we were in a Steam lobby for this match, leave it so DeckHub
        // no longer considers us "in a lobby" and can re-enable host actions
        // (Create Lobby / Quickplay) after we return.
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }

        // Clear network session
        NetworkSessionStore.Clear();

        // Mark game as over so GameManager doesn't interfere
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isGameOver = true;
        }

        Debug.Log("[DisconnectOverlay] Returning to DeckHub...");

        // Return to deck hub
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadScene("DeckHubScene");
        }
        else
        {
            SceneManager.LoadScene("DeckHubScene");
        }
    }
}
