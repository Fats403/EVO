using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for a card choice prompt. Supports various scenarios:
/// - Mulligan: Choose cards to replace from starting hand
/// - Card draw effects: "Look at top 3, pick 1"
/// - Discard effects: "Choose cards to discard"
/// - Target selection: "Choose a card from opponent's hand" (revealed)
/// </summary>
[Serializable]
public class CardChoiceRequest
{
    [Header("Display")]
    [Tooltip("Title shown at the top of the choice panel.")]
    public string title = "Choose Cards";

    [Tooltip("Optional subtitle/instruction text.")]
    public string subtitle = "";

    [Header("Cards")]
    [Tooltip("The cards available for selection.")]
    public List<ScriptableObject> cards = new();

    [Tooltip("If true, cards are shown face-down (for hidden information scenarios).")]
    public bool showFaceDown = false;

    [Header("Selection Rules")]
    [Tooltip("Minimum number of cards that must be selected to confirm.")]
    [Min(0)]
    public int minPicks = 1;

    [Tooltip("Maximum number of cards that can be selected.")]
    [Min(1)]
    public int maxPicks = 1;

    [Tooltip("If true, order of selection matters (first picked, second picked, etc.).")]
    public bool orderMatters = false;

    [Header("Buttons")]
    [Tooltip("Text for the confirm button.")]
    public string confirmButtonText = "Confirm";

    [Tooltip("If true, a cancel button is shown allowing the player to back out.")]
    public bool allowCancel = false;

    [Tooltip("Text for the cancel button (if allowCancel is true).")]
    public string cancelButtonText = "Cancel";

    [Tooltip("If true, allows confirming with zero selections when minPicks is 0.")]
    public bool allowEmpty = false;

    [Header("Timing")]
    [Tooltip("Optional timeout in seconds. 0 or negative means no timeout.")]
    public float timeoutSeconds = 0f;

    [Tooltip("What happens on timeout: confirm with current selection, or cancel.")]
    public CardChoiceTimeoutBehavior timeoutBehavior = CardChoiceTimeoutBehavior.ConfirmCurrent;

    [Header("Callbacks")]
    /// <summary>
    /// Called when the player confirms their selection.
    /// The list contains the selected cards in selection order (if orderMatters).
    /// </summary>
    public Action<List<ScriptableObject>> onConfirm;

    /// <summary>
    /// Called when the player cancels (only if allowCancel is true).
    /// </summary>
    public Action onCancel;

    /// <summary>
    /// Optional: Called each time a card is selected/deselected for live feedback.
    /// </summary>
    public Action<List<ScriptableObject>> onSelectionChanged;

    // ----- Static Factory Methods for Common Scenarios -----

    /// <summary>
    /// Creates a mulligan request: player chooses which cards to replace from their hand.
    /// </summary>
    public static CardChoiceRequest Mulligan(
        List<ScriptableObject> handCards,
        Action<List<ScriptableObject>> onCardsToReplace,
        int maxReplace = -1
    )
    {
        int max = maxReplace > 0 ? maxReplace : handCards.Count;
        return new CardChoiceRequest
        {
            title = "Mulligan",
            subtitle = $"Select up to {max} cards to replace",
            cards = new List<ScriptableObject>(handCards),
            minPicks = 0,
            maxPicks = max,
            allowEmpty = true,
            confirmButtonText = "Keep Hand",
            allowCancel = false,
            onConfirm = onCardsToReplace,
        };
    }

    /// <summary>
    /// Creates a "look at top N, pick M" request (like scry/discover mechanics).
    /// </summary>
    public static CardChoiceRequest LookAndPick(
        string title,
        List<ScriptableObject> topCards,
        int pickCount,
        Action<List<ScriptableObject>> onPicked,
        bool orderMatters = false
    )
    {
        return new CardChoiceRequest
        {
            title = title,
            subtitle =
                pickCount == 1
                    ? "Choose 1 card to add to your hand"
                    : $"Choose {pickCount} cards to add to your hand",
            cards = new List<ScriptableObject>(topCards),
            minPicks = pickCount,
            maxPicks = pickCount,
            orderMatters = orderMatters,
            confirmButtonText = "Add to Hand",
            allowCancel = false,
            onConfirm = onPicked,
        };
    }

    /// <summary>
    /// Creates a discard request: player must discard N cards.
    /// </summary>
    public static CardChoiceRequest Discard(
        List<ScriptableObject> handCards,
        int discardCount,
        Action<List<ScriptableObject>> onDiscarded
    )
    {
        return new CardChoiceRequest
        {
            title = "Discard",
            subtitle = $"Choose {discardCount} card{(discardCount > 1 ? "s" : "")} to discard",
            cards = new List<ScriptableObject>(handCards),
            minPicks = discardCount,
            maxPicks = discardCount,
            confirmButtonText = "Discard",
            allowCancel = false,
            onConfirm = onDiscarded,
        };
    }

    /// <summary>
    /// Creates a generic "pick one" choice from a set of cards.
    /// </summary>
    public static CardChoiceRequest PickOne(
        string title,
        List<ScriptableObject> options,
        Action<ScriptableObject> onPicked,
        bool canCancel = false,
        Action onCancelled = null
    )
    {
        return new CardChoiceRequest
        {
            title = title,
            cards = new List<ScriptableObject>(options),
            minPicks = 1,
            maxPicks = 1,
            confirmButtonText = "Select",
            allowCancel = canCancel,
            cancelButtonText = "Cancel",
            onConfirm = (list) => onPicked?.Invoke(list.Count > 0 ? list[0] : null),
            onCancel = onCancelled,
        };
    }

    /// <summary>
    /// Creates a choice for opponent's revealed cards (e.g., hand disruption).
    /// </summary>
    public static CardChoiceRequest FromOpponent(
        string title,
        string subtitle,
        List<ScriptableObject> revealedCards,
        int pickCount,
        Action<List<ScriptableObject>> onPicked
    )
    {
        return new CardChoiceRequest
        {
            title = title,
            subtitle = subtitle,
            cards = new List<ScriptableObject>(revealedCards),
            minPicks = pickCount,
            maxPicks = pickCount,
            confirmButtonText = "Confirm",
            allowCancel = false,
            onConfirm = onPicked,
        };
    }
}

public enum CardChoiceTimeoutBehavior
{
    /// <summary>Confirm with whatever is currently selected.</summary>
    ConfirmCurrent,

    /// <summary>Treat as cancel (calls onCancel if allowed, otherwise confirms empty).</summary>
    Cancel,

    /// <summary>Auto-select random cards to meet minPicks, then confirm.</summary>
    RandomFill,
}
