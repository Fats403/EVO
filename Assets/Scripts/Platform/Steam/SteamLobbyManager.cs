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

    // LobbyData keys (stored in the lobby's shared key-value store - host only can set these)
    private const string KeyHostId = "hostId";
    private const string KeyHostName = "hostName";
    private const string KeyGuestId = "guestId";
    private const string KeyGuestName = "guestName";
    private const string KeyHostDeckId = "hostDeckId";
    private const string KeyHostDeckName = "hostDeckName";
    private const string KeyGuestDeckId = "guestDeckId";
    private const string KeyGuestDeckName = "guestDeckName";

    // MemberData keys (per-user data - each player sets their own)
    private const string KeyMemberReady = "ready";
    private const string KeyMemberDeckId = "deckId";
    private const string KeyMemberDeckName = "deckName";

    /// <summary>The ID of the lobby we're in (default if not in a lobby).</summary>
    private SteamId _currentLobbyId;

    /// <summary>Pending lobby ID from an invite that hasn't been joined yet.</summary>
    private SteamId _pendingLobbyId;

    public bool IsInLobby => _currentLobbyId.Value != 0;
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
        SteamMatchmaking.OnLobbyMemberDataChanged += HandleLobbyMemberDataChanged;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
        SteamMatchmaking.OnLobbyDataChanged -= HandleLobbyDataChanged;
        SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeave;
        SteamMatchmaking.OnLobbyMemberDisconnected -= HandleLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyMemberDataChanged -= HandleLobbyMemberDataChanged;
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

        if (IsInLobby)
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

            // Store the lobby ID (not the struct - we'll get fresh structs when needed)
            _currentLobbyId = lobby.Id;
            IsHost = true;
            _p2pHeaderSent = false;

            // Configure lobby visibility
            lobby.SetFriendsOnly();
            lobby.SetJoinable(true);

            // Set initial lobby data (only host can set these)
            string localId = SteamClient.SteamId.ToString();
            string localName = SteamClient.Name ?? "Host";

            lobby.SetData(KeyHostId, localId);
            lobby.SetData(KeyHostName, localName);
            lobby.SetData(KeyGuestId, "");
            lobby.SetData(KeyGuestName, "Waiting...");
            lobby.SetData(KeyHostDeckId, deckId ?? "");
            lobby.SetData(KeyHostDeckName, deckName ?? "");
            lobby.SetData(KeyGuestDeckId, "");
            lobby.SetData(KeyGuestDeckName, "");

            // Set our own member data (ready state, deck info)
            lobby.SetMemberData(KeyMemberReady, "0");
            lobby.SetMemberData(KeyMemberDeckId, deckId ?? "");
            lobby.SetMemberData(KeyMemberDeckName, deckName ?? "");

            // Cache the data locally - use defaults for now, Steam will sync
            HostName = localName;
            GuestName = "Waiting...";
            HostReady = false;
            GuestReady = false;
            HostDeckName = deckName;
            GuestDeckName = null;

            Debug.Log($"SteamLobbyManager: Lobby created successfully. ID={lobby.Id}");

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

        if (IsInLobby)
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

            // Store the lobby ID (not the struct)
            _currentLobbyId = lobby.Value.Id;
            IsHost = false;
            _p2pHeaderSent = false;
            _pendingLobbyId = default; // Clear pending invite

            // Set our own member data (ready state)
            // Note: The host will update the lobby-wide guest info via OnLobbyMemberJoined
            lobby.Value.SetMemberData(KeyMemberReady, "0");

            // Initial refresh of lobby data
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
        if (!IsInLobby)
            return;

        // Get a fresh lobby struct
        var lobby = new Lobby(_currentLobbyId);

        // Read lobby-wide data (set by host only)
        HostName = lobby.GetData(KeyHostName);
        GuestName = lobby.GetData(KeyGuestName);
        HostDeckName = lobby.GetData(KeyHostDeckName);
        GuestDeckName = lobby.GetData(KeyGuestDeckName);

        if (string.IsNullOrEmpty(HostName))
            HostName = "Host";
        if (string.IsNullOrEmpty(GuestName))
            GuestName = "Waiting...";

        // Read ready states from member data (each player sets their own)
        string hostIdStr = lobby.GetData(KeyHostId);
        string guestIdStr = lobby.GetData(KeyGuestId);

        HostReady = false;
        GuestReady = false;

        Debug.Log(
            $"SteamLobbyManager.RefreshLobbyData: hostId={hostIdStr}, guestId={guestIdStr}, memberCount={lobby.MemberCount}"
        );

        // Get ready states from lobby members
        foreach (var member in lobby.Members)
        {
            string memberReadyStr = lobby.GetMemberData(member, KeyMemberReady);
            bool memberReady = memberReadyStr == "1";

            Debug.Log($"  Member: {member.Name} ({member.Id}), ready={memberReadyStr}");

            // Check if this member is the host
            if (!string.IsNullOrEmpty(hostIdStr) && member.Id.ToString() == hostIdStr)
            {
                HostReady = memberReady;
            }
            // Check if this member is the guest
            else if (!string.IsNullOrEmpty(guestIdStr) && member.Id.ToString() == guestIdStr)
            {
                GuestReady = memberReady;

                // Also update guest deck info from their member data
                string guestDeckName = lobby.GetMemberData(member, KeyMemberDeckName);
                if (!string.IsNullOrEmpty(guestDeckName))
                    GuestDeckName = guestDeckName;
            }
        }

        Debug.Log(
            $"SteamLobbyManager.RefreshLobbyData: HostReady={HostReady}, GuestReady={GuestReady}"
        );
    }

    /// <summary>
    /// Called by UI when the local player toggles Ready/Unready.
    /// </summary>
    public void SetLocalReady(bool ready)
    {
        if (!IsInLobby)
        {
            Debug.LogWarning("SteamLobbyManager: SetLocalReady called but not in a lobby.");
            return;
        }

        // Get a fresh lobby struct to ensure we're working with current state
        var lobby = new Lobby(_currentLobbyId);
        string value = ready ? "1" : "0";

        Debug.Log(
            $"SteamLobbyManager: Setting member ready data to '{value}' for lobby {_currentLobbyId}"
        );

        // Use SetMemberData - each player can only set their own member data
        lobby.SetMemberData(KeyMemberReady, value);

        // Update local cache immediately
        if (IsHost)
        {
            HostReady = ready;
        }
        else
        {
            GuestReady = ready;
        }

        Debug.Log($"SteamLobbyManager: Local ready state set to {ready} (IsHost={IsHost})");

        // Notify listeners that data changed (OnLobbyMemberDataChanged will also fire for remote)
        LobbyDataChanged?.Invoke();
        CheckBothReady();
    }

    /// <summary>
    /// Called by DeckHubManager when the local player has selected a deck
    /// to use for this lobby.
    /// </summary>
    public void SetLocalLobbyDeck(string deckId, string deckName)
    {
        if (!IsInLobby)
            return;

        var lobby = new Lobby(_currentLobbyId);

        // Set member data (each player sets their own)
        lobby.SetMemberData(KeyMemberDeckId, deckId ?? "");
        lobby.SetMemberData(KeyMemberDeckName, deckName ?? "");

        // Host also updates lobby-wide data for visibility
        if (IsHost)
        {
            lobby.SetData(KeyHostDeckId, deckId ?? "");
            lobby.SetData(KeyHostDeckName, deckName ?? "");
            HostDeckName = deckName;
        }
        else
        {
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
            if (!IsInLobby)
                return SteamClient.SteamId.ToString();
            return new Lobby(_currentLobbyId).GetData(KeyHostId);
        }
    }

    /// <summary>
    /// Returns the Steam ID of the guest player (empty if no guest yet).
    /// </summary>
    public string GuestId
    {
        get
        {
            if (!IsInLobby)
                return "";
            return new Lobby(_currentLobbyId).GetData(KeyGuestId);
        }
    }

    /// <summary>
    /// Returns the Steam ID of the remote player (guest if we're host, host if we're guest).
    /// </summary>
    public SteamId RemotePlayerId
    {
        get
        {
            if (!IsInLobby)
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
    public SteamId CurrentLobbyId => _currentLobbyId;

    /// <summary>
    /// Leaves the current lobby and clears cached state.
    /// </summary>
    public void LeaveLobby()
    {
        if (IsInLobby)
        {
            Debug.Log($"SteamLobbyManager: Leaving lobby {_currentLobbyId}");
            var lobby = new Lobby(_currentLobbyId);
            lobby.Leave();
        }

        // Disconnect transport if active
        var transport = FindFirstObjectByType<SteamP2PTransport>();
        if (transport != null && transport.IsConnected)
        {
            transport.Disconnect();
        }

        // Clear the network session store
        NetworkSessionStore.CurrentTransport = null;

        // Clear all lobby state
        _currentLobbyId = default;
        IsHost = false;
        HostName = null;
        GuestName = null;
        HostReady = false;
        GuestReady = false;
        HostDeckName = null;
        GuestDeckName = null;
        _p2pHeaderSent = false;

        Debug.Log("SteamLobbyManager: Lobby state cleared.");

        LobbyLeft?.Invoke();
    }

    /// <summary>
    /// Returns true if a guest has joined the lobby.
    /// </summary>
    public bool HasGuest
    {
        get
        {
            if (!IsInLobby)
                return false;
            string guestId = new Lobby(_currentLobbyId).GetData(KeyGuestId);
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
        if (!IsInLobby)
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
            $"SteamLobbyManager: Opening invite overlay for lobby {_currentLobbyId}. "
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

        SteamFriends.OpenGameInviteOverlay(_currentLobbyId);
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

        // IMPORTANT:
        // We can enter a lobby without going through our own JoinLobbyAsync/CreateLobbyForMatch
        // (e.g. accepting an invite from the Steam overlay). In that case, _currentLobbyId
        // would still be default and IsInLobby would remain false, causing all later updates
        // (RefreshLobbyData, ready state, UI) to never propagate.
        bool enteringNewLobby = !IsInLobby || _currentLobbyId != lobby.Id;

        if (enteringNewLobby)
        {
            _currentLobbyId = lobby.Id;
            _pendingLobbyId = default;
            _p2pHeaderSent = false;

            // Determine host role from lobby ownership when possible.
            // (Lobby-wide data like hostId may not be populated immediately on entry.)
            try
            {
                IsHost = lobby.Owner.Id == SteamClient.SteamId;
            }
            catch
            {
                // Fallback: keep existing IsHost value.
            }

            Debug.Log(
                $"SteamLobbyManager: Synced local lobby state from callback. IsHost={IsHost}, LobbyId={_currentLobbyId}"
            );

            LobbyEntered?.Invoke();
        }

        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
    }

    private void HandleLobbyDataChanged(Lobby lobby)
    {
        if (!IsInLobby || _currentLobbyId != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: OnLobbyDataChanged callback for lobby {lobby.Id}");
        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
        CheckBothReady();
    }

    private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        if (!IsInLobby || _currentLobbyId != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: Member joined - {friend.Name} ({friend.Id})");

        // If we're the host and a guest joined, update lobby-wide guest info
        if (IsHost && friend.Id != SteamClient.SteamId)
        {
            lobby.SetData(KeyGuestId, friend.Id.ToString());
            lobby.SetData(KeyGuestName, friend.Name ?? "Guest");
            // Guest deck info will come from their member data when they set it
            lobby.SetData(KeyGuestDeckId, "");
            lobby.SetData(KeyGuestDeckName, "");
        }

        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
    }

    private void HandleLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        if (!IsInLobby || _currentLobbyId != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: Member left - {friend.Name} ({friend.Id})");

        // If the guest left, clear guest data from lobby
        if (IsHost && friend.Id.ToString() == GuestId)
        {
            lobby.SetData(KeyGuestId, "");
            lobby.SetData(KeyGuestName, "Waiting...");
            lobby.SetData(KeyGuestDeckId, "");
            lobby.SetData(KeyGuestDeckName, "");
            // Note: Guest's member data is automatically gone when they leave
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

    private void HandleLobbyMemberDataChanged(Lobby lobby, Friend friend)
    {
        if (!IsInLobby || _currentLobbyId != lobby.Id)
            return;

        Debug.Log($"SteamLobbyManager: Member data changed for {friend.Name} ({friend.Id})");

        RefreshLobbyData();
        LobbyDataChanged?.Invoke();
        CheckBothReady();
    }

    // -------------------------------------------------------------------------
    // Match Start Logic
    // -------------------------------------------------------------------------

    private void CheckBothReady()
    {
        if (!IsInLobby || _p2pHeaderSent)
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
        if (!IsInLobby || !IsHost || _p2pHeaderSent)
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

        var lobby = new Lobby(_currentLobbyId);

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
            hostDeckId = lobby.GetData(KeyHostDeckId) ?? "",
            guestDeckId = lobby.GetData(KeyGuestDeckId) ?? "",
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
