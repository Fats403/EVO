using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of all card definitions in the game.
/// - Holds references to all CardDefinition assets (creatures, effects, etc.).
/// - Builds a fast lookup from cardId -> CardDefinition for decks, saves, and netcode.
/// </summary>
[CreateAssetMenu(fileName = "CardDatabase", menuName = "Cards/Card Database")]
public class CardDatabase : ScriptableObject
{
    [Tooltip("All card definitions registered in the game (CreatureCard, EffectCard, etc.).")]
    public List<CardDefinition> allCards = new();

    private Dictionary<string, CardDefinition> _byId;

    private void OnEnable()
    {
        BuildLookup();
    }

    /// <summary>Rebuilds the runtime lookup dictionary from the allCards list.</summary>
    public void BuildLookup()
    {
        _byId = new Dictionary<string, CardDefinition>();

        if (allCards == null)
            return;

        foreach (var card in allCards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardId))
                continue;

            if (_byId.ContainsKey(card.cardId))
            {
                Debug.LogWarning(
                    $"CardDatabase: Duplicate cardId '{card.cardId}' found on {card.name}."
                );
                continue;
            }

            _byId.Add(card.cardId, card);
        }
    }

    /// <summary>Try to get a CardDefinition by its cardId.</summary>
    public bool TryGetById(string cardId, out CardDefinition card)
    {
        card = null;
        if (string.IsNullOrEmpty(cardId))
            return false;

        if (_byId == null || _byId.Count == 0)
            BuildLookup();

        return _byId.TryGetValue(cardId, out card);
    }

    /// <summary>Get a CardDefinition by cardId, or null if not found.</summary>
    public CardDefinition GetById(string cardId)
    {
        return TryGetById(cardId, out var card) ? card : null;
    }
}


