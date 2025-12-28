using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

/// <summary>
/// Manages Steam lobby lifecycle and lobby data (names, ready flags, deck identifiers)
/// for the DeckHubScene. This wraps Facepunch.Steamworks lobby APIs in a way that
/// DeckHubManager and GameLobby UI can consume.
///
/// References:
/// - https://wiki.facepunch.com/steamworks/SteamMatchmaking
/// - https://wiki.facepunch.com/steamworks/SteamFriends
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    private const int MaxLobbyMembers = 2;

    // LobbyData keys (stored in the lobby's shared key-value store)
    private const string KeyHostId = "hostId";
    private const string KeyHostName = "hostName";
    private const string KeyGuestId = "guestId";
    private const string KeyGuestName = "guestName";
    private const string KeyHostReady = "hostReady";
    private const string KeyGuestReady = "guestReady";
    private const string KeyHostDeckId = "hostDeckId";
    private const string KeyHostDeckName = "hostDeckName";
    private const string KeyGuestDeckId = "guestDeckId";
    private const string KeyGuestDeckName = "guestDeckName";

    /// <summary>The actual Steam lobby we're in (if any).</summary>
    private Lobby? _currentLobby;

    /// <summary>Pending lobby ID from an invite that hasn't been joined yet.</summary>
    private SteamId _pendingLobbyId;

    public bool IsInLobby => _currentLobby.HasValue;
    public bool IsHost { get; private set; }

    /// <summary>True if we received an invite and haven't joined/dismissed it yet.</summary>
    public bool HasPendingInvite => _pendingLobbyId.Value != 0;
    public SteamId PendingLobbyId => _pendingLobbyId;

    // Cached lobby data for UI display
    public string HostName { get; private set; }
    public string GuestName { get; private set; }
    public bool HostReady { get; private set; }
    public bool GuestReady { get; private set; }
    public string HostDeckName { get; private set; }
    public string GuestDeckName { get; private set; }

    /// <summary>Raised when we successfully enter a lobby (as host or guest).</summary>
    public event Action LobbyEntered;

    /// <summary>Raised when we leave a lobby.</summary>
    public event Action LobbyLeft;

    /// <summary>Raised when lobby data changes (member joined, ready state changed, etc.).</summary>
    public event Action LobbyDataChanged;

    /// <summary>Raised when both players are ready and the match should start.</summary>
    public event Action BothPlayersReady;

    private bool _p2pHeaderSent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Subscribe to Facepunch.Steamworks lobby events
        SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
        SteamMatchmaking.OnLobbyDataChanged += HandleLobbyDataChanged;
        SteamMatchmaking.OnLobbyMemberJoined += HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += HandleLobbyMemberLeave;
        SteamMatchmaking.OnLobbyMemberDisconnected += HandleLobbyMemberDisconnected;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
        SteamMatchmaking.OnLobbyDataChanged -= HandleLobbyDataChanged;
        SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeave;
        SteamMatchmaking.OnLobbyMemberDisconnected -= HandleLobbyMemberDisconnected;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Host-only: creates a 2-player friends-only lobby and opens the Steam
    /// invite overlay for that lobby.
    /// </summary>
    public async void CreateLobbyForMatch(string deckId, string deckName)
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            Debug.LogError(
                "SteamLobbyManager: Cannot create lobby – SteamManager not initialised."
            );
            return;
        }

        if (_currentLobby.HasValue)
        {
            Debug.LogWarning(
                "SteamLobbyManager: Already in a lobby. Leave first before creating a new one."
            );
            return;
        }

        try
        {
            Debug.Log("SteamLobbyManager: Creating friends-only lobby...");

            // Create a friends-only lobby with max 2 members
            var lobbyResult = await SteamMatchmaking.CreateLobbyAsync(MaxLobbyMembers);

            if (!lobbyResult.HasValue)
            {
                Debug.LogError("SteamLobbyManager: CreateLobbyAsync returned null.");
                return;
            }

            var lobby = lobbyResult.Value;

            // Configure lobby visibility
            lobby.SetFriendsOnly();
            lobby.SetJoinable(true);

            // Set initial lobby data
            string localId = SteamClient.SteamId.ToString();
            string localName = SteamClient.Name ?? "Host";

            lobby.SetData(KeyHostId, localId);
            lobby.SetData(KeyHostName, localName);
            lobby.SetData(KeyGuestId, "");
            lobby.SetData(KeyGuestName, "Waiting...");
            lobby.SetData(KeyHostReady, "0");
            lobby.SetData(KeyGuestReady, "0");
            lobby.SetData(KeyHostDeckId, deckId ?? "");
            lobby.SetData(KeyHostDeckName, deckName ?? "");
            lobby.SetData(KeyGuestDeckId, "");
            lobby.SetData(KeyGuestDeckName, "");

            _currentLobby = lobby;
            IsHost = true;
            _p2pHeaderSent = false;

            // Cache the data locally
            RefreshLobbyData();

            Debug.Log($"SteamLobbyManager: Lobby created successfully. ID={lobby.Id}");

            // Open Steam's game invite overlay so the host can invite a friend
            SteamFriends.OpenGameInviteOverlay(lobby.Id);

            LobbyEntered?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"SteamLobbyManager: Exception during CreateLobbyForMatch: {e.Message}\n{e.StackTrace}"
            );
        }
    }

    /// <summary>
    /// Guest-only: joins a lobby by its SteamId. Called after receiving an invite.
    /// </summary>
    public async Task<bool> JoinLobbyAsync(SteamId lobbyId)
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            Debug.LogError("SteamLobbyManager: Cannot join lobby – SteamManager not initialised.");
            return false;
        }

        if (_currentLobby.HasValue)
        {
            Debug.LogWarning(
                "SteamLobbyManager: Already in a lobby. Leave first before joining another."
            );
            return false;
        }

        try
        {
            Debug.Log($"SteamLobbyManager: Joining lobby {lobbyId}...");

            var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);

            if (!lobby.HasValue)
            {
                Debug.LogError("SteamLobbyManager: JoinLobbyAsync returned null.");
                return false;
            }

            _currentLobby = lobby.Value;
            IsHost = false;
            _p2pHeaderSent = false;
            _pendingLobbyId = default; // Clear pending invite

            // Register ourselves as the guest
            string localId = SteamClient.SteamId.ToString();
            string localName = SteamClient.Name ?? "Guest";

            lobby.Value.SetData(KeyGuestId, localId);
            lobby.Value.SetData(KeyGuestName, localName);
            lobby.Value.SetData(KeyGuestReady, "0");

            RefreshLobbyData();

            Debug.Log($"SteamLobbyManager: Successfully joined lobby {lobbyId}");

            LobbyEntered?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"SteamLobbyManager: Exception during JoinLobbyAsync: {e.Message}\n{e.StackTrace}"
            );
            return false;
        }
    }

    /// <summary>
    /// Sets a pending lobby invite that will be joined once the player selects a deck.
    /// Called by SteamManager when receiving an invite.
    /// </summary>
    public void SetPendingInvite(SteamId lobbyId)
    {
        _pendingLobbyId = lobbyId;
        Debug.Log($"SteamLobbyManager: Pending invite set for lobby {lobbyId}");
    }

    /// <summary>
    /// Clears any pending invite without joining.
    /// </summary>
    public void ClearPendingInvite()
    {
        _pendingLobbyId = default;
    }

    /// <summary>
    /// Joins the pending invite lobby. Returns true if join was initiated.
    /// </summary>
    public async Task<bool> JoinPendingInviteAsync()
    {
        if (!HasPendingInvite)
        {
            Debug.LogWarning("SteamLobbyManager: No pending invite to join.");
            return false;
        }

        return await JoinLobbyAsync(_pendingLobbyId);
    }

    /// <summary>
    /// Updates cached Host/Guest names and ready flags from the lobby's data.
    /// </summary>
    public void RefreshLobbyData()
    {
        if (!_currentLobby.HasValue)
            return;

        var lobby = _currentLobby.Value;

        HostName = lobby.GetData(KeyHostName);
        GuestName = lobby.GetData(KeyGuestName);
        HostReady = lobby.GetData(KeyHostReady) == "1";
        GuestReady = lobby.GetData(KeyGuestReady) == "1";
        HostDeckName = lobby.GetData(KeyHostDeckName);
        GuestDeckName = lobby.GetData(KeyGuestDeckName);

        if (string.IsNullOrEmpty(HostName))
            HostName = "Host";
        if (string.IsNullOrEmpty(GuestName))
            GuestName = "Waiting...";
    }

    /// <summary>
    /// Called by UI when the local player toggles Ready/Unready.
    /// </summary>
    public void SetLocalReady(bool ready)
    {
        if (!_currentLobby.HasValue)
        {
            Debug.LogWarning("SteamLobbyManager: SetLocalReady called but not in a lobby.");
            return;
        }

        var lobby = _currentLobby.Value;
        string value = ready ? "1" : "0";

        if (IsHost)
        {
            lobby.SetData(KeyHostReady, value);
            HostReady = ready;
        }
        else
        {
            lobby.SetData(KeyGuestReady, value);
            GuestReady = ready;
        }

        Debug.Log($"SteamLobbyManager: Local ready state set to {ready} (IsHost={IsHost})");

        // NOTE: Don't call RefreshLobbyData() here - it would overwrite the value
        // we just set with potentially stale data from lobby.GetData().
        // The LobbyDataChanged event will fire and update remote state.

        // Notify listeners that data changed
        LobbyDataChanged?.Invoke();
        CheckBothReady();
    }

    /// <summary>
    /// Called by DeckHubManager when the local player has selected a deck
    /// to use for this lobby.
    /// </summary>
    public void SetLocalLobbyDeck(string deckId, string deckName)
    {
        if (!_currentLobby.HasValue)
            return;

        var lobby = _currentLobby.Value;

        if (IsHost)
        {
            lobby.SetData(KeyHostDeckId, deckId ?? "");
            lobby.SetData(KeyHostDeckName, deckName ?? "");
            HostDeckName = deckName;
        }
        else
        {
            lobby.SetData(KeyGuestDeckId, deckId ?? "");
            lobby.SetData(KeyGuestDeckName, deckName ?? "");
            GuestDeckName = deckName;
        }

        Debug.Log($"SteamLobbyManager: Local deck set to '{deckName}' (id={deckId})");
    }

    /// <summary>
    /// Returns the Steam ID of the host player.
    /// </summary>
    public string HostId
    {
        get
        {
            if (!_currentLobby.HasValue)
                return SteamClient.SteamId.ToString();
            return _currentLobby.Value.GetData(KeyHostId);
        }
    }

    /// <summary>
    /// Returns the Steam ID of the guest player (empty if no guest yet).
    /// </summary>
    public string GuestId
    {
        get
        {
            if (!_currentLobby.HasValue)
                return "";
            return _currentLobby.Value.GetData(KeyGuestId);
        }
    }

    /// <summary>
    /// Returns the Steam ID of the remote player (guest if we're host, host if we're guest).
    /// </summary>
    public SteamId RemotePlayerId
    {
        get
        {
            if (!_currentLobby.HasValue)
                return default;

            string remoteIdStr = IsHost ? GuestId : HostId;
            if (ulong.TryParse(remoteIdStr, out ulong id))
                return id;
            return default;
        }
    }

    /// <summary>
    /// Returns the current lobby ID (or default if not in a lobby).
    /// </summary>
    public SteamId CurrentLobbyId => _currentLobby?.Id ?? default;

    /// <summary>
    /// Leaves the current lobby and clears cached state.
    /// </summary>
    public void LeaveLobby()
    {
        if (_currentLobby.HasValue)
        {
            Debug.Log($"SteamLobbyManager: Leaving lobby {_currentLobby.Value.Id}");
            _currentLobby.Value.Leave();
        }

        _currentLobby = null;
        IsHost = false;
        HostName = null;
        GuestName = null;
        HostReady = false;
        GuestReady = false;
        HostDeckName = null;
        GuestDeckName = null;
        _p2pHeaderSent = false;

        LobbyLeft?.Invoke();
    }

    /// <summary>
    /// Returns true if a guest has joined the lobby.
    /// </summary>
    public bool HasGuest
    {
        get
        {
            if (!_currentLobby.HasValue)
                return false;
            string guestId = _currentLobby.Value.GetData(KeyGuestId);
            return !string.IsNullOrEmpty(guestId);
        }
    }

    /// <summary>
    /// Opens the Steam game invite overlay for the current lobby.
    /// Only works for the host while in a lobby.
    /// Note: This only works in standalone builds launched from Steam, not in the Unity Editor
    /// or when launched directly from Finder/Explorer.
    /// </summary>
    public void OpenInviteOverlay()
    {
        if (!_currentLobby.HasValue)
        {
            Debug.LogWarning("SteamLobbyManager: Cannot open invite overlay - not in a lobby.");
            return;
        }

        if (!IsHost)
        {
            Debug.LogWarning("SteamLobbyManager: Only the host can open the invite overlay.");
            return;
        }

        // Log overlay availability for debugging
        bool overlayEnabled = SteamUtils.IsOverlayEnabled;
        Debug.Log(
            $"SteamLobbyManager: Opening invite overlay for lobby {_currentLobby.Value.Id}. "
                + $"Overlay enabled: {overlayEnabled}"
        );

        if (!overlayEnabled)
        {
            Debug.LogWarning(
                "SteamLobbyManager: Steam overlay is not enabled. "
                    + "On macOS, the game must be launched from Steam for the overlay to work. "
                    + "Try adding the game to Steam as a non-Steam game and launching it from there."
            );
        }

        SteamFriends.OpenGameInviteOverlay(_currentLobby.Value.Id);
    }

    // -------------------------------------------------------------------------
    // Event Handlers
    // -------------------------------------------------------------------------

    private void HandleLobbyCreated(Result result, Lobby lobby)
    {
        Debug.Log(
            $"SteamLobbyManager: OnLobbyCreated callback - Result={result}, LobbyId={lobby.Id}"
        );
    }

    private void HandleLobbyEntered(Lobby lobby)
    {
        Debug.Log($"SteamLobbyManager: OnLobbyEntered callback - LobbyId={lobby.Id}");
        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
    }

    private void HandleLobbyDataChanged(Lobby lobby)
    {
        if (!_currentLobby.HasValue || _currentLobby.Value.Id != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: OnLobbyDataChanged callback");
        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
        CheckBothReady();
    }

    private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        if (!_currentLobby.HasValue || _currentLobby.Value.Id != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: Member joined - {friend.Name} ({friend.Id})");

        // If we're the host and a guest joined, update guest info
        if (IsHost && friend.Id != SteamClient.SteamId)
        {
            lobby.SetData(KeyGuestId, friend.Id.ToString());
            lobby.SetData(KeyGuestName, friend.Name ?? "Guest");
            lobby.SetData(KeyGuestReady, "0");
        }

        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
    }

    private void HandleLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        if (!_currentLobby.HasValue || _currentLobby.Value.Id != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: Member left - {friend.Name} ({friend.Id})");

        // If the guest left, clear guest data
        if (IsHost && friend.Id.ToString() == GuestId)
        {
            lobby.SetData(KeyGuestId, "");
            lobby.SetData(KeyGuestName, "Waiting...");
            lobby.SetData(KeyGuestReady, "0");
            lobby.SetData(KeyGuestDeckId, "");
            lobby.SetData(KeyGuestDeckName, "");
        }

        // If we're the guest and the host left, leave the lobby
        if (!IsHost && friend.Id.ToString() == HostId)
        {
            Debug.Log("SteamLobbyManager: Host left, leaving lobby...");
            LeaveLobby();
            return;
        }

        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
    }

    private void HandleLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        // Treat disconnect same as leave
        HandleLobbyMemberLeave(lobby, friend);
    }

    // -------------------------------------------------------------------------
    // Match Start Logic
    // -------------------------------------------------------------------------

    private void CheckBothReady()
    {
        if (!_currentLobby.HasValue || _p2pHeaderSent)
            return;

        RefreshLobbyData();

        if (!HostReady || !GuestReady)
            return;

        if (!HasGuest)
            return;

        Debug.Log("SteamLobbyManager: Both players ready!");
        BothPlayersReady?.Invoke();

        // Configure transport and start game for both host and guest
        ConfigureTransportAndStartMatch();
    }

    private void ConfigureTransportAndStartMatch()
    {
        if (_p2pHeaderSent)
            return;

        var transport = FindFirstObjectByType<SteamP2PTransport>();
        if (transport == null)
        {
            Debug.LogWarning("SteamLobbyManager: No SteamP2PTransport found; cannot start match.");
            return;
        }

        SteamId remoteId = RemotePlayerId;
        if (remoteId.Value == 0)
        {
            Debug.LogWarning(
                "SteamLobbyManager: Remote player ID not set; cannot configure transport."
            );
            return;
        }

        // Configure transport based on role
        if (IsHost)
        {
            transport.ConfigureAsHost(remoteId);
        }
        else
        {
            transport.ConfigureAsGuest(remoteId);
        }

        NetworkSessionStore.CurrentTransport = transport;

        // Only the host sends the session header
        if (IsHost)
        {
            TrySendSessionHeaderIfReady();
        }
        else
        {
            // Guest marks as ready to receive
            _p2pHeaderSent = true;
            Debug.Log(
                "SteamLobbyManager: Guest transport configured, waiting for session header from host."
            );
        }
    }

    private void TrySendSessionHeaderIfReady()
    {
        if (!_currentLobby.HasValue || !IsHost || _p2pHeaderSent)
            return;

        if (!HostReady || !GuestReady)
            return;

        string hostId = HostId;
        string guestId = GuestId;

        if (string.IsNullOrEmpty(hostId) || string.IsNullOrEmpty(guestId))
        {
            Debug.LogWarning(
                "SteamLobbyManager: Cannot send session header – hostId or guestId missing."
            );
            return;
        }

        // Configure the P2P transport with the guest's Steam ID
        var transport = FindFirstObjectByType<SteamP2PTransport>();
        if (transport == null)
        {
            Debug.LogWarning(
                "SteamLobbyManager: No SteamP2PTransport found; cannot send session header."
            );
            return;
        }

        transport.ConfigureAsHost(RemotePlayerId);
        NetworkSessionStore.CurrentTransport = transport;

        // Build the session header
        var header = new NetSessionHeader
        {
            protocolVersion = 1,
            rngSeed = DeterministicRng.IsInitialized
                ? DeterministicRng.Seed
                : UnityEngine.Random.Range(int.MinValue, int.MaxValue),
            hostId = hostId,
            guestId = guestId,
            localRole = SlotOwner.Player1,
            hostDeckId = _currentLobby.Value.GetData(KeyHostDeckId) ?? "",
            guestDeckId = _currentLobby.Value.GetData(KeyGuestDeckId) ?? "",
            hostDeck = Array.Empty<DeckCardEntry>(),
            guestDeck = Array.Empty<DeckCardEntry>(),
        };

        var matchManager = FindFirstObjectByType<NetworkMatchManager>();
        if (matchManager == null)
        {
            Debug.LogWarning(
                "SteamLobbyManager: No NetworkMatchManager found; cannot send session header."
            );
            return;
        }

        Debug.Log("SteamLobbyManager: Sending NetSessionHeader via P2P transport.");
        matchManager.SendSessionHeader(header);
        _p2pHeaderSent = true;
    }
}
