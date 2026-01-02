using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Global Firebase manager for:
/// - Initializing Firebase (Auth + Firestore).
/// - Automatically logging in via Steam on startup using your authSteam backend.
/// - Providing simple helpers for player data / decks in Firestore.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    [Header("Backend Settings")]
    [Tooltip("HTTP endpoint for your authSteam backend (Cloud Run / Cloud Function).")]
    [SerializeField]
    private readonly string authSteamUrl = "https://authsteam-dh567oabqa-uc.a.run.app";

    /// <summary>True when Firebase is initialized and ready.</summary>
    public bool IsFirebaseReady { get; private set; }

    /// <summary>True once we've attempted auto-login.</summary>
    public bool HasTriedLogin { get; private set; }

    /// <summary>True if the last auto-login attempt succeeded.</summary>
    public bool IsLoggedIn { get; private set; }

    /// <summary>Last login error message, if any.</summary>
    public string LastLoginError { get; private set; }

    /// <summary>True if Firestore is available. False if locked by another process.</summary>
    public bool IsFirestoreAvailable => Db != null;

    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore Db { get; private set; }
    public FirebaseUser CurrentUser => Auth?.CurrentUser;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fire-and-forget with proper exception handling to prevent crashes
        SafeInitializeAsync();
    }

    /// <summary>
    /// Wraps the async initialization in a try-catch to prevent unhandled
    /// exceptions from crashing the game on startup.
    /// </summary>
    private async void SafeInitializeAsync()
    {
        try
        {
            await InitializeAndAutoLoginAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Unhandled exception during initialization: {e}");
            HasTriedLogin = true;
            IsLoggedIn = false;
            LastLoginError = $"Initialization failed: {e.Message}";
        }
    }

    /// <summary>
    /// Initializes Firebase and, once both Firebase and Steam are ready,
    /// automatically performs the Steam → backend → Firebase login flow.
    ///
    /// IMPORTANT:
    /// - If Firebase already has a cached user from a previous session,
    ///   we *reuse* that session and DO NOT call the authSteam backend again.
    /// - This means you only mint a custom token the first time (or after
    ///   an explicit sign‑out), which prevents hammering your backend and
    ///   avoids Steam 429s on every game launch.
    /// </summary>
    private async Task InitializeAndAutoLoginAsync()
    {
        await InitializeFirebaseAsync();

        if (!IsFirebaseReady)
            return;

        // --------------------------------------------------------------
        // 1. Reuse cached Firebase session if available
        // --------------------------------------------------------------
        // Firebase Auth persists the user across app restarts and will
        // refresh ID tokens automatically. If we already have a user,
        // we treat that as "logged in" and skip the Steam → backend call.
        if (Auth != null && Auth.CurrentUser != null)
        {
            IsLoggedIn = true;
            HasTriedLogin = true;
            LastLoginError = null;
            Debug.Log(
                $"FirebaseManager: Reusing cached Firebase user uid={Auth.CurrentUser.UserId} – skipping authSteam call."
            );
            return;
        }

        // --------------------------------------------------------------
        // 2. No cached user → perform normal Steam‑based login
        // --------------------------------------------------------------
        // Wait for Steam to be initialized by SteamManager.
        // This will silently wait in the background during your main-menu loading state.
        const int maxWaitMs = 15000; // 15 seconds safety timeout
        int waited = 0;
        const int stepMs = 250;

        while (!SteamClient.IsValid && waited < maxWaitMs)
        {
            await Task.Delay(stepMs);
            waited += stepMs;
        }

        if (!SteamClient.IsValid)
        {
            LastLoginError = "Steam client was not ready in time for Firebase login.";
            HasTriedLogin = true;
            IsLoggedIn = false;
            Debug.LogWarning($"FirebaseManager: {LastLoginError}");
            return;
        }

        // Perform the actual login via Steam → authSteam → Firebase custom token.
        await LoginWithSteamAsync();
    }

    /// <summary>
    /// Optional helper to explicitly sign out the current Firebase user.
    /// After calling this, the next startup (or manual login call) will
    /// go through the full Steam → backend flow again.
    /// </summary>
    public void SignOut()
    {
        if (Auth == null)
            return;

        Auth.SignOut();
        IsLoggedIn = false;
        HasTriedLogin = false;
        LastLoginError = null;
        Debug.Log("FirebaseManager: Signed out current Firebase user.");
    }

    private async Task InitializeFirebaseAsync()
    {
        try
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError(
                    $"FirebaseManager: Firebase dependencies not available: {dependencyStatus}"
                );
                IsFirebaseReady = false;
                return;
            }

            Auth = FirebaseAuth.DefaultInstance;

            // Configure Firestore - may return null if database is locked
            Db = ConfigureFirestore();

            // Firebase is ready even if Firestore is unavailable (Auth still works)
            IsFirebaseReady = true;

            if (Db != null)
            {
                Debug.Log("FirebaseManager: Firebase initialized with Firestore.");
            }
            else
            {
                Debug.LogWarning(
                    "FirebaseManager: Firebase initialized WITHOUT Firestore. "
                        + "Auth works but cloud saves are disabled."
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: InitializeFirebaseAsync error: {e}");
            IsFirebaseReady = false;
        }
    }

    /// <summary>
    /// Safely initializes Firestore. On standalone builds, we disable persistence
    /// to avoid LevelDB lock conflicts with the Unity Editor.
    /// </summary>
    private FirebaseFirestore ConfigureFirestore()
    {
        FirebaseFirestore firestore = FirebaseFirestore.DefaultInstance;
        return firestore;
    }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    /// <summary>
    /// Checks if the Firestore LevelDB database is locked by another process.
    /// Updated to use a more reliable path for macOS.
    /// </summary>
    private bool IsFirestoreLocked()
    {
        try
        {
            // Get the user's home directory manually to avoid SpecialFolder issues
            string homePath = System.Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(homePath))
                return false;

            string firestorePath = System.IO.Path.Combine(
                homePath,
                "Library/Application Support/firestore"
            );

            if (!System.IO.Directory.Exists(firestorePath))
                return false;

            string[] lockFiles = System.IO.Directory.GetFiles(
                firestorePath,
                "LOCK",
                System.IO.SearchOption.AllDirectories
            );

            foreach (string lockFile in lockFiles)
            {
                try
                {
                    using (
                        var fs = new System.IO.FileStream(
                            lockFile,
                            System.IO.FileMode.Open,
                            System.IO.FileAccess.ReadWrite,
                            System.IO.FileShare.None
                        )
                    )
                    {
                        // File is not locked
                    }
                }
                catch (System.IO.IOException)
                {
                    return true; // Locked!
                }
            }
        }
        catch { }
        return false;
    }
#endif

    /// <summary>
    /// Public login method in case you ever want to trigger login manually.
    /// Normally this is called automatically once Firebase + Steam are ready.
    /// </summary>
    public async Task<bool> LoginWithSteamAsync()
    {
        HasTriedLogin = true;
        LastLoginError = null;
        IsLoggedIn = false;

        if (!IsFirebaseReady)
        {
            LastLoginError = "Firebase not ready for login.";
            Debug.LogWarning($"FirebaseManager: {LastLoginError}");
            return false;
        }

        if (!SteamClient.IsValid)
        {
            LastLoginError = "SteamClient not initialized. Make sure SteamManager ran first.";
            Debug.LogError($"FirebaseManager: {LastLoginError}");
            return false;
        }

        AuthTicket ticket = default;
        bool hasTicket = false;
        try
        {
            // 1. Get Steam auth session ticket asynchronously.
            //    See Facepunch docs: https://wiki.facepunch.com/steamworks/SteamUser.GetAuthSessionTicketAsync
            var identity = (NetIdentity)SteamClient.SteamId;
            ticket = await SteamUser.GetAuthSessionTicketAsync(identity);
            hasTicket = true;
            byte[] ticketBytes = ticket.Data;
            string ticketBase64 = Convert.ToBase64String(ticketBytes);
            string steamId = SteamClient.SteamId.Value.ToString();

            // 2. Call backend to exchange for Firebase custom token.
            string customToken = await RequestFirebaseCustomTokenAsync(steamId, ticketBase64);
            if (string.IsNullOrEmpty(customToken))
            {
                LastLoginError = "Backend did not return a firebaseCustomToken.";
                Debug.LogError($"FirebaseManager: {LastLoginError}");
                IsLoggedIn = false;
                return false;
            }

            AuthResult result = await Auth.SignInWithCustomTokenAsync(customToken);
            Debug.Log($"FirebaseManager: Signed into Firebase as uid={result.User.UserId}");

            IsLoggedIn = true;
            return true;
        }
        catch (Exception e)
        {
            LastLoginError = $"LoginWithSteamAsync error: {e.Message}";
            Debug.LogError($"FirebaseManager: {LastLoginError}");
            IsLoggedIn = false;
            return false;
        }
        finally
        {
            // Clean up the ticket on our side.
            if (hasTicket)
            {
                ticket.Cancel();
            }
        }
    }

    /// <summary>
    /// Sends steamId + auth ticket to your authSteam backend and parses the Firebase custom token.
    /// </summary>
    private async Task<string> RequestFirebaseCustomTokenAsync(string steamId, string ticketBase64)
    {
        var requestBody = new SteamAuthRequest { steamId = steamId, ticket = ticketBase64 };

        string json = JsonUtility.ToJson(requestBody);

        using var request = new UnityWebRequest(authSteamUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        var op = request.SendWebRequest();
        while (!op.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"FirebaseManager: RequestFirebaseCustomTokenAsync error: {request.error}"
            );
            return null;
        }

        string responseJson = request.downloadHandler.text;
        var response = JsonUtility.FromJson<SteamAuthResponse>(responseJson);

        if (!string.IsNullOrEmpty(response.error))
        {
            Debug.LogError($"FirebaseManager: Backend returned error: {response.error}");
            return null;
        }

        return response.firebaseCustomToken;
    }

    /// <summary>Convenience accessor for the player's root document: players/{uid}</summary>
    public DocumentReference GetPlayerDoc()
    {
        if (!IsFirebaseReady || CurrentUser == null)
        {
            Debug.LogWarning("FirebaseManager: GetPlayerDoc called before login.");
            return null;
        }

        if (Db == null)
        {
            Debug.LogWarning(
                "FirebaseManager: Firestore unavailable (database locked by Unity Editor?)."
            );
            return null;
        }

        string uid = CurrentUser.UserId;
        Debug.Log($"FirebaseManager: GetPlayerDoc using uid=\"{uid}\" (path: players/{uid})");
        
        // If you get permission errors, check that this uid matches your Firestore rules.
        // Rules expect: request.auth.uid == playerId
        // If your rules expect "steam:{ID}" format but the uid is just "{ID}", you'll get denied.
        return Db.Collection("players").Document(uid);
    }

    /// <summary>Loads the player's root document as a dictionary.</summary>
    public async Task<Dictionary<string, object>> LoadPlayerDataAsync()
    {
        var doc = GetPlayerDoc();
        if (doc == null)
            return null;

        var snap = await doc.GetSnapshotAsync();
        return snap.Exists ? snap.ToDictionary() : new Dictionary<string, object>();
    }

    /// <summary>Merges the given data into the player's root document.</summary>
    public async Task SavePlayerDataAsync(Dictionary<string, object> data)
    {
        var doc = GetPlayerDoc();
        if (doc == null)
            return;

        await doc.SetAsync(data, SetOptions.MergeAll);
    }

    /// <summary>Example helper for a deck under players/{uid}/decks/{deckId}.</summary>
    public async Task SaveDeckAsync(string deckId, Dictionary<string, object> deckData)
    {
        if (!IsFirebaseReady || CurrentUser == null)
        {
            Debug.LogWarning("FirebaseManager: SaveDeckAsync called before login.");
            return;
        }

        if (Db == null)
        {
            Debug.LogWarning("FirebaseManager: Cannot save deck - Firestore unavailable.");
            return;
        }

        var deckRef = Db.Collection("players")
            .Document(CurrentUser.UserId)
            .Collection("decks")
            .Document(deckId);

        await deckRef.SetAsync(deckData, SetOptions.MergeAll);
    }

    public async Task<DocumentSnapshot> LoadDeckAsync(string deckId)
    {
        if (!IsFirebaseReady || CurrentUser == null)
        {
            Debug.LogWarning("FirebaseManager: LoadDeckAsync called before login.");
            return null;
        }

        if (Db == null)
        {
            Debug.LogWarning("FirebaseManager: Cannot load deck - Firestore unavailable.");
            return null;
        }

        var deckRef = Db.Collection("players")
            .Document(CurrentUser.UserId)
            .Collection("decks")
            .Document(deckId);

        return await deckRef.GetSnapshotAsync();
    }

    // ------------------------------------------------------------------
    // DTOs for JSON serialization
    // ------------------------------------------------------------------

    [Serializable]
    private struct SteamAuthRequest
    {
        public string steamId;
        public string ticket;
    }

    [Serializable]
    private struct SteamAuthResponse
    {
        public string firebaseCustomToken;
        public string error; // optional field from backend
    }
}
