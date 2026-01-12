using UnityEngine;

/// <summary>
/// Tunable configuration for the draft system: deck size, desired
/// creature/effect ratio, momentum curve targets, and bias strengths.
/// </summary>
[CreateAssetMenu(menuName = "Config/Draft Config")]
public class DraftConfig : ScriptableObject
{
    [Header("Type Mix Targets")]
    [Range(0f, 1f)]
    [Tooltip("Target fraction of the deck that should be creature cards.")]
    public float targetCreatureRatio = 0.65f;

    [Tooltip("Maximum copies of any single card allowed in the drafted deck.")]
    [Min(1)]
    public int maxCopiesPerCard = 2;

    [Header("Momentum Buckets")]
    [Tooltip("Maximum momentum cost for a card to be considered 'low cost' (inclusive).")]
    [Min(0)]
    public int lowCostMax = 2; // 1–2 → low

    [Tooltip("Maximum momentum cost for a card to be considered 'mid cost' (inclusive).")]
    [Min(0)]
    public int midCostMax = 3; // 3 → mid

    [Tooltip(
        "Maximum momentum cost for a card to be considered 'high cost' (inclusive). Cards above this are 'apex'."
    )]
    [Min(0)]
    public int highCostMax = 4; // 4 → high; > highCostMax (5+) → apex

    [Tooltip("Target number of low-cost cards (1-2) in the final deck.")]
    [Min(0)]
    public int targetLowCount = 10;

    [Tooltip("Target number of mid-cost cards (3) in the final deck.")]
    [Min(0)]
    public int targetMidCount = 7;

    [Tooltip("Target number of high-cost cards (4) in the final deck.")]
    [Min(0)]
    public int targetHighCount = 5;

    [Tooltip("Target number of apex-cost cards (5+) in the final deck.")]
    [Min(0)]
    public int targetApexCount = 2;

    [Header("Apex Card Restrictions")]
    [Tooltip("Maximum apex-tier cards allowed per deck (overrides targetApexCount if lower).")]
    [Min(0)]
    public int maxApexCardsInDeck = 2;

    [Tooltip("Maximum copies of any single apex-tier card (typically 1 for uniqueness).")]
    [Min(1)]
    public int maxCopiesOfApexCard = 1;

    [Header("Bias Tuning")]
    [Tooltip(
        "How strongly to steer each pick toward the creature/effect ratio (0 = almost ignore, 2+ = very strong)."
    )]
    [Min(0f)]
    public float typeBiasStrength = 1.0f;

    [Tooltip(
        "How strongly to steer each pick toward the low/mid/high/apex momentum curve (0 = almost ignore, 2+ = very strong)."
    )]
    [Min(0f)]
    public float costBiasStrength = 1.0f;

    /// <summary>
    /// Returns an integer cost tier index based on the configured bucket thresholds.
    /// 0 = low, 1 = mid, 2 = high, 3 = apex.
    /// </summary>
    public int GetCostTier(int momentumCost)
    {
        if (momentumCost <= lowCostMax)
            return 0;
        if (momentumCost <= midCostMax)
            return 1;
        if (momentumCost <= highCostMax)
            return 2;
        return 3; // apex tier (5+ cost)
    }

    /// <summary>
    /// Returns true if the given cost is considered apex tier.
    /// </summary>
    public bool IsApexCost(int momentumCost) => momentumCost > highCostMax;

    /// <summary>
    /// Returns the effective max copies for a card based on its cost.
    /// Apex cards have stricter copy limits.
    /// </summary>
    public int GetMaxCopiesForCost(int momentumCost)
    {
        return IsApexCost(momentumCost) ? maxCopiesOfApexCard : maxCopiesPerCard;
    }
}
