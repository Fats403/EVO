using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the full manual effect-card target selection workflow:
/// - Starts selection when a manual effect is dropped onto the global zone.
/// - Tracks valid candidates and the player's current selection.
/// - Updates confirm/cancel UI state.
/// - Sends a PlayEffect GameAction back into GameManager on confirm.
///
/// All rules, momentum spending, and effect execution still live in GameManager
/// and EffectsManager; this controller only orchestrates UI + workflow.
/// </summary>
public class ManualEffectSelectionController : MonoBehaviour
{
    public static ManualEffectSelectionController Instance { get; private set; }

    [Header("References")]
    [SerializeField]
    private GameManager gameManager;

    [Header("Selection UI")]
    [Tooltip("Container for confirm/cancel buttons shown while choosing manual effect targets.")]
    public CanvasGroup selectionGroup;
    public Button confirmButton;
    public Button cancelButton;

    private class SelectionState
    {
        public EffectCard card;
        public SlotOwner owner;
        public List<Creature> candidates = new List<Creature>();
        public HashSet<Creature> selected = new HashSet<Creature>();
        public int minCount;
        public int maxCount;
        public bool allowFewerThanMax;
    }

    private SelectionState _state;

    public bool HasActiveSelection => _state != null && _state.card != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }
    }

    private void OnEnable()
    {
        SetSelectionVisible(false);

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    private void OnDisable()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }
    }

    /// <summary>
    /// External entry point when the player drops a manual-selection effect
    /// onto the global effect drop zone.
    /// </summary>
    public bool TryBeginSelection(EffectCard card, SlotOwner owner, out string failureReason)
    {
        failureReason = null;

        if (card == null)
        {
            failureReason = "Invalid effect card.";
            return false;
        }

        if (!card.requiresManualSelection)
        {
            failureReason = "This effect does not use manual target selection.";
            return false;
        }

        if (_state != null)
        {
            failureReason = "You are already choosing targets for another effect.";
            return false;
        }

        if (gameManager == null)
        {
            failureReason = "No GameManager available.";
            return false;
        }

        // Check rules and momentum without spending yet.
        if (!gameManager.CanPlayEffectCardPreview(card, owner, out failureReason))
            return false;

        // Discover all valid, living candidates for this effect.
        IEnumerable<Creature> allCreatures;
        if (gameManager.resolutionManager != null)
        {
            allCreatures = gameManager.resolutionManager.AllCreatures();
        }
        else
        {
            allCreatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);
        }

        var candidates = allCreatures
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .Where(c =>
                EffectsManager.Instance != null
                && EffectsManager.Instance.IsValidTarget(card, c, owner)
            )
            .ToList();

        // Determine manual selection min/max and availability requirements.
        int maxCount = 1;
        int minCount = 1;
        bool allowFewerThanMax = card.allowFewerThanMax;

        switch (card.targetCount)
        {
            case EffectTargetCount.One:
                maxCount = 1;
                minCount = 1;
                allowFewerThanMax = false;
                break;
            case EffectTargetCount.ManySelectUpToN:
                maxCount = Mathf.Max(1, card.maxTargets);
                if (card.minTargets > 0)
                {
                    minCount = Mathf.Clamp(card.minTargets, 1, maxCount);
                }
                else
                {
                    // If no explicit min is set, default to 1 when the effect
                    // allows fewer than max, otherwise require the full max.
                    minCount = allowFewerThanMax ? 1 : maxCount;
                }
                break;
            default:
                maxCount = 1;
                minCount = 1;
                allowFewerThanMax = false;
                break;
        }

        int requiredAvailable = allowFewerThanMax ? minCount : maxCount;

        if (candidates.Count < requiredAvailable)
        {
            failureReason =
                requiredAvailable == 1
                    ? "There are no valid targets for this effect."
                    : $"You need at least {requiredAvailable} valid targets for this effect.";
            return false;
        }

        // Spend momentum and perform final rules check now that we know it is playable.
        if (!gameManager.CanPlayEffectCard(card, owner, out failureReason))
            return false;

        // Lock actions for the local player only
        if (NetworkRoleHelper.IsLocalPlayer(owner))
            gameManager.SetPlayerActionLocked(owner, true);

        _state = new SelectionState
        {
            card = card,
            owner = owner,
            candidates = candidates,
            selected = new HashSet<Creature>(),
            minCount = minCount,
            maxCount = maxCount,
            allowFewerThanMax = allowFewerThanMax,
        };

        if (FeedbackManager.Instance != null)
        {
            string ownerTag = FeedbackManager.TagOwner(owner);
            string msg;
            if (maxCount == 1)
            {
                msg = $"{ownerTag}: Choose a target for {card.effectName}.";
            }
            else if (allowFewerThanMax && minCount < maxCount)
            {
                msg =
                    $"{ownerTag}: Choose {minCount}-{maxCount} targets for {card.effectName} (then Confirm).";
            }
            else
            {
                msg = $"{ownerTag}: Choose {maxCount} targets for {card.effectName}.";
            }
            FeedbackManager.Instance.Log(msg);
        }

        // Show the effect card preview with an instructional caption so the
        // player has clear context while choosing targets.
        if (CardPreviewManager.Instance != null)
        {
            string caption;
            if (maxCount == 1)
            {
                caption = "Select 1 Creature";
            }
            else if (allowFewerThanMax && minCount < maxCount)
            {
                caption = $"Select {minCount}-{maxCount} Creatures";
            }
            else
            {
                caption = $"Select {maxCount} Creatures";
            }
            CardPreviewManager.Instance.ShowEffectSelection(card, owner, caption);
        }

        // Show and initialize confirm/cancel UI.
        UpdateSelectionUIState();

        return true;
    }

    /// <summary>
    /// Called by creatures when clicked; only active while a manual-selection
    /// effect is in progress. Clicks outside the candidate set are ignored.
    /// </summary>
    public void HandleCreatureClicked(Creature c)
    {
        if (c == null || _state == null)
            return;

        var state = _state;

        if (state.card == null)
            return;

        if (!state.candidates.Contains(c))
            return;

        if (c.currentHealth <= 0 || c.isDying)
            return;

        bool nowSelected;
        if (state.selected.Contains(c))
        {
            state.selected.Remove(c);
            nowSelected = false;
        }
        else
        {
            state.selected.Add(c);
            nowSelected = true;
        }

        var th = c.GetComponent<TargetHighlightController>();
        if (th != null)
        {
            th.SetHighlighted(nowSelected);
        }
        // Update confirm button state after any change.
        UpdateSelectionUIState();
    }

    /// <summary>
    /// Helper for GameManager to detect whether a given effect action is the
    /// confirmation for the current manual-selection state.
    /// </summary>
    public bool IsConfirming(EffectCard card, SlotOwner owner)
    {
        return _state != null && _state.card == card && _state.owner == owner;
    }

    /// <summary>
    /// Called by GameManager once the effect has been fully processed so we can
    /// clear out any remaining selection state.
    /// </summary>
    public void ClearSelection()
    {
        _state = null;
    }

    private void OnConfirmClicked()
    {
        if (_state == null || gameManager == null)
            return;

        var state = _state;
        if (state.card == null)
            return;

        int selectedCount = state.selected != null ? state.selected.Count : 0;
        if (selectedCount == 0)
            return;

        bool canConfirm;
        if (state.allowFewerThanMax)
        {
            canConfirm =
                selectedCount >= Mathf.Max(1, state.minCount)
                && selectedCount <= Mathf.Max(1, state.maxCount);
        }
        else
        {
            canConfirm = selectedCount == Mathf.Max(1, state.maxCount);
        }

        if (!canConfirm)
            return;

        // Manual finalize: clear all highlights and resolve the effect.
        var finalTargets = state.selected.Where(t => t != null).ToList();

        foreach (var cand in state.candidates)
        {
            if (cand == null)
                continue;
            var h = cand.GetComponent<TargetHighlightController>();
            if (h != null)
                h.SetHighlighted(false);
        }

        var finalCard = state.card;
        var finalOwner = state.owner;

        SetSelectionVisible(false);

        var finalTargetIndices = finalTargets
            .Select(t => BoardUtils.GetSlotOf(t))
            .Where(s => s != null)
            .Select(s => s.index)
            .ToList();

        // Route through the LocalHumanController to ensure the action is broadcast
        // to both the local GameManager AND the network (if in networked mode).
        // We intentionally keep _state populated here so GameManager can detect
        // this as a manual confirmation and avoid re-checking momentum that was
        // already spent.
        if (gameManager.GetPlayerController(finalOwner) is LocalHumanController human)
        {
            human.RequestPlayEffect(finalCard.cardId, finalTargetIndices);
        }
        else
        {
            // Fallback for non-human controllers (shouldn't happen for local player)
            gameManager.EnqueueLocalAction(
                new GameAction
                {
                    type = GameActionType.PlayEffect,
                    owner = finalOwner,
                    cardId = finalCard.cardId,
                    targetSlotIndices = finalTargetIndices,
                }
            );
        }
    }

    private void OnCancelClicked()
    {
        CancelSelection();
    }

    public void CancelSelection()
    {
        if (_state == null)
            return;

        var state = _state;
        _state = null;

        // Clear highlights on all candidates.
        foreach (var cand in state.candidates)
        {
            if (cand == null)
                continue;
            var h = cand.GetComponent<TargetHighlightController>();
            if (h != null)
                h.SetHighlighted(false);
        }

        // Hide preview and selection UI.
        CardPreviewManager.Instance?.HideAll();
        SetSelectionVisible(false);

        // Refund momentum and return the card to hand for the local player.
        if (NetworkRoleHelper.IsLocalPlayer(state.owner))
        {
            int refund = Mathf.Max(0, state.card != null ? state.card.momentumCost : 0);
            if (refund > 0 && gameManager != null)
            {
                gameManager.RefundMomentum(state.owner, refund);
            }

            if (DeckManager.Instance != null && state.card != null)
            {
                DeckManager.Instance.CreateCardUI(state.card, triggerLayoutAndUI: true);
            }

            // Unlock action so the player can act again.
            if (gameManager != null)
            {
                gameManager.SetPlayerActionLocked(state.owner, false);
            }
        }
    }

    private void SetSelectionVisible(bool visible)
    {
        if (selectionGroup == null)
            return;
        selectionGroup.alpha = visible ? 1f : 0f;
        selectionGroup.interactable = visible;
        selectionGroup.blocksRaycasts = visible;
    }

    private void UpdateSelectionUIState()
    {
        if (!HasActiveSelection)
        {
            SetSelectionVisible(false);
            return;
        }

        var state = _state;
        if (state == null)
        {
            SetSelectionVisible(false);
            return;
        }

        SetSelectionVisible(true);

        int selectedCount = state.selected != null ? state.selected.Count : 0;
        bool canConfirm;

        if (state.allowFewerThanMax)
        {
            canConfirm =
                selectedCount >= Mathf.Max(1, state.minCount)
                && selectedCount <= Mathf.Max(1, state.maxCount);
        }
        else
        {
            canConfirm = selectedCount == Mathf.Max(1, state.maxCount);
        }

        if (confirmButton != null)
            confirmButton.interactable = canConfirm;

        if (cancelButton != null)
            cancelButton.interactable = true;
    }
}
