using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the opponent's deck and hand state in networked games.
/// Provides information for UI display (opponent hand count, deck remaining).
/// </summary>
public class OpponentDeckTracker : MonoBehaviour
{
    public static OpponentDeckTracker Instance { get; private set; }

    /// <summary>Number of cards remaining in the opponent's deck.</summary>
    public int DeckRemaining { get; private set; }

    /// <summary>Number of cards currently in the opponent's hand.</summary>
    public int HandSize { get; private set; }

    /// <summary>Total size of the opponent's deck at game start.</summary>
    public int StartingDeckSize { get; private set; }

    /// <summary>List of card IDs the opponent has played (revealed after being played).</summary>
    public List<string> PlayedCardIds { get; } = new();

    /// <summary>Raised when any tracked state changes.</summary>
    public event Action OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Initialize the tracker with the opponent's deck entries.
    /// Called by GameSessionBootstrapper when starting a networked game.
    /// </summary>
    public void Initialize(DeckCardEntry[] deckEntries)
    {
        int total = 0;
        if (deckEntries != null)
        {
            foreach (var e in deckEntries)
                total += e.count;
        }

        StartingDeckSize = total;
        DeckRemaining = total;
        HandSize = 0;
        PlayedCardIds.Clear();

        Debug.Log($"OpponentDeckTracker: Initialized with {total} cards in deck.");
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Called when the opponent draws cards (e.g., at round start).
    /// </summary>
    public void OnOpponentDrew(int count)
    {
        int actualDraw = Mathf.Min(count, DeckRemaining);
        HandSize += actualDraw;
        DeckRemaining -= actualDraw;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Called when the opponent plays a card from their hand.
    /// </summary>
    public void OnOpponentPlayed(string cardId)
    {
        HandSize = Mathf.Max(0, HandSize - 1);
        if (!string.IsNullOrEmpty(cardId))
            PlayedCardIds.Add(cardId);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Resets the tracker state.
    /// </summary>
    public void Clear()
    {
        DeckRemaining = 0;
        HandSize = 0;
        StartingDeckSize = 0;
        PlayedCardIds.Clear();
        OnStateChanged?.Invoke();
    }
}

