using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Deck-builder-specific wrapper around a card preview (CreatureCardUI / EffectCardUI),
/// with + / - buttons and a count label.
/// </summary>
public class DeckBuilderCardItem : MonoBehaviour
{
    [Header("Preview")]
    [Tooltip(
        "Parent transform where the card preview (CreatureCardUI / EffectCardUI) will be instantiated."
    )]
    public Transform previewRoot;

    [Tooltip("Prefab for creature card previews.")]
    public GameObject creatureCardPrefab;

    [Tooltip("Prefab for effect card previews.")]
    public GameObject effectCardPrefab;

    [Header("Count UI")]
    [SerializeField]
    private Button minusButton;

    [SerializeField]
    private Button plusButton;

    [SerializeField]
    private TextMeshProUGUI countText;

    /// <summary>Card definition this item represents.</summary>
    public CardDefinition Card { get; private set; }

    /// <summary>Current count for this card in the deck.</summary>
    public int CurrentCount { get; private set; }

    /// <summary>Maximum allowed copies of this card in a deck.</summary>
    public int MaxCopies { get; private set; }

    /// <summary>
    /// Raised whenever the local count changes. The DeckBuilderManager is responsible
    /// for enforcing global deck size limits and may call SetCount to clamp.
    /// </summary>
    public event Action<DeckBuilderCardItem, int> CountChanged;

    private void Awake()
    {
        if (minusButton != null)
            minusButton.onClick.AddListener(OnMinusClicked);
        if (plusButton != null)
            plusButton.onClick.AddListener(OnPlusClicked);
    }

    private void OnDestroy()
    {
        if (minusButton != null)
            minusButton.onClick.RemoveListener(OnMinusClicked);
        if (plusButton != null)
            plusButton.onClick.RemoveListener(OnPlusClicked);
    }

    public void Initialize(CardDefinition card, int maxCopies, int initialCount = 0)
    {
        Card = card;
        MaxCopies = Mathf.Max(0, maxCopies);
        CurrentCount = Mathf.Clamp(initialCount, 0, MaxCopies);

        BuildPreview(card);
        UpdateCountLabel();
    }

    /// <summary>
    /// Called by DeckBuilderManager to override the count (e.g., when clamping by deck size).
    /// </summary>
    public void SetCount(int newCount, bool raiseEvent)
    {
        newCount = Mathf.Clamp(newCount, 0, MaxCopies);
        if (newCount == CurrentCount)
            return;

        CurrentCount = newCount;
        UpdateCountLabel();

        if (raiseEvent)
            CountChanged?.Invoke(this, CurrentCount);
    }

    private void OnMinusClicked()
    {
        if (CurrentCount <= 0)
            return;

        SetCount(CurrentCount - 1, raiseEvent: true);
    }

    private void OnPlusClicked()
    {
        if (CurrentCount >= MaxCopies)
            return;

        SetCount(CurrentCount + 1, raiseEvent: true);
    }

    private void UpdateCountLabel()
    {
        if (countText != null)
        {
            countText.text = $"{CurrentCount} / {MaxCopies}";
        }
    }

    private void BuildPreview(CardDefinition card)
    {
        if (previewRoot == null || card == null)
            return;

        // Clear existing preview
        for (int i = previewRoot.childCount - 1; i >= 0; i--)
        {
            var child = previewRoot.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }

        GameObject previewObj = null;

        if (card is CreatureCard creatureData)
        {
            if (creatureCardPrefab == null)
            {
                Debug.LogError("DeckBuilderCardItem: Creature card prefab not assigned.");
                return;
            }
            previewObj = Instantiate(creatureCardPrefab, previewRoot);
            var ui = previewObj.GetComponent<CreatureCardUI>();
            ui?.Initialize(creatureData);
        }
        else if (card is EffectCard effectData)
        {
            if (effectCardPrefab == null)
            {
                Debug.LogError("DeckBuilderCardItem: Effect card prefab not assigned.");
                return;
            }
            previewObj = Instantiate(effectCardPrefab, previewRoot);
            var ui = previewObj.GetComponent<EffectCardUI>();
            if (ui != null)
            {
                ui.Initialize(effectData);
                ui.owner = SlotOwner.Player1;
            }
        }
        else
        {
            Debug.LogWarning($"DeckBuilderCardItem: Unsupported card type {card.GetType().Name}.");
        }

        // Disable BaseCardUI + child Graphics raycasts so this preview doesn't handle drag/hover or steal clicks.
        if (previewObj != null)
        {
            var baseCard = previewObj.GetComponent<BaseCardUI>();
            if (baseCard != null)
                baseCard.enabled = false;

            var graphics = previewObj.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var g in graphics)
            {
                if (g != null)
                    g.raycastTarget = false;
            }
        }
    }
}
