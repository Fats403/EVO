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

    [Header("Filters & Status")]
    [Tooltip("Optional search box to filter cards by name or trait text.")]
    [SerializeField]
    private TMP_InputField searchInput;

    [Tooltip("Label that shows how many cards are currently in the deck (e.g., '0 / 20').")]
    [SerializeField]
    private TextMeshProUGUI draftedCountLabel;

    [Tooltip("Dropdown used to choose sort order for the card collection list.")]
    [SerializeField]
    private TMP_Dropdown sortDropdown;

    private int _maxDeckSize;

    // cardId -> count
    private readonly Dictionary<string, int> _cardCounts = new();

    // All instantiated card items in the collection list.
    private readonly List<DeckBuilderCardItem> _items = new();

    // Current lowercase search query for filtering the collection list.
    private string _currentSearch = string.Empty;

    private enum SortMode
    {
        TypeThenName = 0,
        NameAsc = 1,
        MomentumAsc = 2,
        MomentumDesc = 3,
    }

    // Default sort: group by type, then name.
    private SortMode _currentSortMode = SortMode.TypeThenName;

    /// <summary>
    /// Raised after a successful Save click (deck is valid).
    /// Args: deck name, list of card entries.
    /// DeckHubManager can listen to this to persist to Firestore.
    /// Uses the shared DeckCardEntry type so that decks can be passed
    /// between scenes and over the network.
    /// </summary>
    public event System.Action<string, List<DeckCardEntry>> DeckSaved;

    private void Awake()
    {
        _maxDeckSize = deckSizeSource != null ? deckSizeSource.deckSize : defaultDeckSize;

        // Enforce a hard character limit on the deck name input so users can't type
        // excessively long names. The save logic will still validate min/max length.
        if (deckNameInput != null)
            deckNameInput.characterLimit = 16;

        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(OnSearchValueChanged);
            // Set a friendlier placeholder hint than the TMP default.
            var placeholder = searchInput.placeholder as TMP_Text;
            if (placeholder != null)
                placeholder.text = "Search...";
        }

        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.AddListener(OnSortDropdownChanged);
            // Ensure dropdown starts on the default sort mode and apply it.
            sortDropdown.value = (int)_currentSortMode;
            sortDropdown.RefreshShownValue();
        }

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
        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(OnSearchValueChanged);

        if (sortDropdown != null)
            sortDropdown.onValueChanged.RemoveListener(OnSortDropdownChanged);

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

        // Apply default sort and any active search filter now that items are built.
        ApplySort();
        ApplySearchFilter();

        // Update drafted count label for an empty deck.
        UpdateDraftedCountLabel();
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
        UpdateDraftedCountLabel();
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

        // Also keep the drafted count label in sync.
        UpdateDraftedCountLabel();
    }

    private void OnSaveClicked()
    {
        string deckName =
            deckNameInput != null ? (deckNameInput.text ?? string.Empty).Trim() : string.Empty;

        // Validate name length: between 1 and 16 characters (after trimming).
        if (deckName.Length < 1)
        {
            Debug.LogWarning("DeckBuilderManager: Deck name must be at least 1 character long.");
            return;
        }
        if (deckName.Length > 16)
        {
            Debug.LogWarning(
                $"DeckBuilderManager: Deck name '{deckName}' is too long ({deckName.Length}/16)."
            );
            return;
        }

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

    private void OnSearchValueChanged(string value)
    {
        _currentSearch = string.IsNullOrWhiteSpace(value) ? string.Empty : value.ToLowerInvariant();
        ApplySearchFilter();
    }

    private void OnSortDropdownChanged(int optionIndex)
    {
        Debug.Log($"[DeckBuilder] Sort changed to index {optionIndex}");
        // Clamp to valid range; assumes dropdown options are ordered to match SortMode.
        if (optionIndex < 0)
            optionIndex = 0;
        if (optionIndex > (int)SortMode.MomentumDesc)
            optionIndex = (int)SortMode.MomentumDesc;

        _currentSortMode = (SortMode)optionIndex;
        ApplySort();
        // Re-apply filter so the visible subset respects the new order.
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        if (_items == null || _items.Count == 0)
            return;

        foreach (var item in _items)
        {
            if (item == null || item.Card == null)
                continue;

            bool visible = MatchesSearch(item.Card, _currentSearch);
            if (item.gameObject.activeSelf != visible)
                item.gameObject.SetActive(visible);
        }
    }

    private bool MatchesSearch(CardDefinition card, string queryLower)
    {
        if (string.IsNullOrEmpty(queryLower) || card == null)
            return true;

        // Card name
        string name = card.DisplayName ?? card.name;
        if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(queryLower))
            return true;

        // Creature traits (name + description)
        if (card is CreatureCard creature && creature.baseTraits != null)
        {
            foreach (var trait in creature.baseTraits)
            {
                if (trait == null)
                    continue;

                string tName = string.IsNullOrEmpty(trait.traitName) ? trait.name : trait.traitName;
                if (!string.IsNullOrEmpty(tName) && tName.ToLowerInvariant().Contains(queryLower))
                    return true;

                if (
                    !string.IsNullOrEmpty(trait.description)
                    && trait.description.ToLowerInvariant().Contains(queryLower)
                )
                    return true;
            }
        }

        // Effect card description text
        if (card is EffectCard effect)
        {
            if (
                !string.IsNullOrEmpty(effect.description)
                && effect.description.ToLowerInvariant().Contains(queryLower)
            )
                return true;
        }

        return false;
    }

    private void UpdateDraftedCountLabel()
    {
        if (draftedCountLabel == null)
            return;

        int total = GetTotalCardCount();
        draftedCountLabel.text = $"{total} / {_maxDeckSize}";
    }

    private void ApplySort()
    {
        if (_items == null || _items.Count == 0 || collectionContentRoot == null)
            return;

        // Create a sorted snapshot of items with valid cards.
        var sortable = _items.Where(i => i != null && i.Card != null).ToList();

        switch (_currentSortMode)
        {
            case SortMode.MomentumAsc:
                sortable = sortable
                    .OrderBy(i => i.Card.MomentumCost)
                    .ThenBy(i => GetCardTypeSortIndex(i.Card))
                    .ThenBy(i => (i.Card.DisplayName ?? i.Card.name))
                    .ToList();
                break;
            case SortMode.MomentumDesc:
                sortable = sortable
                    .OrderByDescending(i => i.Card.MomentumCost)
                    .ThenBy(i => GetCardTypeSortIndex(i.Card))
                    .ThenBy(i => (i.Card.DisplayName ?? i.Card.name))
                    .ToList();
                break;
            case SortMode.TypeThenName:
                sortable = sortable
                    .OrderBy(i => GetCardTypeSortIndex(i.Card))
                    .ThenBy(i => (i.Card.DisplayName ?? i.Card.name))
                    .ToList();
                break;
            case SortMode.NameAsc:
            default:
                sortable = sortable.OrderBy(i => (i.Card.DisplayName ?? i.Card.name)).ToList();
                break;
        }

        // Apply sibling indices to reflect the new order.
        for (int i = 0; i < sortable.Count; i++)
        {
            var item = sortable[i];
            if (item != null)
                item.transform.SetSiblingIndex(i);
        }

        // Keep _items in a consistent order (sorted items first, then any null/invalid).
        var remaining = _items.Except(sortable).ToList();
        _items.Clear();
        _items.AddRange(sortable);
        _items.AddRange(remaining);
    }

    private int GetCardTypeSortIndex(CardDefinition card)
    {
        if (card is CreatureCard creature)
        {
            // Group creatures by type, then effects after.
            switch (creature.type)
            {
                case CardType.Herbivore:
                    return 0;
                case CardType.Carnivore:
                    return 1;
                case CardType.Avian:
                    return 2;
                default:
                    return 3;
            }
        }

        if (card is EffectCard)
            return 4; // effects come after creatures

        return 5;
    }

    // NOTE: DeckCardEntry has been extracted into a shared DeckDefinition.cs file
    // so that it can be reused by other systems (SelectedDeckStore, networking, etc.).

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
        UpdateDraftedCountLabel();
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
        UpdateDraftedCountLabel();
    }
}
