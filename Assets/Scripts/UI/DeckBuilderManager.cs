using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the deck-building UI:
/// - Shows all cards (from CardDatabase) with + / - / count controls.
/// - Tracks current deck composition, respecting max deck size.
/// - Updates a simple text list of cards in the deck.
/// - Save button is wired to a callback (Firestore integration can be added later).
/// </summary>
public class DeckBuilderManager : MonoBehaviour
{
    [Header("Data Sources")]
    [SerializeField]
    private CardDatabase cardDatabase;

    [Tooltip("Optional: used to get the deckSize limit. If null, defaultDeckSize is used instead.")]
    [SerializeField]
    private DeckManager deckSizeSource;

    [Tooltip("Fallback deck size if no DeckManager is provided.")]
    [SerializeField]
    private int defaultDeckSize = 20;

    [Tooltip("Maximum copies of any single card allowed in a deck.")]
    [SerializeField]
    private int maxCopiesPerCard = 2;

    [Header("Left Panel - Card Collection")]
    [Tooltip("Content transform of the ScrollRect that lists all available cards.")]
    [SerializeField]
    private Transform collectionContentRoot;

    [Tooltip("Prefab with DeckBuilderCardItem + preview root + +/- UI.")]
    [SerializeField]
    private GameObject cardItemPrefab;

    [Header("Right Panel - Deck Summary")]
    [SerializeField]
    private TMP_InputField deckNameInput;

    [Tooltip("Content transform of the ScrollRect that lists cards currently in the deck.")]
    [SerializeField]
    private Transform deckListContentRoot;

    [Tooltip("Prefab for a simple text entry in the deck list (e.g., '2x Raptors').")]
    [SerializeField]
    private TextMeshProUGUI deckListItemPrefab;

    [SerializeField]
    private Button saveButton;

    private int _maxDeckSize;

    // cardId -> count
    private readonly Dictionary<string, int> _cardCounts = new();

    // All instantiated card items in the collection list.
    private readonly List<DeckBuilderCardItem> _items = new();

    /// <summary>
    /// Raised after a successful Save click (deck is valid).
    /// Args: deck name, list of card entries.
    /// DeckHubManager can listen to this to persist to Firestore.
    /// </summary>
    public event System.Action<string, List<DeckCardEntry>> DeckSaved;

    private void Awake()
    {
        _maxDeckSize = deckSizeSource != null ? deckSizeSource.deckSize : defaultDeckSize;

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);
    }

    private void Start()
    {
        BuildCollectionList();
        StartNewDeck("New Deck");
    }

    private void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);
    }

    private void BuildCollectionList()
    {
        if (cardDatabase == null || collectionContentRoot == null || cardItemPrefab == null)
        {
            Debug.LogError("DeckBuilderManager: Missing references for collection list.");
            return;
        }

        // Clear any existing children and cached items.
        for (int i = collectionContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = collectionContentRoot.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
        _items.Clear();

        if (cardDatabase.allCards == null || cardDatabase.allCards.Count == 0)
        {
            Debug.LogWarning("DeckBuilderManager: CardDatabase has no cards.");
            return;
        }

        foreach (var card in cardDatabase.allCards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardId))
                continue;

            var go = Instantiate(cardItemPrefab, collectionContentRoot);
            var item = go.GetComponent<DeckBuilderCardItem>();
            if (item == null)
            {
                Debug.LogError(
                    "DeckBuilderManager: cardItemPrefab does not have a DeckBuilderCardItem component."
                );
                Destroy(go);
                continue;
            }

            // Initialize starting at 0 count.
            item.Initialize(card, maxCopiesPerCard, initialCount: 0);
            item.CountChanged += OnCardItemCountChanged;
            _items.Add(item);

            // Ensure the dictionary has an entry (so we don't need null checks later).
            if (!_cardCounts.ContainsKey(card.cardId))
                _cardCounts[card.cardId] = 0;
        }
    }

    private void OnCardItemCountChanged(DeckBuilderCardItem item, int newLocalCount)
    {
        if (item == null || item.Card == null)
            return;

        string id = item.Card.cardId;
        int oldCount = _cardCounts.TryGetValue(id, out int c) ? c : 0;

        // Update with the new count.
        _cardCounts[id] = newLocalCount;

        // Enforce overall deck size limit.
        int total = GetTotalCardCount();
        if (total > _maxDeckSize)
        {
            int overflow = total - _maxDeckSize;
            int corrected = Mathf.Max(0, newLocalCount - overflow);
            _cardCounts[id] = corrected;
            item.SetCount(corrected, raiseEvent: false);
        }

        UpdateDeckSummaryUI();
    }

    private int GetTotalCardCount()
    {
        int sum = 0;
        foreach (var kvp in _cardCounts)
            sum += kvp.Value;
        return sum;
    }

    private void UpdateDeckSummaryUI()
    {
        if (deckListContentRoot == null || deckListItemPrefab == null)
            return;

        // Clear existing entries
        for (int i = deckListContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = deckListContentRoot.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }

        if (cardDatabase == null || cardDatabase.allCards == null)
            return;

        // Show only cards with count > 0, grouped by cardId.
        var ordered = _cardCounts
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp =>
            {
                var def = cardDatabase.GetById(kvp.Key);
                return def != null ? def.DisplayName : kvp.Key;
            });

        foreach (var kvp in ordered)
        {
            var def = cardDatabase.GetById(kvp.Key);
            string name = def != null ? def.DisplayName : kvp.Key;
            int count = kvp.Value;

            var label = Instantiate(deckListItemPrefab, deckListContentRoot);
            label.text = $"{count}x {name}";
        }
    }

    private void OnSaveClicked()
    {
        string deckName =
            deckNameInput != null && !string.IsNullOrEmpty(deckNameInput.text)
                ? deckNameInput.text
                : "New Deck";

        int total = GetTotalCardCount();
        if (total == 0)
        {
            Debug.LogWarning("DeckBuilderManager: Cannot save an empty deck.");
            return;
        }

        if (total != _maxDeckSize)
        {
            Debug.LogWarning(
                $"DeckBuilderManager: Deck has {total} cards, expected {_maxDeckSize}. Adjust counts before saving."
            );
            return;
        }

        // Build a simple in-memory representation for now.
        var entries = _cardCounts
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => new DeckCardEntry { cardId = kvp.Key, count = kvp.Value })
            .ToList();

        Debug.Log($"DeckBuilderManager: Save clicked. Name='{deckName}', TotalCards={total}.");

        // Notify listeners (e.g., DeckHubManager) that a valid deck has been saved.
        DeckSaved?.Invoke(deckName, entries);
    }

    // Simple serialisable deck entry structure for future Firestore integration.
    [System.Serializable]
    public struct DeckCardEntry
    {
        public string cardId;
        public int count;
    }

    /// <summary>
    /// Resets the builder to a fresh deck with the given default name,
    /// clearing all counts and deck summary UI.
    /// </summary>
    public void StartNewDeck(string defaultName)
    {
        // Ensure dictionary entries exist for all known cards.
        foreach (var card in cardDatabase.allCards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardId))
                continue;

            if (!_cardCounts.ContainsKey(card.cardId))
                _cardCounts[card.cardId] = 0;
            else
                _cardCounts[card.cardId] = 0;
        }

        // Reset all visible item counts.
        foreach (var item in _items)
        {
            item?.SetCount(0, raiseEvent: false);
        }

        if (deckNameInput != null)
            deckNameInput.text = string.IsNullOrEmpty(defaultName) ? "New Deck" : defaultName;

        UpdateDeckSummaryUI();
    }

    /// <summary>
    /// Populates the builder from an existing deck definition.
    /// </summary>
    public void LoadFromExistingDeck(string deckName, IEnumerable<DeckCardEntry> entries)
    {
        StartNewDeck(deckName);

        if (entries == null)
        {
            UpdateDeckSummaryUI();
            return;
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.cardId))
                continue;

            _cardCounts[entry.cardId] = Mathf.Clamp(entry.count, 0, maxCopiesPerCard);
        }

        // Push counts into the visible items.
        foreach (var item in _items)
        {
            if (item == null || item.Card == null)
                continue;

            if (_cardCounts.TryGetValue(item.Card.cardId, out int c))
                item.SetCount(c, raiseEvent: false);
            else
                item.SetCount(0, raiseEvent: false);
        }

        UpdateDeckSummaryUI();
    }
}
