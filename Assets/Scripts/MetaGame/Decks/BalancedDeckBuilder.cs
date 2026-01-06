using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Helper for building a momentum- and type-balanced deck using the same
/// rules as the player draft (DraftConfig), but without any UI.
/// Used for AI decks and the "Random Deck" option.
/// </summary>
public static class BalancedDeckBuilder
{
    /// <summary>
    /// Build a single balanced deck from the given card pool using the
    /// targets and bias strengths from DraftConfig.
    /// </summary>
    public static List<ScriptableObject> BuildDeck(
        IEnumerable<ScriptableObject> pool,
        DraftConfig config
    )
    {
        var result = new List<ScriptableObject>();
        if (pool == null || config == null)
            return result;

        // Build entry pool from all CreatureCard and EffectCard assets.
        var entries = DraftRules.BuildEntryPool(pool, config);
        if (entries.Count == 0)
            return result;

        var copiesPerCard = new Dictionary<ScriptableObject, int>();
        int creatureCount = 0;
        int effectCount = 0;
        int lowCount = 0;
        int midCount = 0;
        int highCount = 0;

        for (int picksDone = 0; picksDone < GameRules.DeckSize; picksDone++)
        {
            bool preferCreature = DraftRules.ChoosePreferredIsCreature(
                config,
                picksDone,
                creatureCount
            );
            int desiredTier = DraftRules.ChoosePreferredCostTier(
                config,
                lowCount,
                midCount,
                highCount
            );

            var candidates = DraftRules.BuildCandidates(
                entries,
                config,
                preferCreature,
                desiredTier,
                copiesPerCard
            );

            if (candidates.Count == 0)
            {
                // No candidates left under copy caps; stop early.
                break;
            }

            // Pick one candidate at random.
            int index = DraftRules.NextRandomInt(0, candidates.Count);
            var pickedEntry = candidates[index];
            var picked = pickedEntry.data;

            result.Add(picked);

            // Update counters
            DraftRules.IncrementCountersForPicked(
                config,
                picked,
                ref creatureCount,
                ref effectCount,
                ref lowCount,
                ref midCount,
                ref highCount
            );

            if (!copiesPerCard.ContainsKey(picked))
                copiesPerCard[picked] = 0;
            copiesPerCard[picked]++;
        }

        return result;
    }
}
