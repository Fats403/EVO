using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using TMPro;
using UnityEngine;

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

    // Resolved at runtime from children of loadingRoot so we only need to wire up one object.
    private TextMeshProUGUI loadingErrorText;

    [Header("Deck Slots")]
    [SerializeField]
    private DeckSlotUI[] deckSlots;

    [Header("Card Data")]
    [Tooltip("Global card database used to resolve cardIds when building decks.")]
    [SerializeField]
    private CardDatabase cardDatabase;

    private FirebaseManager Firebase => FirebaseManager.Instance;

    private void Awake()
    {
        if (loadingRoot != null)
        {
            loadingErrorText = loadingRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void Start()
    {
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

        while (!Firebase.IsFirebaseReady || !Firebase.IsLoggedIn)
        {
            await Task.Delay(stepMs);
            waited += stepMs;
            if (waited >= timeoutMs)
            {
                SetErrorState("Timed out waiting for Firebase login.");
                return;
            }
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
                slot.EditRequested -= OnEditRequested;
                slot.DeleteRequested -= OnDeleteRequested;

                slot.CreateRequested += OnCreateRequested;
                slot.EditRequested += OnEditRequested;
                slot.DeleteRequested += OnDeleteRequested;
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
        // TODO: Navigate to deck creation view and pass slot.slotIndex.
    }

    private void OnEditRequested(DeckSlotUI slot)
    {
        if (slot == null || !slot.HasDeck)
            return;

        Debug.Log(
            $"DeckHubManager: Edit requested for deck '{slot.DeckName}' (id={slot.DeckId}) in slot {slot.slotIndex}"
        );
        // TODO: Navigate to deck editor view with slot.DeckId.
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
}
