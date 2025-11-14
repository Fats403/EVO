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

    [Header("Multi-Select (UI)")]
    [Tooltip("If true, the UI treats this as a multi-select and uses maxTargets.")]
    public bool multiSelect = false;

    [Header("Actions")]
    [Tooltip("Traits to attach to each targeted creature. Instances are created at play time.")]
    public EffectTraitBase[] traitsToAttachToTargets;

    [Tooltip("Optional global effect to register on play (instanced per use)")]
    public GlobalEffectBase globalEffect;

    // Removed unused permanent stat delta and cost fields

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
