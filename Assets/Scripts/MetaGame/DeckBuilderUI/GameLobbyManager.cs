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

        UpdateReadyLabel();
        UpdateUIDisplay();
        SubscribeToEvents();

        // Load avatars on enable
        LoadAvatarsAsync();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_lobby != null)
        {
            _lobby.LobbyDataChanged += OnLobbyDataChanged;
            _lobby.LobbyLeft += OnLobbyLeft;
            _lobby.BothPlayersReady += OnBothPlayersReady;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_lobby != null)
        {
            _lobby.LobbyDataChanged -= OnLobbyDataChanged;
            _lobby.LobbyLeft -= OnLobbyLeft;
            _lobby.BothPlayersReady -= OnBothPlayersReady;
        }
    }

    private void Update()
    {
        // Only update the UI display, don't refresh lobby data every frame.
        // Lobby data refresh happens via events (OnLobbyDataChanged).
        UpdateUIDisplay();
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
        // Hide the lobby canvas when we leave
        gameObject.SetActive(false);
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
            if (lobbyStatusText != null)
                lobbyStatusText.text = "Not in lobby";
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
        if (hostNameText != null)
            hostNameText.text = _lobby.HostName ?? "Host";

        if (peerNameText != null)
        {
            if (_lobby.IsHost)
                peerNameText.text = _lobby.HasGuest ? (_lobby.GuestName ?? "Guest") : "Waiting...";
            else
                peerNameText.text = _lobby.HostName ?? "Host";
        }

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
            if (hostId.Value != 0 && hostId != _loadedHostAvatarId)
            {
                _loadedHostAvatarId = hostId;
                var hostAvatar = await SteamFriends.GetLargeAvatarAsync(hostId);
                if (hostAvatar.HasValue)
                {
                    hostImage.texture = CreateTextureFromImage(hostAvatar.Value);
                }
            }
        }

        // Load guest/peer avatar
        if (peerImage != null)
        {
            SteamId peerId;
            if (_lobby.IsHost)
            {
                // We're host, peer is guest
                peerId = GetSteamIdFromString(_lobby.GuestId);
            }
            else
            {
                // We're guest, peer is host
                peerId = GetSteamIdFromString(_lobby.HostId);
            }

            if (peerId.Value != 0 && peerId != _loadedGuestAvatarId)
            {
                _loadedGuestAvatarId = peerId;
                var peerAvatar = await SteamFriends.GetLargeAvatarAsync(peerId);
                if (peerAvatar.HasValue)
                {
                    peerImage.texture = CreateTextureFromImage(peerAvatar.Value);
                }
            }
            else if (peerId.Value == 0)
            {
                // No peer yet, clear the image
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

        if (_lobby != null && _lobby.IsInLobby)
        {
            _lobby.SetLocalReady(_isReady);
        }
    }

    private void OnLeaveLobbyClicked()
    {
        if (_lobby != null)
        {
            _lobby.LeaveLobby();
        }

        // Hide the lobby canvas; DeckHubManager is responsible for re-showing
        // the main hub UI.
        gameObject.SetActive(false);
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
