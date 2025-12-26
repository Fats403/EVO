using UnityEngine;

/// <summary>
/// Deck provider that builds a DeckDefinition from the current
/// SelectedDeckStore state. Intended for constructed mode where the
/// player has chosen a saved deck in the DeckHubScene.
/// </summary>
public class ConstructedDeckProvider : MonoBehaviour, IDeckProvider
{
    [Tooltip("Global card database used to resolve cardIds into card assets.")]
    [SerializeField]
    private CardDatabase cardDatabase;

    public DeckDefinition BuildLocalDeckDefinition()
    {
        if (!SelectedDeckStore.HasConstructedDeck)
        {
            Debug.LogError(
                "ConstructedDeckProvider: BuildLocalDeckDefinition called with no constructed deck selected."
            );
            return null;
        }

        var def = new DeckDefinition
        {
            deckId = SelectedDeckStore.DeckId,
            deckName = SelectedDeckStore.DeckName,
            slotIndex = SelectedDeckStore.SlotIndex,
        };

        if (SelectedDeckStore.Cards != null && SelectedDeckStore.Cards.Count > 0)
        {
            def.cards.AddRange(SelectedDeckStore.Cards);
        }

        return def;
    }
}


