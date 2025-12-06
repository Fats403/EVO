using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls the pre-game draft flow for the local player.
/// Shows 3-card offers, applies momentum/type biasing based on DraftConfig,
/// and hands the resulting deck to DeckManager before the normal game starts.
/// </summary>
public class DraftManager : MonoBehaviour
{
    [Header("Config & References")]
    public DraftConfig config;
    public DeckManager deckManager;

    [Header("UI")]
    public CanvasGroup draftCanvasGroup;
    public DraftCardOptionUI[] optionSlots;
    public UnityEngine.UI.Button confirmButton;
    public TextMeshProUGUI picksRemainingLabel;

    [Header("Card Pool")]
    [Tooltip("If empty, falls back to deckManager.allCards as the draftable pool.")]
    public List<ScriptableObject> allDraftableCards = new();

    private readonly List<ScriptableObject> draftedDeck = new();
    private readonly Dictionary<ScriptableObject, int> copiesPerCard = new();
    private readonly List<DraftRules.CardEntry> draftPool = new();

    private int creatureCount;
    private int effectCount;
    private int lowCount;
    private int midCount;
    private int highCount;
    private int picksDone;

    private DraftCardOptionUI selectedOption;

    public bool IsDrafting => config != null && picksDone < config.deckSize;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }
    }

    /// <summary>
    /// Entry point: called by GameManager at the beginning of the scene.
    /// </summary>
    public void BeginDraft()
    {
        if (config == null)
        {
            Debug.LogError("DraftManager.BeginDraft: DraftConfig not assigned.");
            return;
        }

        BuildDraftPool();
        ResetDraftState();
        ShowDraftUI(true);
        ShowNextPick();
    }

    private void BuildDraftPool()
    {
        draftPool.Clear();

        IEnumerable<ScriptableObject> source =
            (allDraftableCards != null && allDraftableCards.Count > 0) ? allDraftableCards
            : deckManager != null ? deckManager.allCards
            : Enumerable.Empty<ScriptableObject>();

        var built = DraftRules.BuildEntryPool(source, config);
        draftPool.AddRange(built);

        if (draftPool.Count == 0)
        {
            Debug.LogError(
                "DraftManager: Draft pool is empty – check allDraftableCards / DeckManager."
            );
        }
    }

    private void ResetDraftState()
    {
        draftedDeck.Clear();
        copiesPerCard.Clear();
        creatureCount = 0;
        effectCount = 0;
        lowCount = 0;
        midCount = 0;
        highCount = 0;
        picksDone = 0;
        selectedOption = null;

        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }

        if (optionSlots != null)
        {
            foreach (var slot in optionSlots)
            {
                if (slot != null)
                    slot.SetSelected(false);
            }
        }
    }

    private void ShowDraftUI(bool visible)
    {
        if (draftCanvasGroup == null)
            return;

        draftCanvasGroup.alpha = visible ? 1f : 0f;
        draftCanvasGroup.interactable = visible;
        draftCanvasGroup.blocksRaycasts = visible;
    }

    /// <summary>
    /// UI hook: skip the draft and play with a random deck instead.
    /// </summary>
    public void OnRandomDeckClicked()
    {
        if (deckManager == null)
        {
            Debug.LogError("DraftManager.OnRandomDeckClicked: DeckManager not assigned.");
            return;
        }
        if (config == null)
        {
            Debug.LogError("DraftManager.OnRandomDeckClicked: DraftConfig not assigned.");
            return;
        }

        // Hide the draft UI and build a balanced random deck for the player
        // using the same rules as the normal draft.
        ShowDraftUI(false);
        var src = deckManager.allCards ?? new System.Collections.Generic.List<ScriptableObject>();
        var built = BalancedDeckBuilder.BuildDeck(src, config);
        deckManager.InitializeFromDraft(built);

        // Hand off to normal game startup, which will draw the starting hand
        // and begin the usual setup / round flow.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDraftCompleted();
        }
        else
        {
            Debug.LogWarning("DraftManager.OnRandomDeckClicked: No GameManager instance found.");
        }
    }

    private void ShowNextPick()
    {
        if (!IsDrafting)
        {
            FinalizeDraft();
            return;
        }

        selectedOption = null;
        if (confirmButton != null)
            confirmButton.interactable = false;

        if (picksRemainingLabel != null)
        {
            picksRemainingLabel.text = $"Pick {picksDone + 1} / {config.deckSize}";
        }

        // Decide desired type and cost tier for this pick, using bias strengths.
        bool preferCreature = DraftRules.ChoosePreferredIsCreature(
            config,
            picksDone,
            creatureCount
        );
        int desiredTier = DraftRules.ChoosePreferredCostTier(config, lowCount, midCount, highCount);

        // Build candidate list with progressively relaxed constraints.
        var candidates = DraftRules.BuildCandidates(
            draftPool,
            config,
            preferCreature,
            desiredTier,
            copiesPerCard
        );

        if (candidates.Count == 0)
        {
            Debug.LogWarning("DraftManager: No candidates available; falling back to full pool.");
            candidates.AddRange(draftPool.Where(e => GetCopies(e.data) < config.maxCopiesPerCard));
        }

        // Shuffle candidates lightly to avoid always picking the same few.
        ShuffleList(candidates);

        int offerCount = Mathf.Min(3, candidates.Count);
        if (optionSlots == null || optionSlots.Length == 0)
        {
            Debug.LogError("DraftManager: No optionSlots configured.");
            return;
        }

        for (int i = 0; i < optionSlots.Length; i++)
        {
            var slot = optionSlots[i];
            if (slot == null)
                continue;

            if (i < offerCount)
            {
                var entry = candidates[i];
                slot.SetCard(entry.data, OnOptionClicked);
                slot.SetSelected(false);
            }
            else
            {
                // Clear extra slots
                slot.SetCard(null, null);
                slot.SetSelected(false);
            }
        }
    }

    private int GetCopies(ScriptableObject card)
    {
        if (card == null)
            return 0;
        return copiesPerCard.TryGetValue(card, out int count) ? count : 0;
    }

    private void IncrementCounters(ScriptableObject picked)
    {
        if (picked == null || config == null)
            return;

        DraftRules.IncrementCountersForPicked(
            config,
            picked,
            ref creatureCount,
            ref effectCount,
            ref lowCount,
            ref midCount,
            ref highCount
        );
    }

    private void OnOptionClicked(DraftCardOptionUI option)
    {
        if (option == null)
            return;

        selectedOption = option;

        if (optionSlots != null)
        {
            foreach (var slot in optionSlots)
            {
                if (slot != null)
                    slot.SetSelected(slot == option);
            }
        }

        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    private void OnConfirmClicked()
    {
        if (selectedOption == null)
            return;

        var picked = selectedOption.GetCardData();
        if (picked == null)
            return;

        draftedDeck.Add(picked);
        IncrementCounters(picked);

        if (!copiesPerCard.ContainsKey(picked))
            copiesPerCard[picked] = 0;
        copiesPerCard[picked]++;

        picksDone++;

        if (!IsDrafting)
        {
            FinalizeDraft();
        }
        else
        {
            ShowNextPick();
        }
    }

    private void FinalizeDraft()
    {
        ShowDraftUI(false);

        if (deckManager == null)
        {
            Debug.LogError("DraftManager.FinalizeDraft: DeckManager not assigned.");
            return;
        }

        deckManager.InitializeFromDraft(draftedDeck);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDraftCompleted();
        }
        else
        {
            Debug.LogWarning("DraftManager.FinalizeDraft: No GameManager instance found.");
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j =
                (GameManager.Instance != null)
                    ? GameManager.Instance.NextRandomInt(0, i + 1)
                    : Random.Range(0, i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }
}
