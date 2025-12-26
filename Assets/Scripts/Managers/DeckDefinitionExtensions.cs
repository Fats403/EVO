using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helper extensions for converting between deck definitions and runtime
/// card asset lists used by DeckManager and other systems.
/// </summary>
public static class DeckDefinitionExtensions
{
    /// <summary>
    /// Resolves the cardIds in this deck definition into concrete ScriptableObject
    /// card assets using the provided CardDatabase. Each entry's count is expanded
    /// into that many copies in the returned list.
    /// </summary>
    public static List<ScriptableObject> ToCardAssets(
        this DeckDefinition def,
        CardDatabase cardDatabase
    )
    {
        var list = new List<ScriptableObject>();

        if (def == null)
            return list;

        if (cardDatabase == null)
        {
            Debug.LogError("DeckDefinitionExtensions.ToCardAssets: CardDatabase is null.");
            return list;
        }

        if (def.cards == null || def.cards.Count == 0)
            return list;

        foreach (var entry in def.cards)
        {
            if (string.IsNullOrEmpty(entry.cardId) || entry.count <= 0)
                continue;

            var cardDef = cardDatabase.GetById(entry.cardId);
            if (cardDef == null)
            {
                Debug.LogWarning(
                    $"DeckDefinitionExtensions: Card with id '{entry.cardId}' not found in CardDatabase."
                );
                continue;
            }

            for (int i = 0; i < entry.count; i++)
            {
                list.Add(cardDef);
            }
        }

        return list;
    }
}


