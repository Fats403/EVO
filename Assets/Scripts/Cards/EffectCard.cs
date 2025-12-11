using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Effect Card")]
public class EffectCard : ScriptableObject
{
    [Header("Identity")]
    public string effectName;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Targeting")]
    public EffectTargetSide targetSide = EffectTargetSide.Any;
    public EffectTargetType targetType = EffectTargetType.Any;
    public EffectTargetCount targetCount = EffectTargetCount.One;

    [Tooltip("For targetCount = ManySelectUpToN")]
    public int maxTargets = 1;

    [Tooltip("Marks this card as a global effect (no creature targets)")]
    public bool isGlobal;

    [Header("Manual Selection")]
    [Tooltip(
        "If true, this card is played and then the player must click targets manually using targetCount/maxTargets instead of auto-selecting on drag release."
    )]
    public bool requiresManualSelection = false;

    [Header("Multi-Select (UI)")]
    [Tooltip("If true, the UI treats this as a multi-select and uses maxTargets.")]
    public bool multiSelect = false;

    [Header("Actions")]
    [Tooltip("Traits to attach to each targeted creature. Instances are created at play time.")]
    public EffectTraitBase[] traitsToAttachToTargets;

    [Tooltip("Optional global effect to register on play (instanced per use)")]
    public GlobalEffectBase globalEffect;

    [Tooltip(
        "Optional bespoke runtime logic that runs once on the final chosen target set (e.g., swapping positions)."
    )]
    public RuntimeEffectBase runtimeEffect;

    [Tooltip(
        "If true, EffectsManager will NOT play the default hit-bounce animation on targets when this card resolves."
    )]
    public bool suppressHitBounce = false;

    [Header("Cost & Conditions")]
    [Tooltip("Momentum cost to play this effect card")]
    [Min(1)]
    public int momentumCost = 1;

    [Tooltip("Minimum era in which this card can be played")]
    public Era minEraAllowed = Era.Triassic;

    [Header("Weather Requirements")]
    [Tooltip(
        "If none are checked, this card can be played in any weather. If one or more are checked, the card can only be played while the weather matches one of the selected types."
    )]
    public bool allowInClear = false;
    public bool allowInDrought = false;
    public bool allowInStorm = false;
    public bool allowInWildfire = false;

    [Header("AI Evaluation (Optional)")]
    [Tooltip(
        "If true, this global effect is preferred when the AI is behind on board (fewer creatures)."
    )]
    public bool aiPreferWhenBehindOnBoard = false;

    [Tooltip(
        "Minimum number of allied creatures the AI wants before using this global buff (0 = no requirement)."
    )]
    [Min(0)]
    public int aiMinAlliesForBuffGlobals = 0;

    [Tooltip(
        "How much this effect gains value from cleansing/removing negative statuses (0 = ignore)."
    )]
    [Range(0f, 5f)]
    public float aiCleanseSynergy = 0f;

    [Tooltip(
        "How much this effect gains value from giving an ally an immediate attack payoff (e.g., Rage)."
    )]
    [Range(0f, 5f)]
    public float aiAttackSynergy = 0f;

    [Tooltip(
        "Approximate multiplier to how much this effect improves an ally's effective body (1 = neutral, >1 = stronger body buff)."
    )]
    [Range(1f, 3f)]
    public float aiBodyBuffMultiplier = 1f;

    [Tooltip("Extra value when this effect removes/disables threats")]
    [Range(0f, 5f)]
    public float aiRemovalValue = 0f;

    public bool IsValidTarget(Creature candidate, SlotOwner player)
    {
        if (isGlobal || candidate == null || candidate.data == null)
            return false;
        // Side filter
        switch (targetSide)
        {
            case EffectTargetSide.Ally:
                if (candidate.owner != player)
                    return false;
                break;
            case EffectTargetSide.Enemy:
                if (candidate.owner == player)
                    return false;
                break;
            case EffectTargetSide.Any:
                break;
        }
        // Type filter
        switch (targetType)
        {
            case EffectTargetType.Herbivore:
                if (candidate.data.type != CardType.Herbivore)
                    return false;
                break;
            case EffectTargetType.Carnivore:
                if (candidate.data.type != CardType.Carnivore)
                    return false;
                break;
            case EffectTargetType.Avian:
                if (candidate.data.type != CardType.Avian)
                    return false;
                break;
            case EffectTargetType.Any:
                break;
        }
        return true;
    }
}

public enum EffectTargetSide
{
    Ally,
    Enemy,
    Any,
}

public enum EffectTargetType
{
    Any,
    Herbivore,
    Carnivore,
    Avian,
}

public enum EffectTargetCount
{
    One,
    ManySelectUpToN,
    AllValid,
}
