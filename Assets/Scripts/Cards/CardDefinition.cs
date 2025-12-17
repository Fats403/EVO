using UnityEngine;

/// <summary>
/// Base ScriptableObject for all card definitions (creatures, effects, etc.).
/// Provides a shared view of the common identity fields used by decks, saves, and UI.
/// </summary>
public abstract class CardDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique ID for this card (used for decks, saves, multiplayer).")]
    public string cardId;

    /// <summary>Display name shown in UI (card frame, logs, etc.).</summary>
    public abstract string DisplayName { get; }

    /// <summary>Main artwork sprite for the card.</summary>
    public abstract Sprite Artwork { get; }

    /// <summary>Momentum cost to play this card.</summary>
    public abstract int MomentumCost { get; }
}
