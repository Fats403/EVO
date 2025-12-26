using System;
using System.Collections.Generic;

/// <summary>
/// Simple serialisable entry describing how many copies of a given cardId
/// belong in a deck. This is the canonical deck representation that can be
/// stored, sent over the network, or converted into concrete card assets.
/// </summary>
[Serializable]
public struct DeckCardEntry
{
    public string cardId;
    public int count;
}

/// <summary>
/// High-level description of a deck: identity, display name, slot index,
/// and the list of card entries that make it up.
/// </summary>
[Serializable]
public class DeckDefinition
{
    public string deckId;
    public string deckName;
    public int slotIndex;
    public List<DeckCardEntry> cards = new List<DeckCardEntry>();
}


