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
    private Button addButton;

    [SerializeField]
    private Button editButton;

    [SerializeField]
    private Button deleteButton;

    /// <summary>True if this slot currently has a deck assigned.</summary>
    public bool HasDeck => _hasDeck;

    /// <summary>Current deck name (for display only).</summary>
    public string DeckName => _deckName;

    /// <summary>Firestore deck document ID, if any.</summary>
    public string DeckId => _deckId;

    public event Action<DeckSlotUI> CreateRequested;
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
    }

    private void OnDestroy()
    {
        if (addButton != null)
            addButton.onClick.RemoveListener(OnAddClicked);
        if (editButton != null)
            editButton.onClick.RemoveListener(OnEditClicked);
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteClicked);
    }

    public void SetEmptyState()
    {
        _hasDeck = false;
        _deckName = null;
        _deckId = null;

        if (deckTitleText != null)
            deckTitleText.text = "NEW DECK";

        SetActiveSafe(addIconRoot, true);
        SetActiveSafe(editIconRoot, false);
        SetActiveSafe(deleteIconRoot, false);
    }

    public void SetDeck(string deckId, string deckName)
    {
        _hasDeck = true;
        _deckId = deckId;
        _deckName = string.IsNullOrEmpty(deckName) ? "Unnamed Deck" : deckName;

        if (deckTitleText != null)
            deckTitleText.text = _deckName;

        SetActiveSafe(addIconRoot, false);
        SetActiveSafe(editIconRoot, true);
        SetActiveSafe(deleteIconRoot, true);
    }

    private void OnAddClicked()
    {
        CreateRequested?.Invoke(this);
    }

    private void OnEditClicked()
    {
        if (_hasDeck)
            EditRequested?.Invoke(this);
        else
            CreateRequested?.Invoke(this);
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
