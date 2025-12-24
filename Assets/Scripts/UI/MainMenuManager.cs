using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    // Hard‑coded here since your project isn't using namespaces or
    // folder‑based namespaces elsewhere. This keeps things simple.
    private const string gameSceneName = "MainScene";
    private const string deckHubSceneName = "DeckHubScene";

    [Header("UI References")]
    [Tooltip("Container with interactive main‑menu buttons (Play, Options, Quit).")]
    [SerializeField]
    private GameObject mainMenuItemsRoot;

    [Tooltip("Panel or object shown while the main menu is loading (Steam, data, etc.).")]
    [SerializeField]
    private GameObject loadingRoot;

    [Tooltip("Text used to show critical boot / loading errors. Optional.")]
    [SerializeField]
    private TextMeshProUGUI loadingErrorText;

    [Tooltip("Panel or object shown if a critical boot / loading error occurs.")]
    [SerializeField]
    private GameObject loadingErrorRoot;

    [Header("Loader Animation")]
    [Tooltip("The RectTransform for the loader graphic that should rotate.")]
    [SerializeField]
    private RectTransform loaderTransform;

    [Tooltip("Degrees per second the loader rotates (negative values rotate left).")]
    [SerializeField]
    private float loaderRotateSpeed = -180f;

    [Header("Sound UI")]
    [Tooltip("Root object containing the sound toggle icons.")]
    [SerializeField]
    private GameObject soundItemsRoot;

    [Tooltip("Icon shown when music is ON (e.g., music note).")]
    [SerializeField]
    private GameObject musicOnIcon;

    [Tooltip("Icon shown when music is OFF / muted (e.g., crossed-out circle).")]
    [SerializeField]
    private GameObject musicOffIcon;

    private bool _hasAppliedInitialState;

    private void Start()
    {
        ApplyInitialBootState();
        UpdateSoundIcons();

        musicOffIcon.transform.localScale = new Vector3(-1, 1, 1);
    }

    private void Update()
    {
        // Keep UI responsive to late/failed initialization / boot.
        if (!_hasAppliedInitialState)
        {
            ApplyInitialBootState();
        }

        RotateLoader();
    }

    private void ApplyInitialBootState()
    {
        // Our "boot" process is Steam + (optionally) Firebase.
        var steam = SteamManager.Instance;
        if (steam == null)
        {
            // If SteamManager isn't present, just enable normal menu so game is still usable.
            SetActiveSafe(loadingRoot, false);
            SetActiveSafe(loadingErrorRoot, false);
            SetActiveSafe(mainMenuItemsRoot, true);
            _hasAppliedInitialState = true;
            return;
        }

        if (steam.IsInitializing)
        {
            // Show loading while hiding buttons; background stays on.
            SetActiveSafe(loadingRoot, true);
            SetActiveSafe(mainMenuItemsRoot, false);
            SetActiveSafe(loadingErrorRoot, false);
            UpdateErrorText(null);
        }
        else if (steam.IsInitialized)
        {
            // Steam ready – if Firebase is present, also wait for its login
            // before enabling the main menu. If there's no FirebaseManager,
            // we just proceed with Steam-only boot.
            var firebase = FirebaseManager.Instance;

            if (firebase != null)
            {
                // Case 1: Firebase still booting or hasn't attempted login yet → keep loading.
                if (!firebase.IsFirebaseReady || !firebase.HasTriedLogin)
                {
                    SetActiveSafe(loadingRoot, true);
                    SetActiveSafe(mainMenuItemsRoot, false);
                    SetActiveSafe(loadingErrorRoot, false);
                    UpdateErrorText(null);
                    return;
                }

                // Case 2: Firebase login failed → show error panel.
                if (!firebase.IsLoggedIn && !string.IsNullOrEmpty(firebase.LastLoginError))
                {
                    SetActiveSafe(loadingRoot, false);
                    SetActiveSafe(mainMenuItemsRoot, false);
                    SetActiveSafe(loadingErrorRoot, true);
                    UpdateErrorText(firebase.LastLoginError);
                    _hasAppliedInitialState = true;
                    return;
                }
            }

            // Steam (and Firebase if present) are ready – hide loading & error, show main buttons.
            SetActiveSafe(loadingRoot, false);
            SetActiveSafe(loadingErrorRoot, false);
            SetActiveSafe(mainMenuItemsRoot, true);
            UpdateErrorText(null);
            _hasAppliedInitialState = true;
        }
        else
        {
            // Initialization failed.
            SetActiveSafe(loadingRoot, false);
            SetActiveSafe(mainMenuItemsRoot, false); // or true if you want offline mode
            SetActiveSafe(loadingErrorRoot, true);
            UpdateErrorText(steam.LastErrorMessage);
            _hasAppliedInitialState = true;
        }
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    private void UpdateErrorText(string message)
    {
        if (loadingErrorText == null)
            return;

        loadingErrorText.text = string.IsNullOrEmpty(message) ? string.Empty : message;
    }

    private void RotateLoader()
    {
        if (loaderTransform == null)
            return;

        if (!loaderTransform.gameObject.activeInHierarchy)
            return;

        loaderTransform.Rotate(0f, 0f, loaderRotateSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Called by UI button to start the game.
    /// </summary>
    public void StartGame()
    {
        // Optional gate: prevent starting game if Steam isn't ready.
        if (SteamManager.Instance != null && !SteamManager.Instance.IsInitialized)
        {
            Debug.LogWarning("MainMenuManager: Tried to start game before Steam was ready.");
            return;
        }

        // Make sure the scene is added to Build Settings.
        SceneTransitionManager.Instance.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Called by UI button to start the game.
    /// </summary>
    public void GoToDeckHub()
    {
        // Optional gate: prevent starting game if Steam isn't ready.
        if (SteamManager.Instance != null && !SteamManager.Instance.IsInitialized)
        {
            Debug.LogWarning("MainMenuManager: Tried to start game before Steam was ready.");
            return;
        }

        // Make sure the scene is added to Build Settings.
        SceneTransitionManager.Instance.LoadScene(deckHubSceneName);
    }

    /// <summary>
    /// Quits the game. In editor, stops play mode.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Called by the sound toggle button in the main menu.
    /// </summary>
    public void ToggleMusic()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("MainMenuManager: No SoundManager instance found to toggle music.");
            return;
        }

        SoundManager.Instance.ToggleMute();
        UpdateSoundIcons();
    }

    private void UpdateSoundIcons()
    {
        if (soundItemsRoot == null)
            return;

        var sound = SoundManager.Instance;
        if (sound == null)
        {
            // If there's no sound manager, hide sound UI entirely.
            SetActiveSafe(soundItemsRoot, false);
            return;
        }

        SetActiveSafe(soundItemsRoot, true);

        // Always show the "sound on" icon; overlay the "off" icon on top when muted.
        SetActiveSafe(musicOnIcon, true);
        SetActiveSafe(musicOffIcon, !sound.IsMuted);
    }
}
