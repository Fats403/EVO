using Steamworks;
using UnityEngine;

/// <summary>
/// Global Steam manager for Facepunch.Steamworks.
/// - Initializes Steam once and survives across scene loads.
/// - Runs Steam callbacks every frame.
/// - Shuts Steam down cleanly on quit.
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

    /// <summary>True if SteamClient.Init has succeeded.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>True while we're attempting to initialize Steam.</summary>
    public bool IsInitializing { get; private set; }

    /// <summary>Last initialization error message (if any).</summary>
    public string LastErrorMessage { get; private set; }

    private bool _isQuitting;

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
    }

    private void OnEnable()
    {
        Application.quitting += OnApplicationQuitting;
    }

    private void OnDisable()
    {
        Application.quitting -= OnApplicationQuitting;
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
        catch (System.Exception e)
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
            }
        }
        catch (System.Exception e)
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

    private void OnApplicationQuitting()
    {
        _isQuitting = true;
        ShutdownSteam();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // If destroyed for any reason (scene unload on quit, etc.), shut down Steam once.
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
        catch (System.Exception e)
        {
            Debug.LogError(
                $"SteamManager: Exception during SteamClient.Shutdown. {e.GetType().Name}: {e.Message}"
            );
        }
        finally
        {
            IsInitialized = false;
        }
    }
}
