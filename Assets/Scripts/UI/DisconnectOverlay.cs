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
    private float timeoutSeconds = 30f;

    [SerializeField]
    private float minSecondsBeforeReturn = 10f;

    private bool _isShowing;
    private float _elapsedTime;
    private Coroutine _countdownCoroutine;

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
    }

    private void OnEnable()
    {
        // Subscribe to disconnect events
        if (NetworkSyncValidator.Instance != null)
        {
            NetworkSyncValidator.Instance.OnPeerDisconnected += HandlePeerDisconnected;
            NetworkSyncValidator.Instance.OnPeerReconnected += HandlePeerReconnected;
            Debug.Log("[DisconnectOverlay] Subscribed to NetworkSyncValidator events.");
        }
        else
        {
            Debug.LogWarning(
                "[DisconnectOverlay] NetworkSyncValidator.Instance is null on enable."
            );
        }
    }

    private void OnDisable()
    {
        if (NetworkSyncValidator.Instance != null)
        {
            NetworkSyncValidator.Instance.OnPeerDisconnected -= HandlePeerDisconnected;
            NetworkSyncValidator.Instance.OnPeerReconnected -= HandlePeerReconnected;
        }
    }

    private void HandlePeerDisconnected()
    {
        Debug.Log("[DisconnectOverlay] Peer disconnected - showing overlay.");
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
        while (_elapsedTime < timeoutSeconds)
        {
            _elapsedTime += Time.unscaledDeltaTime;
            UpdateUI();
            yield return null;
        }

        // Timeout reached - return to lobby
        Debug.Log("[DisconnectOverlay] Timeout reached, returning to lobby.");
        ReturnToLobby();
    }

    private void UpdateUI()
    {
        float remaining = Mathf.Max(0, timeoutSeconds - _elapsedTime);
        bool canReturn = _elapsedTime >= minSecondsBeforeReturn;

        if (statusText != null)
        {
            if (remaining <= 0)
            {
                statusText.text = "Connection Lost\n\nReturning to lobby...";
            }
            else
            {
                statusText.text =
                    $"Connection Lost\n\nReturning to lobby in {remaining:0}s\n\nYou can rejoin from there.";
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
                buttonText.text = "Return to Lobby";
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
