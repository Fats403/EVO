using System.Collections.Generic;

/// <summary>
/// Simple static store used to pass deck selection from the DeckHubScene
/// into the gameplay scene (MainScene).
/// </summary>
public enum GameStartMode
{
    None,
    Draft,
    Constructed,
}

public static class SelectedDeckStore
{
    public static GameStartMode Mode = GameStartMode.None;

    public static string DeckId;
    public static string DeckName;
    public static int SlotIndex;

    public static List<DeckBuilderManager.DeckCardEntry> Cards { get; } =
        new List<DeckBuilderManager.DeckCardEntry>();

    public static bool HasConstructedDeck =>
        Mode == GameStartMode.Constructed && Cards != null && Cards.Count > 0;

    public static void SetConstructedDeck(
        string deckId,
        string deckName,
        int slotIndex,
        List<DeckBuilderManager.DeckCardEntry> cards
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

    public static void SetDraftMode()
    {
        Mode = GameStartMode.Draft;
        DeckId = null;
        DeckName = null;
        SlotIndex = -1;
        Cards.Clear();
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
