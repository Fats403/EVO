using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Shared helper for draft/deck-building rules:
/// - Converts card assets into typed entries.
/// - Chooses preferred card type and cost tier based on DraftConfig.
/// - Builds candidate sets under copy caps.
/// Used by both DraftManager (player UI draft) and BalancedDeckBuilder (AI/random decks).
/// </summary>
public static class DraftRules
{
    public class CardEntry
    {
        public ScriptableObject data;
        public bool isCreature;
        public int momentumCost;
        public int costTier;
    }

    /// <summary>
    /// Build an entry pool from the given ScriptableObjects using the provided DraftConfig.
    /// Only CreatureCard and EffectCard assets are included.
    /// </summary>
    public static List<CardEntry> BuildEntryPool(
        IEnumerable<ScriptableObject> pool,
        DraftConfig config
    )
    {
        var list = new List<CardEntry>();
        if (pool == null || config == null)
            return list;

        foreach (var so in pool)
        {
            if (so == null)
                continue;

            var entry = new CardEntry { data = so };

            if (so is CreatureCard creature)
            {
                entry.isCreature = true;
                entry.momentumCost = Mathf.Max(0, creature.momentumCost);
            }
            else if (so is EffectCard effect)
            {
                entry.isCreature = false;
                entry.momentumCost = Mathf.Max(0, effect.momentumCost);
            }
            else
            {
                // Unsupported type for drafting/deckbuilding; skip.
                continue;
            }

            entry.costTier = config.GetCostTier(entry.momentumCost);
            list.Add(entry);
        }

        return list;
    }

    /// <summary>
    /// Decide whether we should prefer creatures or effects for this pick,
    /// blending the current ratio vs the target ratio using typeBiasStrength.
    /// </summary>
    public static bool ChoosePreferredIsCreature(
        DraftConfig config,
        int picksDone,
        int creatureCount
    )
    {
        if (config == null)
            return true;

        if (picksDone == 0)
            return true; // first pick creature to anchor a board.

        float currentCreatureRatio =
            picksDone > 0 ? (float)creatureCount / picksDone : config.targetCreatureRatio;

        // Blend current vs target using a simple bias factor.
        float bias = Mathf.Max(0f, config.typeBiasStrength);
        // When bias is 0, we mostly keep current ratio; when high, we push toward target.
        float blendedTarget =
            (1f / (1f + bias)) * currentCreatureRatio
            + (bias / (1f + bias)) * config.targetCreatureRatio;

        // If we're below blended target, prefer creatures; otherwise prefer effects.
        return currentCreatureRatio <= blendedTarget;
    }

    /// <summary>
    /// Roll whether a single offered card should be a creature.
    /// Unlike ChoosePreferredIsCreature (deterministic), this returns a probability-shaped
    /// random result so each offer slot can independently be creature/effect.
    /// </summary>
    public static bool RollIsCreature(DraftConfig config, int picksDone, int creatureCount)
    {
        if (config == null)
            return true;
        if (picksDone == 0)
            return true; // first pick creature to anchor a board.

        float currentRatio =
            picksDone > 0 ? (float)creatureCount / picksDone : config.targetCreatureRatio;
        float desired = Mathf.Clamp01(config.targetCreatureRatio);

        float bias = Mathf.Max(0f, config.typeBiasStrength);
        // Convert bias into a 0..1 strength where 0 = very loose, 1 = very strong.
        float strength = bias / (bias + 1f);

        // If we're above the desired ratio, reduce creature probability; if below, increase it.
        float p = Mathf.Clamp01(desired + (desired - currentRatio) * strength);

        return NextRandomInt(0, 10000) < Mathf.RoundToInt(p * 10000f);
    }

    /// <summary>
    /// Decide which momentum cost tier (low/mid/high) we prefer for this pick,
    /// based on how far each tier is below its target and costBiasStrength.
    /// </summary>
    public static int ChoosePreferredCostTier(
        DraftConfig config,
        int lowCount,
        int midCount,
        int highCount
    )
    {
        if (config == null)
            return 0;

        // Compute deficits relative to targets.
        int lowDeficit = config.targetLowCount - lowCount;
        int midDeficit = config.targetMidCount - midCount;
        int highDeficit = config.targetHighCount - highCount;

        float bias = Mathf.Max(0f, config.costBiasStrength);

        // If bias is 0, treat all tiers equally weighted; otherwise magnify the deficit difference.
        float lowScore = Mathf.Pow(Mathf.Max(0, lowDeficit), 1f + bias);
        float midScore = Mathf.Pow(Mathf.Max(0, midDeficit), 1f + bias);
        float highScore = Mathf.Pow(Mathf.Max(0, highDeficit), 1f + bias);

        if (lowScore >= midScore && lowScore >= highScore)
            return 0;
        if (midScore >= lowScore && midScore >= highScore)
            return 1;
        return 2;
    }

    /// <summary>
    /// Roll a desired tier for a single offered card, based on deficits and costBiasStrength.
    /// This is intended for per-slot offer generation so the three cards can vary naturally.
    /// </summary>
    public static int RollDesiredCostTier(
        DraftConfig config,
        int lowCount,
        int midCount,
        int highCount
    )
    {
        if (config == null)
            return 0;

        int lowDeficit = config.targetLowCount - lowCount;
        int midDeficit = config.targetMidCount - midCount;
        int highDeficit = config.targetHighCount - highCount;

        float bias = Mathf.Max(0f, config.costBiasStrength);
        float exponent = 1f + bias;

        // If we are at/above all targets, treat tiers evenly.
        float baseLow = Mathf.Max(0, lowDeficit);
        float baseMid = Mathf.Max(0, midDeficit);
        float baseHigh = Mathf.Max(0, highDeficit);
        if (baseLow <= 0 && baseMid <= 0 && baseHigh <= 0)
        {
            baseLow = baseMid = baseHigh = 1f;
        }
        else
        {
            // Add a small baseline so we still occasionally see other tiers.
            baseLow += 0.35f;
            baseMid += 0.35f;
            baseHigh += 0.35f;
        }

        float wLow = Mathf.Pow(baseLow, exponent);
        float wMid = Mathf.Pow(baseMid, exponent);
        float wHigh = Mathf.Pow(baseHigh, exponent);

        float sum = wLow + wMid + wHigh;
        if (sum <= 0f)
            return 0;

        float roll = NextRandomInt(0, 10000) / 10000f * sum;
        if (roll < wLow)
            return 0;
        roll -= wLow;
        if (roll < wMid)
            return 1;
        return 2;
    }

    /// <summary>
    /// Build a candidate list of CardEntry values under the given copy caps,
    /// preferring the specified type and cost tier but relaxing constraints as needed.
    /// </summary>
    public static List<CardEntry> BuildCandidates(
        List<CardEntry> entries,
        DraftConfig config,
        bool preferCreature,
        int desiredTier,
        Dictionary<ScriptableObject, int> copiesPerCard
    )
    {
        var result = new List<CardEntry>();
        if (entries == null || entries.Count == 0 || config == null)
            return result;

        int maxCopies = Mathf.Max(1, config.maxCopiesPerCard);

        bool UnderCopyCap(CardEntry e)
        {
            if (e == null || e.data == null)
                return false;
            int count = copiesPerCard.TryGetValue(e.data, out int c) ? c : 0;
            return count < maxCopies;
        }

        // 1) Strict: desired type + desired tier
        result = entries
            .Where(e =>
                e.isCreature == preferCreature && e.costTier == desiredTier && UnderCopyCap(e)
            )
            .ToList();
        if (result.Count >= 3)
            return result;

        // 2) Relax tier: any tier of desired type
        var typeOnly = entries
            .Where(e => e.isCreature == preferCreature && UnderCopyCap(e))
            .ToList();
        MergeUnique(result, typeOnly);
        if (result.Count >= 3)
            return result;

        // 3) Allow opposite type but keep desired tier
        var tierOnly = entries.Where(e => e.costTier == desiredTier && UnderCopyCap(e)).ToList();
        MergeUnique(result, tierOnly);
        if (result.Count >= 3)
            return result;

        // 4) Fully relaxed: any under copy cap
        var any = entries.Where(UnderCopyCap).ToList();
        MergeUnique(result, any);

        return result;
    }

    /// <summary>
    /// Increment the low/mid/high counters for a given momentum tier.
    /// </summary>
    public static void IncrementTierCount(
        int tier,
        ref int lowCount,
        ref int midCount,
        ref int highCount
    )
    {
        switch (tier)
        {
            case 0:
                lowCount++;
                break;
            case 1:
                midCount++;
                break;
            default:
                highCount++;
                break;
        }
    }

    /// <summary>
    /// Advance both creature/effect counts and the low/mid/high tier counts for a picked card.
    /// </summary>
    public static void IncrementCountersForPicked(
        DraftConfig config,
        ScriptableObject picked,
        ref int creatureCount,
        ref int effectCount,
        ref int lowCount,
        ref int midCount,
        ref int highCount
    )
    {
        if (config == null || picked == null)
            return;

        if (picked is CreatureCard creature)
        {
            creatureCount++;
            int tier = config.GetCostTier(creature.momentumCost);
            IncrementTierCount(tier, ref lowCount, ref midCount, ref highCount);
        }
        else if (picked is EffectCard effect)
        {
            effectCount++;
            int tier = config.GetCostTier(effect.momentumCost);
            IncrementTierCount(tier, ref lowCount, ref midCount, ref highCount);
        }
    }

    /// <summary>
    /// Shared RNG helper that respects GameManager's seeded RNG when available.
    /// </summary>
    public static int NextRandomInt(int minInclusive, int maxExclusive)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.NextRandomInt(minInclusive, maxExclusive);
        }

        Debug.LogWarning("DraftRules: GameManager.Instance is null during NextRandomInt. Determinism may be compromised.");
        return Random.Range(minInclusive, maxExclusive);
    }

    private static void MergeUnique(List<CardEntry> target, List<CardEntry> source)
    {
        foreach (var e in source)
        {
            if (e != null && !target.Contains(e))
                target.Add(e);
        }
    }
}
