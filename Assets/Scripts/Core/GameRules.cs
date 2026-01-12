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

    // --- Apex (5-Cost) Card Restrictions ---

    /// <summary>Minimum momentum cost for a card to be considered "apex" tier.</summary>
    public const int ApexCostThreshold = 5;

    /// <summary>Maximum number of apex-tier (5+ cost) cards allowed in a single deck.</summary>
    public const int MaxApexCardsPerDeck = 2;

    /// <summary>Maximum copies of any single apex-tier card allowed in a deck.</summary>
    public const int MaxCopiesOfApexCard = 1;

    /// <summary>
    /// Returns true if the given momentum cost qualifies as apex tier.
    /// </summary>
    public static bool IsApexCost(int momentumCost) => momentumCost >= ApexCostThreshold;
}
