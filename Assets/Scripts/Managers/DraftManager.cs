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
    [System.Serializable]
    private class DraftCardEntry
    {
        public ScriptableObject data;
        public bool isCreature;
        public int momentumCost;
        public int costTier; // 0 = low, 1 = mid, 2 = high
    }

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
    private readonly List<DraftCardEntry> draftPool = new();

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

        foreach (var so in source)
        {
            if (so == null)
                continue;

            var entry = new DraftCardEntry { data = so };

            if (so is CreatureCard creature)
            {
                entry.isCreature = true;
                entry.momentumCost = Mathf.Max(0, creature.momentumCost);
            }
            else if (so is EffectCard effect)
            {
                entry.isCreature = false;
                entry.momentumCost = Mathf.Max(0, effect.momentumCost);
            }
            else
            {
                // Unsupported type for drafting; skip.
                continue;
            }

            entry.costTier = config.GetCostTier(entry.momentumCost);
            draftPool.Add(entry);
        }

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

        // Hide the draft UI and build a random deck for the player.
        ShowDraftUI(false);
        deckManager.InitializeRandomDeck();

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
        bool preferCreature = ChoosePreferredIsCreature();
        int desiredTier = ChoosePreferredCostTier();

        // Build candidate list with progressively relaxed constraints.
        var candidates = BuildCandidates(preferCreature, desiredTier);

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

    private bool ChoosePreferredIsCreature()
    {
        if (config == null)
            return true;

        if (picksDone == 0)
            return true; // first pick creature to anchor a board.

        float currentCreatureRatio =
            picksDone > 0 ? (float)creatureCount / picksDone : config.targetCreatureRatio;

        // Blend current vs target using a simple bias factor.
        float bias = Mathf.Max(0f, config.typeBiasStrength);
        // When bias is 0, we mostly keep current ratio; when high, we push toward target.
        float blendedTarget =
            (1f / (1f + bias)) * currentCreatureRatio
            + (bias / (1f + bias)) * config.targetCreatureRatio;

        // If we're below blended target, prefer creatures; otherwise prefer effects.
        return currentCreatureRatio <= blendedTarget;
    }

    private int ChoosePreferredCostTier()
    {
        if (config == null)
            return 0;

        // Compute deficits relative to targets.
        int lowDeficit = config.targetLowCount - lowCount;
        int midDeficit = config.targetMidCount - midCount;
        int highDeficit = config.targetHighCount - highCount;

        float bias = Mathf.Max(0f, config.costBiasStrength);

        // If bias is 0, treat all tiers equally weighted; otherwise magnify the deficit difference.
        float lowScore = Mathf.Pow(Mathf.Max(0, lowDeficit), 1f + bias);
        float midScore = Mathf.Pow(Mathf.Max(0, midDeficit), 1f + bias);
        float highScore = Mathf.Pow(Mathf.Max(0, highDeficit), 1f + bias);

        if (lowScore >= midScore && lowScore >= highScore)
            return 0;
        if (midScore >= lowScore && midScore >= highScore)
            return 1;
        return 2;
    }

    private List<DraftCardEntry> BuildCandidates(bool preferCreature, int desiredTier)
    {
        var result = new List<DraftCardEntry>();
        if (draftPool.Count == 0 || config == null)
            return result;

        // Helper local predicate respecting copy cap.
        bool UnderCopyCap(DraftCardEntry e) =>
            GetCopies(e.data) < Mathf.Max(1, config.maxCopiesPerCard);

        // 1) Strict: desired type + desired tier
        result = draftPool
            .Where(e =>
                e.isCreature == preferCreature && e.costTier == desiredTier && UnderCopyCap(e)
            )
            .ToList();
        if (result.Count >= 3)
            return result;

        // 2) Relax tier: any tier of desired type
        var typeOnly = draftPool
            .Where(e => e.isCreature == preferCreature && UnderCopyCap(e))
            .ToList();
        MergeUnique(result, typeOnly);
        if (result.Count >= 3)
            return result;

        // 3) Allow opposite type but keep desired tier
        var tierOnly = draftPool.Where(e => e.costTier == desiredTier && UnderCopyCap(e)).ToList();
        MergeUnique(result, tierOnly);
        if (result.Count >= 3)
            return result;

        // 4) Fully relaxed: any under copy cap
        var any = draftPool.Where(UnderCopyCap).ToList();
        MergeUnique(result, any);

        return result;
    }

    private void MergeUnique(List<DraftCardEntry> target, List<DraftCardEntry> source)
    {
        foreach (var e in source)
        {
            if (!target.Contains(e))
                target.Add(e);
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

        if (picked is CreatureCard creature)
        {
            creatureCount++;
            int tier = config.GetCostTier(creature.momentumCost);
            IncrementTierCount(tier);
        }
        else if (picked is EffectCard effect)
        {
            effectCount++;
            int tier = config.GetCostTier(effect.momentumCost);
            IncrementTierCount(tier);
        }
    }

    private void IncrementTierCount(int tier)
    {
        switch (tier)
        {
            case 0:
                lowCount++;
                break;
            case 1:
                midCount++;
                break;
            default:
                highCount++;
                break;
        }
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
