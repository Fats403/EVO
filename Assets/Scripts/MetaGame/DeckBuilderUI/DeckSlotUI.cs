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

    [Header("Selection Visuals")]
    [SerializeField]
    private GameObject selectedPlaqueRoot;

    [Header("Buttons")]
    [Tooltip("Button for creating a new deck in this slot (typically an Add icon).")]
    [SerializeField]
    private Button addButton;

    [Tooltip("Button for editing this deck.")]
    [SerializeField]
    private Button editButton;

    [Tooltip("Button for deleting this deck.")]
    [SerializeField]
    private Button deleteButton;

    [Tooltip(
        "Button that selects this deck slot. Wire this to the background image's Button component."
    )]
    [SerializeField]
    private Button selectButton;

    /// <summary>True if this slot currently has a deck assigned.</summary>
    public bool HasDeck => _hasDeck;

    /// <summary>Current deck name (for display only).</summary>
    public string DeckName => _deckName;

    /// <summary>Firestore deck document ID, if any.</summary>
    public string DeckId => _deckId;

    /// <summary>Requested to create a new deck in this slot.</summary>
    public event Action<DeckSlotUI> CreateRequested;

    /// <summary>Requested to select this deck slot (only valid when HasDeck is true).</summary>
    public event Action<DeckSlotUI> SelectRequested;

    public event Action<DeckSlotUI> EditRequested;
    public event Action<DeckSlotUI> DeleteRequested;

    private bool _hasDeck;
    private string _deckName;
    private string _deckId;

    private bool _isSelected;
    private Vector3 _baseScale;

    private void Awake()
    {
        _baseScale = transform.localScale;

        if (addButton != null)
            addButton.onClick.AddListener(OnAddClicked);
        if (editButton != null)
            editButton.onClick.AddListener(OnEditClicked);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClicked);
        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);
    }

    private void OnDestroy()
    {
        if (addButton != null)
            addButton.onClick.RemoveListener(OnAddClicked);
        if (editButton != null)
            editButton.onClick.RemoveListener(OnEditClicked);
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteClicked);
        if (selectButton != null)
            selectButton.onClick.RemoveListener(OnSelectClicked);
    }

    public void SetEmptyState()
    {
        _hasDeck = false;
        _deckName = null;
        _deckId = null;
        _isSelected = false;
        ApplySelectionVisuals();

        if (deckTitleText != null)
            deckTitleText.text = "NEW DECK";

        // Show only the "Add" (create) icon when there is no deck data.
        SetActiveSafe(addIconRoot, true);
        SetActiveSafe(editIconRoot, false);
        SetActiveSafe(deleteIconRoot, false);

        if (addButton != null)
            addButton.interactable = true; // create new deck
        if (selectButton != null)
            selectButton.interactable = false; // can't select an empty slot
        if (editButton != null)
            editButton.interactable = false; // cannot edit a non-existent deck
        if (deleteButton != null)
            deleteButton.interactable = false; // nothing to delete yet
    }

    public void SetDeck(string deckId, string deckName)
    {
        _hasDeck = true;
        _deckId = deckId;
        _deckName = string.IsNullOrEmpty(deckName) ? "Unnamed Deck" : deckName;
        _isSelected = false;

        if (deckTitleText != null)
            deckTitleText.text = _deckName;

        // Hide "Add" icon when a deck exists, and show Edit/Delete controls.
        SetActiveSafe(addIconRoot, false);
        SetActiveSafe(editIconRoot, true);
        SetActiveSafe(deleteIconRoot, true);

        if (addButton != null)
            addButton.interactable = false; // keep create separate from edit
        if (selectButton != null)
            selectButton.interactable = true; // can select this deck
        if (editButton != null)
            editButton.interactable = true;
        if (deleteButton != null)
            deleteButton.interactable = true;
    }

    private void OnAddClicked()
    {
        CreateRequested?.Invoke(this);
    }

    private void OnSelectClicked()
    {
        if (_hasDeck)
            SelectRequested?.Invoke(this);
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

    /// <summary>
    /// Updates this slot's visual selection state. Intended to be driven by
    /// the DeckHubManager when the user selects a single active deck.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        ApplySelectionVisuals();
    }

    private void ApplySelectionVisuals()
    {
        // Toggle the "SELECTED" plaque if one is wired.
        SetActiveSafe(selectedPlaqueRoot, _isSelected && _hasDeck);
    }
}
