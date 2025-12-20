using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI controller for a single deck slot in the DeckHubScene.
/// Handles presenting either an existing deck or an empty "New Deck" state
/// and exposes button callbacks to the DeckHubManager.
/// </summary>
public class DeckSlotUI : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("0-based index of this slot (0..3).")]
    public int slotIndex;

    [Header("UI References")]
    [SerializeField]
    private TextMeshProUGUI deckTitleText;

    [SerializeField]
    private GameObject addIconRoot;

    [SerializeField]
    private GameObject editIconRoot;

    [SerializeField]
    private GameObject deleteIconRoot;

    [SerializeField]
    private GameObject playIconRoot;

    [SerializeField]
    private Button addButton;

    [SerializeField]
    private Button editButton;

    [SerializeField]
    private Button deleteButton;

    [SerializeField]
    private Button playButton;

    /// <summary>True if this slot currently has a deck assigned.</summary>
    public bool HasDeck => _hasDeck;

    /// <summary>Current deck name (for display only).</summary>
    public string DeckName => _deckName;

    /// <summary>Firestore deck document ID, if any.</summary>
    public string DeckId => _deckId;

    /// <summary>Requested to create a new deck in this slot.</summary>
    public event Action<DeckSlotUI> CreateRequested;

    /// <summary>Requested to play this deck (only valid when HasDeck is true).</summary>
    public event Action<DeckSlotUI> PlayRequested;

    public event Action<DeckSlotUI> EditRequested;
    public event Action<DeckSlotUI> DeleteRequested;

    private bool _hasDeck;
    private string _deckName;
    private string _deckId;

    private void Awake()
    {
        if (addButton != null)
            addButton.onClick.AddListener(OnAddClicked);
        if (editButton != null)
            editButton.onClick.AddListener(OnEditClicked);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClicked);
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
    }

    private void OnDestroy()
    {
        if (addButton != null)
            addButton.onClick.RemoveListener(OnAddClicked);
        if (editButton != null)
            editButton.onClick.RemoveListener(OnEditClicked);
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteClicked);
        if (playButton != null)
            playButton.onClick.RemoveListener(OnPlayClicked);
    }

    public void SetEmptyState()
    {
        _hasDeck = false;
        _deckName = null;
        _deckId = null;

        if (deckTitleText != null)
            deckTitleText.text = "NEW DECK";

        // Show "Add" (create) but not "Play". Edit/Delete are visible but disabled.
        SetActiveSafe(addIconRoot, true);
        SetActiveSafe(playIconRoot, false);
        SetActiveSafe(editIconRoot, true);
        SetActiveSafe(deleteIconRoot, true);

        if (addButton != null)
            addButton.interactable = true; // create new deck
        if (playButton != null)
            playButton.interactable = false;
        if (editButton != null)
            editButton.interactable = false;  // cannot edit a non-existent deck
        if (deleteButton != null)
            deleteButton.interactable = false; // nothing to delete yet
    }

    public void SetDeck(string deckId, string deckName)
    {
        _hasDeck = true;
        _deckId = deckId;
        _deckName = string.IsNullOrEmpty(deckName) ? "Unnamed Deck" : deckName;

        if (deckTitleText != null)
            deckTitleText.text = _deckName;

        // Show "Play" instead of "Add" for a valid deck.
        SetActiveSafe(addIconRoot, false);
        SetActiveSafe(playIconRoot, true);
        SetActiveSafe(editIconRoot, true);
        SetActiveSafe(deleteIconRoot, true);

        if (addButton != null)
            addButton.interactable = false; // keep create separate from edit
        if (playButton != null)
            playButton.interactable = true;
        if (editButton != null)
            editButton.interactable = true;
        if (deleteButton != null)
            deleteButton.interactable = true;
    }

    private void OnAddClicked()
    {
        CreateRequested?.Invoke(this);
    }

    private void OnPlayClicked()
    {
        if (_hasDeck)
            PlayRequested?.Invoke(this);
    }

    private void OnEditClicked()
    {
        // Edit always opens the deck builder. When there's no deck yet,
        // this acts as "create deck".
        EditRequested?.Invoke(this);
    }

    private void OnDeleteClicked()
    {
        if (_hasDeck)
            DeleteRequested?.Invoke(this);
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}


