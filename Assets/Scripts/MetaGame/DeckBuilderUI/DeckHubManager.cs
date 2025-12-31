using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Top-level controller for the DeckHubScene.
/// - Waits for Firebase login.
/// - Loads up to 4 decks from Firestore for the current user.
/// - Binds them to the DeckSlotUI instances.
/// - Handles navigation to deck creation/editing (stubbed for now).
/// </summary>
public class DeckHubManager : MonoBehaviour
{
    [Header("UI Roots")]
    [Tooltip("Root object containing the loading spinner and error text.")]
    [SerializeField]
    private GameObject loadingRoot;

    [Tooltip("Root object containing the main deck hub UI (slots, background, etc.).")]
    [SerializeField]
    private GameObject deckHubRoot;

    [Tooltip("Root object for the deck builder view (card list, deck summary, save button).")]
    [SerializeField]
    private GameObject deckBuilderRoot;

    // Resolved at runtime from children of loadingRoot so we only need to wire up one object.
    private TextMeshProUGUI loadingErrorText;

    [Header("Deck Slots")]
    [SerializeField]
    private DeckSlotUI[] deckSlots;

    [Header("Card Data / Builder")]
    [Tooltip("Global card database used to resolve cardIds when building decks.")]
    [SerializeField]
    private CardDatabase cardDatabase;

    [Tooltip("Manager for the deck builder view.")]
    [SerializeField]
    private DeckBuilderManager deckBuilderManager;

    [Header("Draft")]
    [Tooltip("Root object containing the draft UI (moved from MainScene).")]
    [SerializeField]
    private GameObject draftRoot;

    [Tooltip("Draft manager controlling the draft flow in the hub scene.")]
    [SerializeField]
    private DraftManager draftManager;

    [Header("Matchmaking UI")]
    [Tooltip("Button that creates a Steam lobby for a head-to-head match.")]
    [SerializeField]
    private Button createLobbyButton;

    [Tooltip("Button that starts a local game vs AI using the selected deck.")]
    [SerializeField]
    private Button quickplayButton;

    [Tooltip("Button used by the invited player to join a lobby after selecting a deck.")]
    [SerializeField]
    private Button joinLobbyButton;

    [Tooltip("Button used to go back to the menu.")]
    [SerializeField]
    private Button backToMenuButton;

    [Tooltip("Button to resume an interrupted match.")]
    [SerializeField]
    private Button resumeMatchButton;

    [Tooltip("Text on the resume button to show opponent name.")]
    [SerializeField]
    private TextMeshProUGUI resumeMatchText;

    [Tooltip("Root object for the in-game lobby UI (Canvas_Lobby).")]
    [SerializeField]
    private GameObject gameLobbyRoot;

    private FirebaseManager Firebase => FirebaseManager.Instance;

    // Tracks which slot/deck is currently being edited/created.
    private int _activeSlotIndex = -1;
    private string _activeDeckId;

    // Currently selected deck slot for matchmaking actions.
    private DeckSlotUI _selectedSlot;

    private void Awake()
    {
        if (loadingRoot != null)
        {
            loadingErrorText = loadingRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Hub is the default view; hide the builder and draft at startup.
        if (deckBuilderRoot != null)
            deckBuilderRoot.SetActive(false);
        if (draftRoot != null)
            draftRoot.SetActive(false);

        if (deckBuilderManager != null)
            deckBuilderManager.DeckSaved += HandleDeckSaved;

        if (draftManager != null)
            draftManager.DeckBuilt += HandleDraftDeckBuilt;

        if (gameLobbyRoot != null)
            gameLobbyRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (deckBuilderManager != null)
            deckBuilderManager.DeckSaved -= HandleDeckSaved;

        if (draftManager != null)
            draftManager.DeckBuilt -= HandleDraftDeckBuilt;

        UnsubscribeFromSteamEvents();
    }

    private void SubscribeToSteamEvents()
    {
        if (SteamManager.Instance != null)
            SteamManager.Instance.OnInviteReady += HandleInviteReady;

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LobbyEntered += HandleLobbyEntered;
            SteamLobbyManager.Instance.LobbyLeft += HandleLobbyLeft;
        }
    }

    private void UnsubscribeFromSteamEvents()
    {
        if (SteamManager.Instance != null)
            SteamManager.Instance.OnInviteReady -= HandleInviteReady;

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LobbyEntered -= HandleLobbyEntered;
            SteamLobbyManager.Instance.LobbyLeft -= HandleLobbyLeft;
        }
    }

    private void HandleInviteReady(Steamworks.SteamId lobbyId)
    {
        Debug.Log($"DeckHubManager: Invite ready for lobby {lobbyId}");
        UpdateMatchActionButtons();
    }

    private void HandleLobbyEntered()
    {
        Debug.Log("DeckHubManager: Lobby entered");

        // Show the lobby UI for both host and guest when entering a lobby
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsInLobby)
        {
            if (deckHubRoot != null)
                deckHubRoot.SetActive(false);
            if (gameLobbyRoot != null)
                gameLobbyRoot.SetActive(true);
        }

        UpdateMatchActionButtons();
    }

    private void HandleLobbyLeft()
    {
        Debug.Log("DeckHubManager: Lobby left");

        // Return to hub UI
        if (gameLobbyRoot != null)
            gameLobbyRoot.SetActive(false);
        if (deckHubRoot != null)
            deckHubRoot.SetActive(true);

        UpdateMatchActionButtons();
    }

    private void Start()
    {
        SubscribeToSteamEvents();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        SetLoadingState(true, null);

        // Wait for Firebase login (defensive, in case user came here directly).
        if (Firebase == null)
        {
            SetErrorState("FirebaseManager instance not found.");
            return;
        }

        const int timeoutMs = 15000;
        int waited = 0;
        const int stepMs = 250;

        // Wait for Firebase to be ready and either logged in or have tried login
        while (!Firebase.IsFirebaseReady || (!Firebase.IsLoggedIn && !Firebase.HasTriedLogin))
        {
            await Task.Delay(stepMs);
            waited += stepMs;
            if (waited >= timeoutMs)
            {
                // Check if there's a specific error from Firebase
                string errorDetail = !string.IsNullOrEmpty(Firebase.LastLoginError)
                    ? $": {Firebase.LastLoginError}"
                    : ".";
                SetErrorState($"Timed out waiting for Firebase login{errorDetail}");
                return;
            }
        }

        // Check if login actually succeeded after waiting
        if (!Firebase.IsLoggedIn)
        {
            string errorDetail = !string.IsNullOrEmpty(Firebase.LastLoginError)
                ? Firebase.LastLoginError
                : "Login failed.";
            SetErrorState(errorDetail);
            return;
        }

        await LoadDecksAsync();
    }

    private async Task LoadDecksAsync()
    {
        if (Firebase.CurrentUser == null)
        {
            SetErrorState("No Firebase user is signed in.");
            return;
        }

        var db = Firebase.Db;
        var uid = Firebase.CurrentUser.UserId;

        try
        {
            var colRef = db.Collection("players").Document(uid).Collection("decks");
            var snap = await colRef.GetSnapshotAsync();

            var decks = new List<DeckSummary>();
            foreach (var doc in snap.Documents)
            {
                if (!doc.Exists)
                    continue;

                int slotIndex = doc.ContainsField("slotIndex")
                    ? doc.GetValue<int>("slotIndex")
                    : -1;
                string name = doc.ContainsField("name") ? doc.GetValue<string>("name") : "Deck";

                decks.Add(
                    new DeckSummary
                    {
                        deckId = doc.Id,
                        name = name,
                        slotIndex = slotIndex,
                    }
                );
            }

            BindDecksToSlots(decks);
            SetLoadingState(false, null);
        }
        catch (System.SystemException e)
        {
            SetErrorState($"Failed to load decks: {e.Message}");
        }
    }

    private void BindDecksToSlots(List<DeckSummary> decks)
    {
        if (deckSlots == null || deckSlots.Length == 0)
            return;

        // Clear all slots to empty first.
        foreach (var slot in deckSlots)
        {
            slot?.SetEmptyState();
            if (slot != null)
            {
                slot.CreateRequested -= OnCreateRequested;
                slot.SelectRequested -= OnSelectRequested;
                slot.EditRequested -= OnEditRequested;
                slot.DeleteRequested -= OnDeleteRequested;

                slot.CreateRequested += OnCreateRequested;
                slot.SelectRequested += OnSelectRequested;
                slot.EditRequested += OnEditRequested;
                slot.DeleteRequested += OnDeleteRequested;

                slot.SetSelected(false);
            }
        }

        if (decks == null || decks.Count == 0)
            return;

        foreach (var deck in decks)
        {
            if (deck.slotIndex < 0 || deck.slotIndex >= deckSlots.Length)
                continue;

            var slot = deckSlots[deck.slotIndex];
            if (slot == null)
                continue;

            slot.SetDeck(deck.deckId, deck.name);
        }

        // Reset selection when decks are rebound.
        _selectedSlot = null;
        UpdateMatchActionButtons();
    }

    private void SetLoadingState(bool isLoading, string errorMessage)
    {
        bool hasError = !string.IsNullOrEmpty(errorMessage);

        // Show loading root while we are loading OR when showing an error.
        if (loadingRoot != null)
            loadingRoot.SetActive(isLoading || hasError);

        // Only show the main hub when loading is finished and there is no error.
        if (deckHubRoot != null)
            deckHubRoot.SetActive(!isLoading && !hasError);

        if (loadingErrorText != null)
            loadingErrorText.text = hasError ? errorMessage : string.Empty;
    }

    private void SetErrorState(string message)
    {
        Debug.LogError($"DeckHubManager: {message}");
        SetLoadingState(false, message);
    }

    // ------------------------------------------------------------------------
    // Slot callbacks
    // ------------------------------------------------------------------------

    private void OnCreateRequested(DeckSlotUI slot)
    {
        if (slot == null)
            return;

        Debug.Log($"DeckHubManager: Create requested for slot {slot.slotIndex}");

        _activeSlotIndex = slot.slotIndex;
        _activeDeckId = null;

        deckBuilderRoot?.SetActive(true);
        deckHubRoot?.SetActive(false);
        loadingRoot?.SetActive(false);

        deckBuilderManager?.StartNewDeck($"Deck {slot.slotIndex + 1}");
    }

    private void OnSelectRequested(DeckSlotUI slot)
    {
        if (slot == null || !slot.HasDeck)
            return;

        // Clicking on a deck slot selects it. The actual game start is driven
        // by the Quickplay/CreateLobby buttons using the currently selected deck.
        Debug.Log(
            $"DeckHubManager: Deck selected '{slot.DeckName}' (id={slot.DeckId}) in slot {slot.slotIndex}"
        );

        SelectDeckSlot(slot);
    }

    private void OnEditRequested(DeckSlotUI slot)
    {
        if (slot == null)
            return;

        Debug.Log($"DeckHubManager: Edit requested for slot {slot.slotIndex}");

        _activeSlotIndex = slot.slotIndex;
        _activeDeckId = slot.HasDeck ? slot.DeckId : null;

        deckBuilderRoot?.SetActive(true);
        deckHubRoot?.SetActive(false);
        loadingRoot?.SetActive(false);

        if (deckBuilderManager != null)
        {
            if (slot.HasDeck)
            {
                // Load existing deck contents from Firestore into the builder.
                OpenExistingDeckForEdit(slot);
            }
            else
            {
                var defaultName = $"Deck {slot.slotIndex + 1}";
                deckBuilderManager.StartNewDeck(defaultName);
            }
        }
    }

    private async void OnDeleteRequested(DeckSlotUI slot)
    {
        if (slot == null || !slot.HasDeck)
            return;

        if (Firebase == null || Firebase.CurrentUser == null)
            return;

        var db = Firebase.Db;
        var uid = Firebase.CurrentUser.UserId;

        try
        {
            var docRef = db.Collection("players")
                .Document(uid)
                .Collection("decks")
                .Document(slot.DeckId);
            await docRef.DeleteAsync();

            // Clear UI state locally.
            slot.SetEmptyState();
        }
        catch (System.SystemException e)
        {
            Debug.LogError($"DeckHubManager: Failed to delete deck: {e.Message}");
        }
    }

    // Simple summary used for binding list -> slots.
    private class DeckSummary
    {
        public string deckId;
        public string name;
        public int slotIndex;
    }

    /// <summary>
    /// Called by a UI button in the DeckHubScene to open the draft UI. The
    /// resulting drafted (or random) deck will be converted into a constructed
    /// deck and passed into SelectedDeckStore before loading MainScene.
    /// </summary>
    public void OnClick_PlayDraft()
    {
        // Ensure the deterministic RNG is initialised for this run so that
        // draft and later gameplay share the same seed.
        if (!DeterministicRng.IsInitialized)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            DeterministicRng.Initialize(seed);
        }

        if (draftManager == null || draftRoot == null)
        {
            Debug.LogError("DeckHubManager: DraftManager or draftRoot not assigned.");
            return;
        }

        // Swap to the draft view.
        if (deckHubRoot != null)
            deckHubRoot.SetActive(false);
        if (deckBuilderRoot != null)
            deckBuilderRoot.SetActive(false);
        if (loadingRoot != null)
            loadingRoot.SetActive(false);
        draftRoot.SetActive(true);

        // Ensure the draftable pool is configured if not set via inspector.
        if (
            (draftManager.allDraftableCards == null || draftManager.allDraftableCards.Count == 0)
            && cardDatabase != null
            && cardDatabase.allCards != null
        )
        {
            draftManager.allDraftableCards.Clear();
            foreach (var def in cardDatabase.allCards)
            {
                if (def != null)
                    draftManager.allDraftableCards.Add(def);
            }
        }

        draftManager.BeginDraft();
    }

    /// <summary>
    /// Called by the \"Create Lobby\" button. Creates a Steam lobby for a
    /// head-to-head match using the currently selected deck.
    /// </summary>
    public async void OnClick_CreateLobby()
    {
        if (_selectedSlot == null || !_selectedSlot.HasDeck)
        {
            Debug.LogWarning("DeckHubManager: CreateLobby clicked with no deck selected.");
            return;
        }

        if (SteamLobbyManager.Instance == null)
        {
            Debug.LogError("DeckHubManager: SteamLobbyManager instance not found.");
            return;
        }

        // Load the full deck into SelectedDeckStore before creating the lobby
        bool loaded = await LoadDeckIntoStoreAsync(_selectedSlot);
        if (!loaded)
        {
            Debug.LogError("DeckHubManager: Failed to load deck for lobby creation.");
            return;
        }

        // Host picks the RNG seed for this match
        if (!DeterministicRng.IsInitialized)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            DeterministicRng.Initialize(seed);
        }

        // Start lobby creation - UI will be swapped in HandleLobbyEntered when lobby is ready
        SteamLobbyManager.Instance.CreateLobbyForMatch(
            _selectedSlot.DeckId,
            _selectedSlot.DeckName
        );

        // Note: UI switching moved to HandleLobbyEntered() to avoid race condition
        // The lobby UI should only appear after the lobby is actually created
    }

    /// <summary>
    /// Called by the Quickplay button. Starts a local game vs AI using the
    /// currently selected deck.
    /// </summary>
    public void OnClick_Quickplay()
    {
        if (_selectedSlot == null || !_selectedSlot.HasDeck)
        {
            Debug.LogWarning("DeckHubManager: Quickplay clicked with no deck selected.");
            return;
        }

        _ = StartGameWithDeckAsync(_selectedSlot);
    }

    /// <summary>
    /// Called by the Join button when the user has arrived in DeckHubScene
    /// via an invite and selected a deck to use for the lobby.
    /// </summary>
    public async void OnClick_JoinLobby()
    {
        if (_selectedSlot == null || !_selectedSlot.HasDeck)
        {
            Debug.LogWarning("DeckHubManager: JoinLobby clicked with no deck selected.");
            return;
        }

        var lobby = SteamLobbyManager.Instance;
        if (lobby == null)
        {
            Debug.LogError("DeckHubManager: SteamLobbyManager instance not found.");
            return;
        }

        // Load the full deck into SelectedDeckStore before joining the lobby
        bool loaded = await LoadDeckIntoStoreAsync(_selectedSlot);
        if (!loaded)
        {
            Debug.LogError("DeckHubManager: Failed to load deck for lobby join.");
            return;
        }

        // If we have a pending invite but haven't joined yet, join now
        if (lobby.HasPendingInvite && !lobby.IsInLobby)
        {
            Debug.Log("DeckHubManager: Joining pending invite lobby...");

            bool success = await lobby.JoinPendingInviteAsync();
            if (!success)
            {
                Debug.LogError("DeckHubManager: Failed to join lobby from invite.");
                return;
            }
        }

        if (!lobby.IsInLobby)
        {
            Debug.LogError("DeckHubManager: JoinLobby clicked but no active lobby is present.");
            return;
        }

        // Set our deck info in the lobby
        lobby.SetLocalLobbyDeck(_selectedSlot.DeckId, _selectedSlot.DeckName);

        // Swap to lobby UI
        if (deckHubRoot != null)
            deckHubRoot.SetActive(false);
        if (gameLobbyRoot != null)
            gameLobbyRoot.SetActive(true);
    }

    /// <summary>
    /// Called by a UI button in the deck builder view to return to the main
    /// deck hub without saving changes.
    /// </summary>
    public void OnClick_BackFromDeckBuilder()
    {
        // Clear any editing context.
        _activeSlotIndex = -1;
        _activeDeckId = null;

        // Swap views: hide builder, show hub.
        if (deckBuilderRoot != null)
            deckBuilderRoot.SetActive(false);
        if (deckHubRoot != null)
            deckHubRoot.SetActive(true);
        if (loadingRoot != null)
            loadingRoot.SetActive(false);

        // Optionally refresh deck summaries from Firestore so the hub view is up-to-date.
        _ = LoadDecksAsync();
    }

    /// <summary>
    /// Called by a button in the draft view to cancel drafting and return to
    /// the main deck hub without starting a game.
    /// </summary>
    public void OnClick_CancelDraft()
    {
        if (draftRoot != null)
            draftRoot.SetActive(false);
        if (deckHubRoot != null)
            deckHubRoot.SetActive(true);
        if (loadingRoot != null)
            loadingRoot.SetActive(false);
    }

    private async void HandleDeckSaved(string deckName, List<DeckCardEntry> cards)
    {
        if (Firebase == null || Firebase.CurrentUser == null)
        {
            Debug.LogError("DeckHubManager: Cannot save deck – no Firebase user.");
            return;
        }

        if (_activeSlotIndex < 0)
        {
            Debug.LogWarning("DeckHubManager: DeckSaved received with no active slot.");
            return;
        }

        var db = Firebase.Db;
        var uid = Firebase.CurrentUser.UserId;

        try
        {
            var decksCol = db.Collection("players").Document(uid).Collection("decks");
            DocumentReference docRef = !string.IsNullOrEmpty(_activeDeckId)
                ? decksCol.Document(_activeDeckId)
                : decksCol.Document(); // auto-id for new deck

            var cardList = new List<Dictionary<string, object>>();
            if (cards != null)
            {
                foreach (var c in cards)
                {
                    if (string.IsNullOrEmpty(c.cardId) || c.count <= 0)
                        continue;

                    cardList.Add(
                        new Dictionary<string, object>
                        {
                            { "cardId", c.cardId },
                            { "count", c.count },
                        }
                    );
                }
            }

            var data = new Dictionary<string, object>
            {
                { "name", deckName },
                { "slotIndex", _activeSlotIndex },
                { "cards", cardList },
                { "updatedAt", FieldValue.ServerTimestamp },
            };

            if (string.IsNullOrEmpty(_activeDeckId))
                data["createdAt"] = FieldValue.ServerTimestamp;

            await docRef.SetAsync(data, SetOptions.MergeAll);

            _activeDeckId = docRef.Id;

            // After saving, return to the hub view and refresh decks.
            if (deckBuilderRoot != null)
                deckBuilderRoot.SetActive(false);
            if (deckHubRoot != null)
                deckHubRoot.SetActive(true);

            await LoadDecksAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DeckHubManager: Failed to save deck: {e.Message}");
        }
    }

    /// <summary>
    /// Receives a completed drafted or random deck from DraftManager, converts
    /// it into DeckCardEntry data, stores it in SelectedDeckStore as a
    /// constructed deck, and then transitions to the main gameplay scene.
    /// </summary>
    private void HandleDraftDeckBuilt(List<ScriptableObject> draftedCards)
    {
        if (draftedCards == null || draftedCards.Count == 0)
        {
            Debug.LogWarning("DeckHubManager: Draft produced an empty deck.");
            return;
        }

        // Group by cardId to build DeckCardEntry data.
        var counts = new Dictionary<string, int>();
        foreach (var so in draftedCards)
        {
            if (so == null)
                continue;

            var def = so as CardDefinition;
            if (def == null || string.IsNullOrEmpty(def.cardId))
                continue;

            if (!counts.ContainsKey(def.cardId))
                counts[def.cardId] = 0;
            counts[def.cardId]++;
        }

        var entries = new List<DeckCardEntry>();
        foreach (var kvp in counts)
        {
            entries.Add(new DeckCardEntry { cardId = kvp.Key, count = kvp.Value });
        }

        if (entries.Count == 0)
        {
            Debug.LogError("DeckHubManager: Draft deck contained no valid card entries.");
            return;
        }

        // Treat drafted decks as constructed for the purposes of game startup.
        SelectedDeckStore.SetConstructedDeck(
            deckId: null,
            deckName: "Draft Deck",
            slotIndex: -1,
            cards: entries
        );

        // Optionally hide the draft UI before transitioning.
        if (draftRoot != null)
            draftRoot.SetActive(false);

        SceneTransitionManager.Instance.LoadScene("MainScene");
    }

    public void OnClick_BackToMenu()
    {
        SceneTransitionManager.Instance.LoadScene("MainMenu");
    }

    private async void OpenExistingDeckForEdit(DeckSlotUI slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.DeckId))
        {
            // Fallback: start a fresh deck if we don't have an id.
            deckBuilderManager?.StartNewDeck(slot != null ? slot.DeckName : "Deck");
            return;
        }

        if (Firebase == null || Firebase.CurrentUser == null || deckBuilderManager == null)
            return;

        var db = Firebase.Db;
        var uid = Firebase.CurrentUser.UserId;

        try
        {
            var docRef = db.Collection("players")
                .Document(uid)
                .Collection("decks")
                .Document(slot.DeckId);
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists)
            {
                deckBuilderManager.StartNewDeck(slot.DeckName);
                return;
            }

            var dict = snap.ToDictionary();
            string name =
                dict.TryGetValue("name", out var nameObj)
                && nameObj is string sName
                && !string.IsNullOrEmpty(sName)
                    ? sName
                    : slot.DeckName;

            var entries = new List<DeckCardEntry>();
            if (
                dict.TryGetValue("cards", out var cardsObj)
                && cardsObj is IEnumerable<object> rawCards
            )
            {
                foreach (var raw in rawCards)
                {
                    if (raw is Dictionary<string, object> cardMap)
                    {
                        if (
                            cardMap.TryGetValue("cardId", out var idObj)
                            && idObj is string id
                            && cardMap.TryGetValue("count", out var countObj)
                            && countObj is long lCount
                        )
                        {
                            entries.Add(new DeckCardEntry { cardId = id, count = (int)lCount });
                        }
                    }
                }
            }

            deckBuilderManager.LoadFromExistingDeck(name, entries);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DeckHubManager: Failed to load deck for edit: {e.Message}");
            deckBuilderManager.StartNewDeck(slot.DeckName);
        }
    }

    /// <summary>
    /// Loads the full deck data from Firestore and stores it in SelectedDeckStore.
    /// Used by both CreateLobby (host) and JoinLobby (guest) to prepare deck data
    /// for the network handshake.
    /// </summary>
    /// <returns>True if the deck was loaded successfully.</returns>
    private async Task<bool> LoadDeckIntoStoreAsync(DeckSlotUI slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.DeckId))
        {
            Debug.LogError("DeckHubManager: Cannot load deck – invalid slot or deck ID.");
            return false;
        }

        if (Firebase == null || Firebase.CurrentUser == null)
        {
            Debug.LogError("DeckHubManager: Cannot load deck – no Firebase user.");
            return false;
        }

        var db = Firebase.Db;
        var uid = Firebase.CurrentUser.UserId;

        try
        {
            var docRef = db.Collection("players")
                .Document(uid)
                .Collection("decks")
                .Document(slot.DeckId);
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists)
            {
                Debug.LogError("DeckHubManager: Selected deck does not exist in Firestore.");
                return false;
            }

            var dict = snap.ToDictionary();
            string name =
                dict.TryGetValue("name", out var nameObj)
                && nameObj is string sName
                && !string.IsNullOrEmpty(sName)
                    ? sName
                    : slot.DeckName;

            var entries = new List<DeckCardEntry>();
            if (
                dict.TryGetValue("cards", out var cardsObj)
                && cardsObj is IEnumerable<object> rawCards
            )
            {
                foreach (var raw in rawCards)
                {
                    if (raw is Dictionary<string, object> cardMap)
                    {
                        if (
                            cardMap.TryGetValue("cardId", out var idObj)
                            && idObj is string id
                            && cardMap.TryGetValue("count", out var countObj)
                            && countObj is long lCount
                        )
                        {
                            entries.Add(new DeckCardEntry { cardId = id, count = (int)lCount });
                        }
                    }
                }
            }

            SelectedDeckStore.SetConstructedDeck(slot.DeckId, name, slot.slotIndex, entries);
            Debug.Log(
                $"DeckHubManager: Loaded deck '{name}' with {entries.Count} unique cards into store."
            );
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DeckHubManager: Failed to load deck into store: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads the selected deck from Firestore into the SelectedDeckStore and
    /// transitions to the main gameplay scene in constructed mode.
    /// </summary>
    private async Task StartGameWithDeckAsync(DeckSlotUI slot)
    {
        // Reuse the shared loading logic
        bool loaded = await LoadDeckIntoStoreAsync(slot);
        if (!loaded)
            return;

        // Ensure the deterministic RNG is initialised for this run if it
        // has not already been set (e.g., for constructed play without
        // going through the draft flow).
        if (!DeterministicRng.IsInitialized)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            DeterministicRng.Initialize(seed);
        }

        SceneTransitionManager.Instance.LoadScene("MainScene");
    }

    private void Update()
    {
        // Keep the main action buttons in sync with selection and lobby state.
        UpdateMatchActionButtons();
    }

    private void SelectDeckSlot(DeckSlotUI slot)
    {
        _selectedSlot = slot;

        if (deckSlots != null)
        {
            foreach (var s in deckSlots)
            {
                if (s != null)
                    s.SetSelected(s == _selectedSlot);
            }
        }

        UpdateMatchActionButtons();
    }

    private void UpdateMatchActionButtons()
    {
        bool hasSelectedDeck = _selectedSlot != null && _selectedSlot.HasDeck;
        var lobby = SteamLobbyManager.Instance;
        var checkpoint = MatchCheckpointManager.Instance;

        // Host actions (Create Lobby, Quickplay) are available when not in a lobby
        bool canHostActions = lobby == null || !lobby.IsInLobby;

        if (createLobbyButton != null)
            createLobbyButton.interactable = hasSelectedDeck && canHostActions;

        if (quickplayButton != null)
            quickplayButton.interactable = hasSelectedDeck && canHostActions;

        // Join button is visible when:
        // 1. We have a pending invite (haven't joined yet), OR
        // 2. We're already in a lobby as a guest
        bool hasPendingInvite = lobby != null && lobby.HasPendingInvite;
        bool isGuestInLobby = lobby != null && lobby.IsInLobby && !lobby.IsHost;
        bool showJoinButton = hasPendingInvite || isGuestInLobby;

        if (joinLobbyButton != null)
        {
            joinLobbyButton.gameObject.SetActive(showJoinButton);
            joinLobbyButton.interactable = hasSelectedDeck && showJoinButton;
        }

        // Resume Match button - show if there's an active match to resume
        bool hasActiveMatch = checkpoint != null && checkpoint.HasActiveMatch;

        if (resumeMatchButton != null)
        {
            resumeMatchButton.gameObject.SetActive(hasActiveMatch && canHostActions);
            resumeMatchButton.interactable = hasSelectedDeck && hasActiveMatch && canHostActions;

            // Update button text with opponent name and round
            if (hasActiveMatch && resumeMatchText != null)
            {
                var match = checkpoint.ActiveMatch;
                resumeMatchText.text =
                    $"Resume vs {match.opponentName}\n(Round {match.lastCheckpointRound})";
            }
        }
    }

    /// <summary>
    /// Called when the Resume Match button is clicked.
    /// Creates a lobby and prepares to resume the match.
    /// </summary>
    public void OnClick_ResumeMatch()
    {
        var checkpoint = MatchCheckpointManager.Instance;
        if (checkpoint == null || !checkpoint.HasActiveMatch)
        {
            Debug.LogWarning("DeckHubManager: No active match to resume.");
            return;
        }

        Debug.Log($"DeckHubManager: Resuming match {checkpoint.ActiveMatch.matchId}...");

        // Initialize checkpoint manager for resume
        checkpoint.InitializeForResume(checkpoint.ActiveMatch);

        // Create a lobby like normal - the opponent will need to be invited
        OnClick_CreateLobby();

        // Show a message about inviting opponent
        FeedbackManager.Instance?.ShowGlobalAlert(
            $"Invite {checkpoint.ActiveMatch.opponentName} to continue your match!",
            Color.cyan
        );
    }

    /// <summary>
    /// Called when the Abandon Match button is clicked (optional).
    /// </summary>
    public void OnClick_AbandonMatch()
    {
        var checkpoint = MatchCheckpointManager.Instance;
        if (checkpoint != null)
        {
            Debug.Log("DeckHubManager: Abandoning active match.");
            checkpoint.ClearActiveMatch();
            UpdateMatchActionButtons();
        }
    }
}
