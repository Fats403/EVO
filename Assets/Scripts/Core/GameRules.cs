using UnityEngine;

/// <summary>
/// Centralised game-wide constants for deck/hand configuration so that
/// DeckManager, AI, draft, and deck-builder all stay in sync.
///
/// If you want to tweak core numbers (deck size, starting hand, draws per
/// round, max hand size), change them here.
/// </summary>
public static class GameRules
{
    /// <summary>Number of cards in a standard constructed deck.</summary>
    public const int DeckSize = 24;

    /// <summary>Number of cards in the starting hand at round 1.</summary>
    public const int StartingHandSize = 4;

    /// <summary>Number of cards drawn automatically at the start of each round (from round 2+).</summary>
    public const int CardsPerRound = 1;

    /// <summary>Maximum number of cards allowed in hand.</summary>
    public const int MaxHandSize = 7;
}
