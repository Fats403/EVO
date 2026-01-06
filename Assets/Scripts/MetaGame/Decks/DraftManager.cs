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

    [Header("UI")]
    public CanvasGroup draftCanvasGroup;
    public DraftCardOptionUI[] optionSlots;
    public UnityEngine.UI.Button confirmButton;
    public TextMeshProUGUI picksRemainingLabel;

    [Header("Completion Buttons")]
    [Tooltip("Shown only after the draft is complete to start a local Quickplay game.")]
    public UnityEngine.UI.Button quickplayFromDraftButton;

    [Tooltip(
        "Shown only after the draft is complete to start an online game from the drafted deck."
    )]
    public UnityEngine.UI.Button onlineFromDraftButton;

    [Tooltip(
        "Join Lobby button in the draft view. Visible after draft completes; "
            + "starts disabled and is enabled once an invite/lobby is available."
    )]
    public UnityEngine.UI.Button joinLobbyFromDraftButton;

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

    /// <summary>
    /// Raised when this DraftManager has finished building a complete deck
    /// (either via the normal draft flow or the Random Deck button). List
    /// contains the concrete ScriptableObject card definitions that make up
    /// the deck. Callers (e.g., DeckHubManager) are responsible for turning
    /// this into DeckCardEntry data and starting a game.
    /// </summary>
    public event System.Action<List<ScriptableObject>> DeckBuilt;

    public bool IsDrafting => config != null && picksDone < GameRules.DeckSize;

    /// <summary>
    /// Called by DeckHubManager to enable/disable the Join-from-draft button
    /// based on lobby/invite state. The button is only visible after the
    /// draft has completed; this method controls its interactable state.
    /// </summary>
    public void SetJoinFromDraftButtonEnabled(bool enabled)
    {
        if (joinLobbyFromDraftButton == null)
            return;
        joinLobbyFromDraftButton.interactable =
            enabled && joinLobbyFromDraftButton.gameObject.activeSelf;
    }

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        // Hide completion buttons at startup; they are only relevant once a
        // full draft has been completed.
        if (quickplayFromDraftButton != null)
            quickplayFromDraftButton.gameObject.SetActive(false);
        if (onlineFromDraftButton != null)
            onlineFromDraftButton.gameObject.SetActive(false);
        if (joinLobbyFromDraftButton != null)
            joinLobbyFromDraftButton.gameObject.SetActive(false);
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

        // Clear any lingering HUD card previews from a previous game state so
        // the draft starts from a clean UI.
        CardPreviewManager.Instance?.HideAll();

        BuildDraftPool();
        ResetDraftState();
        ShowDraftUI(true);
        ShowNextPick();
    }

    private void BuildDraftPool()
    {
        draftPool.Clear();

        IEnumerable<ScriptableObject> source =
            (allDraftableCards != null && allDraftableCards.Count > 0)
                ? allDraftableCards
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
            // Ensure the confirm/create button is visible again when starting a new draft,
            // even if a previous draft hid it in FinalizeDraft().
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = false;
        }

        // While drafting, the completion/join buttons should be hidden/disabled.
        if (quickplayFromDraftButton != null)
            quickplayFromDraftButton.gameObject.SetActive(false);
        if (onlineFromDraftButton != null)
            onlineFromDraftButton.gameObject.SetActive(false);
        if (joinLobbyFromDraftButton != null)
            joinLobbyFromDraftButton.gameObject.SetActive(false);

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
        if (config == null)
        {
            Debug.LogError("DraftManager.OnRandomDeckClicked: DraftConfig not assigned.");
            return;
        }

        // Hide the draft UI and build a balanced random deck for the player
        // using the same rules as the normal draft.
        ShowDraftUI(false);
        CardPreviewManager.Instance?.HideAll();

        if (allDraftableCards == null || allDraftableCards.Count == 0)
        {
            Debug.LogError(
                "DraftManager.OnRandomDeckClicked: No draftable card pool available (allDraftableCards is empty or null)."
            );
            return;
        }

        var built = BalancedDeckBuilder.BuildDeck(allDraftableCards, config);

        // Hand the deck off to whoever is listening (e.g., DeckHubManager),
        // which will convert it into DeckCardEntry data and start a game
        // using the unified deck pipeline.
        if (DeckBuilt != null)
        {
            DeckBuilt.Invoke(built);
        }
        else
        {
            Debug.LogWarning(
                "DraftManager.OnRandomDeckClicked: No DeckBuilt listener; deck was generated but not used."
            );
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
            picksRemainingLabel.text = $"Pick {picksDone + 1} / {GameRules.DeckSize}";
        }

        // Build three independent offer slots. Each slot rolls creature/effect and cost tier
        // separately, using the current deck composition + bias strengths. This avoids drafts
        // that feel like "all three cards are creatures" too often.
        var chosenThisOffer = new HashSet<ScriptableObject>();
        var offers = new List<ScriptableObject>(3);

        int offerSlots = Mathf.Min(3, optionSlots != null ? optionSlots.Length : 3);
        for (int i = 0; i < offerSlots; i++)
        {
            bool wantCreature = DraftRules.RollIsCreature(config, picksDone, creatureCount);
            int wantTier = DraftRules.RollDesiredCostTier(config, lowCount, midCount, highCount);

            // Build candidate list with progressively relaxed constraints.
            var candidates = DraftRules.BuildCandidates(
                draftPool,
                config,
                wantCreature,
                wantTier,
                copiesPerCard
            );

            // Do not offer duplicates within the same 3-card pack.
            candidates.RemoveAll(e =>
                e == null || e.data == null || chosenThisOffer.Contains(e.data)
            );

            if (candidates.Count == 0)
            {
                // Relax fully: anything under copy cap and not already in this offer.
                candidates = draftPool
                    .Where(e => e != null && e.data != null)
                    .Where(e => GetCopies(e.data) < config.maxCopiesPerCard)
                    .Where(e => !chosenThisOffer.Contains(e.data))
                    .ToList();
            }

            if (candidates.Count == 0)
                break;

            ShuffleList(candidates);
            var pickedEntry = candidates[0];
            if (pickedEntry != null && pickedEntry.data != null)
            {
                offers.Add(pickedEntry.data);
                chosenThisOffer.Add(pickedEntry.data);
            }
        }

        int offerCount = offers.Count;
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
                var data = offers[i];
                slot.SetCard(data, OnOptionClicked);
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
        // Keep the draft UI visible so the player can see their drafted deck
        // and choose what to do next (Quickplay / Online).
        ShowDraftUI(true);

        // Once the draft is complete, the per-pick confirm button is no longer
        // relevant; hide it so only the completion buttons remain.
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

        // When we leave the draft and transition into normal play, make sure
        // the in-game preview manager is reset as well.
        CardPreviewManager.Instance?.HideAll();

        // Show completion buttons now that the deck is finalized.
        if (quickplayFromDraftButton != null)
            quickplayFromDraftButton.gameObject.SetActive(true);
        if (onlineFromDraftButton != null)
            onlineFromDraftButton.gameObject.SetActive(true);

        // Show the join-from-draft button, but leave it disabled until the
        // DeckHubManager detects a pending invite / lobby and enables it.
        if (joinLobbyFromDraftButton != null)
        {
            joinLobbyFromDraftButton.gameObject.SetActive(true);
            joinLobbyFromDraftButton.interactable = false;
        }

        // Notify listeners (e.g., DeckHubManager) that a full drafted deck is
        // ready. The listener is responsible for converting this into deck
        // data and starting a game. If nobody is listening, just log a warning.
        if (DeckBuilt != null)
        {
            DeckBuilt.Invoke(new List<ScriptableObject>(draftedDeck));
        }
        else
        {
            Debug.LogWarning(
                "DraftManager.FinalizeDraft: No DeckBuilt listener; drafted deck was generated but not used."
            );
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = DeterministicRng.NextInt(0, i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }
}
