using UnityEngine;

/// <summary>
/// Tunable configuration for the draft system: deck size, desired
/// creature/effect ratio, momentum curve targets, and bias strengths.
/// </summary>
[CreateAssetMenu(menuName = "Config/Draft Config")]
public class DraftConfig : ScriptableObject
{
    [Header("Deck Size")]
    [Min(1)]
    public int deckSize = 20;

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
    public int midCostMax = 3; // 3 → mid; > midCostMax → high

    [Tooltip("Target number of low-cost cards in the final 20-card deck.")]
    [Min(0)]
    public int targetLowCount = 9;

    [Tooltip("Target number of mid-cost cards in the final 20-card deck.")]
    [Min(0)]
    public int targetMidCount = 7;

    [Tooltip("Target number of high-cost cards in the final 20-card deck.")]
    [Min(0)]
    public int targetHighCount = 4;

    [Header("Bias Tuning")]
    [Tooltip(
        "How strongly to steer each pick toward the creature/effect ratio (0 = almost ignore, 2+ = very strong)."
    )]
    [Min(0f)]
    public float typeBiasStrength = 1.0f;

    [Tooltip(
        "How strongly to steer each pick toward the low/mid/high momentum curve (0 = almost ignore, 2+ = very strong)."
    )]
    [Min(0f)]
    public float costBiasStrength = 1.0f;

    /// <summary>
    /// Returns an integer cost tier index based on the configured bucket thresholds.
    /// 0 = low, 1 = mid, 2 = high.
    /// </summary>
    public int GetCostTier(int momentumCost)
    {
        if (momentumCost <= lowCostMax)
            return 0;
        if (momentumCost <= midCostMax)
            return 1;
        return 2;
    }
}
