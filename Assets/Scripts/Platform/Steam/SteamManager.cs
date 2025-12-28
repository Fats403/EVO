using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Global Steam manager for Facepunch.Steamworks.
/// - Initializes Steam once and survives across scene loads.
/// - Runs Steam callbacks every frame.
/// - Handles game invite flow (overlay invites + command-line join).
/// - Shuts Steam down cleanly on quit.
///
/// References:
/// - https://wiki.facepunch.com/steamworks/SteamFriends
/// - https://wiki.facepunch.com/steamworks/SteamMatchmaking
/// </summary>
public class SteamManager : MonoBehaviour
{
    /// <summary>Global singleton instance.</summary>
    public static SteamManager Instance { get; private set; }

    [Header("Steam Settings")]
    [Tooltip("Your Steam App ID. Using 480 (Spacewar) for local testing.")]
    [SerializeField]
    private uint appId = 480;

    [Tooltip("If true, logs extra information to the console.")]
    [SerializeField]
    private bool debugLogging = true;

    [Header("Scene Settings")]
    [Tooltip("Name of the DeckHub scene to navigate to when receiving an invite.")]
    [SerializeField]
    private string deckHubSceneName = "DeckHubScene";

    /// <summary>True if SteamClient.Init has succeeded.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>True while we're attempting to initialize Steam.</summary>
    public bool IsInitializing { get; private set; }

    /// <summary>Last initialization error message (if any).</summary>
    public string LastErrorMessage { get; private set; }

    /// <summary>Raised when an invite is received and we're ready to show the join UI.</summary>
    public event Action<SteamId> OnInviteReady;

    private bool _isQuitting;
    private SteamId _pendingCommandLineLobby;

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSteam();
        CheckCommandLineInvite();
    }

    private void OnEnable()
    {
        Application.quitting += OnApplicationQuitting;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Subscribe to Steam invite events
        SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
    }

    private void OnDisable()
    {
        Application.quitting -= OnApplicationQuitting;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
    }

    private void Update()
    {
        // Must be called every frame for callbacks/events to fire.
        if (!IsInitialized || _isQuitting)
            return;

        try
        {
            SteamClient.RunCallbacks();
        }
        catch (Exception e)
        {
            if (debugLogging)
            {
                Debug.LogError($"SteamManager: RunCallbacks threw {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private void InitializeSteam()
    {
        if (IsInitialized || IsInitializing)
            return;

        try
        {
            IsInitializing = true;
            LastErrorMessage = null;

            SteamClient.Init(appId, true);
            IsInitialized = true;

            if (debugLogging)
            {
                Debug.Log(
                    $"SteamManager: Initialized. User: {SteamClient.Name} ({SteamClient.SteamId})"
                );
                Debug.Log(
                    $"SteamManager: Overlay enabled: {SteamUtils.IsOverlayEnabled}, "
                        + $"Running on Steam Deck: {SteamUtils.IsRunningOnSteamDeck}"
                );
            }

            // Initialize relay network access for SteamNetworkingSockets
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }
        catch (Exception e)
        {
            IsInitialized = false;
            LastErrorMessage = $"Failed to initialize Steam. {e.GetType().Name}: {e.Message}";
            Debug.LogError($"SteamManager: {LastErrorMessage}");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// Checks for +connect_lobby command line argument passed when the game
    /// is launched via a Steam invite.
    /// </summary>
    private void CheckCommandLineInvite()
    {
        // Steam passes +connect_lobby <lobbyId> when launching via invite
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("+connect_lobby", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    _pendingCommandLineLobby = lobbyId;
                    if (debugLogging)
                    {
                        Debug.Log($"SteamManager: Found +connect_lobby {lobbyId} in command line.");
                    }
                }
                break;
            }
        }
    }

    /// <summary>
    /// Called when a friend invites us to a game lobby while we're already running.
    /// </summary>
    private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        if (debugLogging)
        {
            Debug.Log(
                $"SteamManager: Received lobby invite from friend {friendId} for lobby {lobby.Id}"
            );
        }

        ProcessLobbyInvite(lobby.Id);
    }

    /// <summary>
    /// Processes a lobby invite (from command line or overlay).
    /// </summary>
    private void ProcessLobbyInvite(SteamId lobbyId)
    {
        // Check prerequisites
        if (!IsInitialized)
        {
            Debug.LogWarning("SteamManager: Cannot process invite - Steam not initialized.");
            return;
        }

        // Check if Firebase is ready (if FirebaseManager exists)
        var firebase = FindFirstObjectByType<FirebaseManager>();
        if (firebase != null && !firebase.IsFirebaseReady)
        {
            Debug.Log("SteamManager: Firebase not ready, deferring invite processing...");
            // We'll re-process when scene loads and Firebase is ready
            _pendingCommandLineLobby = lobbyId;
            return;
        }

        // Set the pending invite in SteamLobbyManager
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.SetPendingInvite(lobbyId);
        }
        else
        {
            // Store it for later if SteamLobbyManager doesn't exist yet
            _pendingCommandLineLobby = lobbyId;
        }

        // Navigate to DeckHubScene if we're not already there
        string currentScene = SceneManager.GetActiveScene().name;
        if (!currentScene.Equals(deckHubSceneName, StringComparison.OrdinalIgnoreCase))
        {
            if (debugLogging)
            {
                Debug.Log($"SteamManager: Navigating to {deckHubSceneName} for invite handling...");
            }

            // Use SceneTransitionManager if available, otherwise direct load
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadScene(deckHubSceneName);
            }
            else
            {
                SceneManager.LoadScene(deckHubSceneName);
            }
        }
        else
        {
            // Already in DeckHubScene, notify listeners
            OnInviteReady?.Invoke(lobbyId);
        }
    }

    /// <summary>
    /// Called when a scene finishes loading.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If we have a pending invite and we just loaded DeckHubScene, process it
        if (
            _pendingCommandLineLobby.Value != 0
            && scene.name.Equals(deckHubSceneName, StringComparison.OrdinalIgnoreCase)
        )
        {
            // Wait a frame for managers to initialize
            StartCoroutine(ProcessPendingInviteDelayed());
        }
    }

    private System.Collections.IEnumerator ProcessPendingInviteDelayed()
    {
        // Wait for end of frame to ensure all Awake/Start methods have run
        yield return new WaitForEndOfFrame();
        yield return null; // Extra frame for safety

        if (_pendingCommandLineLobby.Value == 0)
            yield break;

        // Check Firebase again
        var firebase = FindFirstObjectByType<FirebaseManager>();
        if (firebase != null && !firebase.IsFirebaseReady)
        {
            if (debugLogging)
            {
                Debug.Log("SteamManager: Waiting for Firebase initialization...");
            }
            // Keep waiting - Firebase will fire an event when ready
            yield return new WaitUntil(() => firebase.IsFirebaseReady);
        }

        SteamId lobbyId = _pendingCommandLineLobby;
        _pendingCommandLineLobby = default;

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.SetPendingInvite(lobbyId);

            if (debugLogging)
            {
                Debug.Log($"SteamManager: Pending invite {lobbyId} passed to SteamLobbyManager.");
            }

            OnInviteReady?.Invoke(lobbyId);
        }
        else
        {
            Debug.LogWarning("SteamManager: SteamLobbyManager not found, cannot process invite.");
        }
    }

    private void OnApplicationQuitting()
    {
        _isQuitting = true;
        ShutdownSteam();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ShutdownSteam();
            Instance = null;
        }
    }

    /// <summary>Cleanly shuts down the Steam client.</summary>
    public void ShutdownSteam()
    {
        if (!IsInitialized)
            return;

        try
        {
            SteamClient.Shutdown();
            if (debugLogging)
            {
                Debug.Log("SteamManager: SteamClient.Shutdown called.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"SteamManager: Exception during Shutdown. {e.GetType().Name}: {e.Message}"
            );
        }
        finally
        {
            IsInitialized = false;
        }
    }
}
