using System.Collections.Generic;

/// <summary>
/// Simple static store used to pass deck selection from the DeckHubScene
/// into the gameplay scene (MainScene).
/// </summary>
public enum GameStartMode
{
    None,
    Constructed,
}

public static class SelectedDeckStore
{
    public static GameStartMode Mode = GameStartMode.None;

    public static string DeckId;
    public static string DeckName;
    public static int SlotIndex;

    /// <summary>
    /// Canonical deck contents chosen in the DeckHubScene. This now uses the
    /// shared DeckCardEntry type so it can be consumed by both gameplay and
    /// networking systems without depending on DeckBuilderManager.
    /// </summary>
    public static List<DeckCardEntry> Cards { get; } = new List<DeckCardEntry>();

    public static bool HasConstructedDeck =>
        Mode == GameStartMode.Constructed && Cards != null && Cards.Count > 0;

    public static void SetConstructedDeck(
        string deckId,
        string deckName,
        int slotIndex,
        List<DeckCardEntry> cards
    )
    {
        Mode = GameStartMode.Constructed;
        DeckId = deckId;
        DeckName = deckName;
        SlotIndex = slotIndex;

        Cards.Clear();
        if (cards != null)
            Cards.AddRange(cards);
    }

    public static void Clear()
    {
        Mode = GameStartMode.None;
        DeckId = null;
        DeckName = null;
        SlotIndex = -1;
        Cards.Clear();
    }
}
