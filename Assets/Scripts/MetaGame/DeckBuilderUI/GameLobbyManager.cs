using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Manager for the in-game lobby (Canvas_Lobby) shown from DeckHub.
/// It displays host/guest info, ready states, Steam avatars, and manages
/// the Ready/Unready and Leave Lobby buttons, synchronising state via SteamLobbyManager.
/// </summary>
public class GameLobbyManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private TextMeshProUGUI lobbyStatusText;

    [SerializeField]
    private TextMeshProUGUI hostNameText;

    [SerializeField]
    private TextMeshProUGUI peerNameText;

    [SerializeField]
    private TextMeshProUGUI hostReadyIndicator;

    [SerializeField]
    private TextMeshProUGUI guestReadyIndicator;

    [Header("Avatar Images")]
    [SerializeField]
    private RawImage hostImage;

    [SerializeField]
    private RawImage peerImage;

    [Header("Buttons")]
    [SerializeField]
    private Button readyButton;

    [SerializeField]
    private TextMeshProUGUI readyButtonLabel;

    [SerializeField]
    private Button leaveLobbyButton;

    [Tooltip("Button to open the Steam invite overlay (host only).")]
    [SerializeField]
    private Button inviteButton;

    [Header("Visual Feedback")]
    [SerializeField]
    private Color readyColor = new Color(0.2f, 0.8f, 0.2f);

    [SerializeField]
    private Color notReadyColor = new Color(0.8f, 0.2f, 0.2f);

    [SerializeField]
    private Color waitingColor = new Color(0.5f, 0.5f, 0.5f);

    private bool _isReady;
    private SteamLobbyManager _lobby;

    // Track which avatars we've already loaded to avoid redundant fetches
    private SteamId _loadedHostAvatarId;
    private SteamId _loadedGuestAvatarId;

    // Throttle avatar fetch attempts (Steam may return empty until it has cached the image)
    private float _nextAvatarRefreshTime;
    private const float AvatarRetryIntervalSeconds = 0.5f;

    private void Awake()
    {
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        if (inviteButton != null)
            inviteButton.onClick.AddListener(OnInviteClicked);
    }

    private void OnDestroy()
    {
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnReadyClicked);
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.RemoveListener(OnLeaveLobbyClicked);
        if (inviteButton != null)
            inviteButton.onClick.RemoveListener(OnInviteClicked);

        UnsubscribeFromEvents();
    }

    private void OnEnable()
    {
        _lobby = SteamLobbyManager.Instance;
        _isReady = false;
        _loadedHostAvatarId = default;
        _loadedGuestAvatarId = default;

        // Reset all UI elements to clean state before loading new lobby data
        ResetUIToDefaults();
        UpdateReadyLabel();

        // Subscribe to events
        SubscribeToEvents();

        // Prime UI from current lobby state if available
        if (_lobby != null && _lobby.IsInLobby)
            _lobby.RefreshLobbyData();

        UpdateUIDisplay();
        LoadAvatarsAsync();

        // Allow immediate retry attempts while avatars are blank
        _nextAvatarRefreshTime = 0f;
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Resets all UI elements to a clean default state.
    /// Called when entering a new lobby to clear stale data from previous lobbies.
    /// </summary>
    private void ResetUIToDefaults()
    {
        // Clear text fields
        if (lobbyStatusText != null)
            lobbyStatusText.text = "Connecting...";
        if (hostNameText != null)
            hostNameText.text = "Host";
        if (peerNameText != null)
            peerNameText.text = "Waiting...";

        // Reset ready indicators to waiting state
        if (hostReadyIndicator != null)
        {
            hostReadyIndicator.text = "—";
            hostReadyIndicator.color = waitingColor;
        }
        if (guestReadyIndicator != null)
        {
            guestReadyIndicator.text = "—";
            guestReadyIndicator.color = waitingColor;
        }

        // Clear avatar textures
        if (hostImage != null)
            hostImage.texture = null;
        if (peerImage != null)
            peerImage.texture = null;
    }

    private void SubscribeToEvents()
    {
        if (_lobby != null)
        {
            _lobby.LobbyEntered += OnLobbyEntered;
            _lobby.LobbyDataChanged += OnLobbyDataChanged;
            _lobby.LobbyLeft += OnLobbyLeft;
            _lobby.BothPlayersReady += OnBothPlayersReady;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_lobby != null)
        {
            _lobby.LobbyEntered -= OnLobbyEntered;
            _lobby.LobbyDataChanged -= OnLobbyDataChanged;
            _lobby.LobbyLeft -= OnLobbyLeft;
            _lobby.BothPlayersReady -= OnBothPlayersReady;
        }
    }

    private void OnLobbyEntered()
    {
        // Lobby is now ready - refresh data and update UI
        if (_lobby != null && _lobby.IsInLobby)
        {
            _lobby.RefreshLobbyData();
        }
        UpdateUIDisplay();
        LoadAvatarsAsync();
    }

    private void Update()
    {
        if (_lobby == null)
            _lobby = SteamLobbyManager.Instance;

        // Always update the UI display to reflect current state
        UpdateUIDisplay();

        // Retry avatar loading if needed (Steam may not have the image ready on first attempt)
        if (
            _lobby != null
            && _lobby.IsInLobby
            && Time.unscaledTime >= _nextAvatarRefreshTime
            && (
                (hostImage != null && hostImage.texture == null)
                || (peerImage != null && peerImage.texture == null)
            )
        )
        {
            _nextAvatarRefreshTime = Time.unscaledTime + AvatarRetryIntervalSeconds;
            LoadAvatarsAsync();
        }
    }

    private void OnLobbyDataChanged()
    {
        // When lobby data changes remotely, refresh from the lobby
        if (_lobby != null && _lobby.IsInLobby)
        {
            _lobby.RefreshLobbyData();
        }
        UpdateUIDisplay();
        LoadAvatarsAsync();
    }

    private void OnLobbyLeft()
    {
        // Reset UI to clean state so next lobby shows fresh
        // Note: DeckHubManager.HandleLobbyLeft() handles hiding the canvas,
        // so we don't call SetActive(false) here to avoid issues with
        // unsubscribing from events mid-invocation
        ResetUIToDefaults();
        _isReady = false;
        UpdateReadyLabel();
    }

    private void OnBothPlayersReady()
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = "Starting match...";
        }
    }

    /// <summary>
    /// Updates the UI display based on current SteamLobbyManager state.
    /// Does NOT refresh lobby data from Steam - that's done via events.
    /// </summary>
    private void UpdateUIDisplay()
    {
        if (_lobby == null)
            _lobby = SteamLobbyManager.Instance;

        if (_lobby == null || !_lobby.IsInLobby)
        {
            // Show a connecting/waiting state instead of leaving stale data
            if (lobbyStatusText != null)
                lobbyStatusText.text = "Connecting to lobby...";
            if (hostNameText != null)
                hostNameText.text = "Host";
            if (peerNameText != null)
                peerNameText.text = "Waiting...";
            UpdateReadyIndicator(hostReadyIndicator, false, false);
            UpdateReadyIndicator(guestReadyIndicator, false, false);
            return;
        }

        // Update status text
        if (lobbyStatusText != null)
        {
            if (!_lobby.HasGuest)
            {
                lobbyStatusText.text = "Waiting for opponent to join...";
            }
            else if (_lobby.HostReady && _lobby.GuestReady)
            {
                lobbyStatusText.text = "Starting match...";
            }
            else if (_lobby.IsHost)
            {
                lobbyStatusText.text = _lobby.GuestReady
                    ? "Opponent is ready! Click READY when you're set."
                    : "Waiting for opponent to ready up...";
            }
            else
            {
                lobbyStatusText.text = _lobby.HostReady
                    ? "Host is ready! Click READY when you're set."
                    : "Waiting for host to ready up...";
            }
        }

        // Update player names
        // Layout is consistent for both players: Host on left, Guest on right
        if (hostNameText != null)
            hostNameText.text = _lobby.HostName ?? "Host";

        if (peerNameText != null)
            peerNameText.text = _lobby.HasGuest ? (_lobby.GuestName ?? "Guest") : "Waiting...";

        // Update ready indicators
        UpdateReadyIndicator(hostReadyIndicator, _lobby.HostReady, true);
        UpdateReadyIndicator(guestReadyIndicator, _lobby.GuestReady, _lobby.HasGuest);

        // Disable ready button if both are already ready
        if (readyButton != null)
        {
            readyButton.interactable = !(_lobby.HostReady && _lobby.GuestReady);
        }

        // Invite button is only visible/interactable for the host
        if (inviteButton != null)
        {
            inviteButton.gameObject.SetActive(_lobby.IsHost);
            inviteButton.interactable = _lobby.IsHost && !_lobby.HasGuest;
        }
    }

    private void UpdateReadyIndicator(TextMeshProUGUI indicator, bool isReady, bool hasPlayer)
    {
        if (indicator == null)
            return;

        if (!hasPlayer)
        {
            indicator.text = "—";
            indicator.color = waitingColor;
        }
        else if (isReady)
        {
            indicator.text = "✓ READY";
            indicator.color = readyColor;
        }
        else
        {
            indicator.text = "✗ NOT READY";
            indicator.color = notReadyColor;
        }
    }

    // -------------------------------------------------------------------------
    // Avatar Loading
    // -------------------------------------------------------------------------

    private async void LoadAvatarsAsync()
    {
        if (_lobby == null || !_lobby.IsInLobby)
            return;

        // Load host avatar
        if (hostImage != null)
        {
            SteamId hostId = GetSteamIdFromString(_lobby.HostId);
            // Only mark as "loaded" once we actually got a texture.
            // Steam can return empty avatar results briefly after joining a lobby.
            bool needsHostAvatar =
                hostId.Value != 0 && (hostImage.texture == null || hostId != _loadedHostAvatarId);

            if (needsHostAvatar)
            {
                var hostAvatar = await SteamFriends.GetLargeAvatarAsync(hostId);
                if (hostAvatar.HasValue)
                {
                    hostImage.texture = CreateTextureFromImage(hostAvatar.Value);
                    _loadedHostAvatarId = hostId;
                }
            }
        }

        // Load guest avatar (peer slot always shows the guest, regardless of who's viewing)
        if (peerImage != null)
        {
            SteamId guestId = GetSteamIdFromString(_lobby.GuestId);

            bool needsGuestAvatar =
                guestId.Value != 0
                && (peerImage.texture == null || guestId != _loadedGuestAvatarId);

            if (needsGuestAvatar)
            {
                var guestAvatar = await SteamFriends.GetLargeAvatarAsync(guestId);
                if (guestAvatar.HasValue)
                {
                    peerImage.texture = CreateTextureFromImage(guestAvatar.Value);
                    _loadedGuestAvatarId = guestId;
                }
            }
            else if (guestId.Value == 0)
            {
                // No guest yet, clear the image
                peerImage.texture = null;
            }
        }
    }

    private SteamId GetSteamIdFromString(string idString)
    {
        if (string.IsNullOrEmpty(idString))
            return default;

        if (ulong.TryParse(idString, out ulong id))
            return id;

        return default;
    }

    private Texture2D CreateTextureFromImage(Steamworks.Data.Image image)
    {
        var texture = new Texture2D(
            (int)image.Width,
            (int)image.Height,
            TextureFormat.RGBA32,
            false
        );
        texture.LoadRawTextureData(image.Data);
        texture.Apply();

        // Steam images are upside down, flip them
        return FlipTextureVertically(texture);
    }

    private Texture2D FlipTextureVertically(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        var flipped = new Texture2D(width, height, original.format, false);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flipped.SetPixel(x, height - 1 - y, original.GetPixel(x, y));
            }
        }

        flipped.Apply();
        Destroy(original);
        return flipped;
    }

    // -------------------------------------------------------------------------
    // Button Handlers
    // -------------------------------------------------------------------------

    private void OnReadyClicked()
    {
        _isReady = !_isReady;
        UpdateReadyLabel();

        // Always use the current singleton (avoid stale cached reference)
        _lobby = SteamLobbyManager.Instance;

        if (_lobby != null && _lobby.IsInLobby)
            _lobby.SetLocalReady(_isReady);
    }

    private void OnLeaveLobbyClicked()
    {
        // Always refresh reference (avoid stale cached reference)
        _lobby = SteamLobbyManager.Instance;

        if (_lobby != null)
            _lobby.LeaveLobby();

        ResetUIToDefaults();
        _isReady = false;
        UpdateReadyLabel();
    }

    private void OnInviteClicked()
    {
        if (_lobby != null && _lobby.IsInLobby && _lobby.IsHost)
        {
            _lobby.OpenInviteOverlay();
        }
    }

    private void UpdateReadyLabel()
    {
        if (readyButtonLabel != null)
            readyButtonLabel.text = _isReady ? "UNREADY" : "READY";
    }
}
