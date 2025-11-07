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
    [Tooltip("For targetCount = ManySelectUpToN")] public int maxTargets = 1;
    [Tooltip("Marks this card as a global effect (no creature targets)")] public bool isGlobal;

    [Header("Actions")]
    [Tooltip("Traits to attach to each targeted creature. Instances are created at play time.")]
    public EffectTraitBase[] traitsToAttachToTargets;
    [Tooltip("Optional global effect to register on play (instanced per use)")]
    public GlobalEffectBase globalEffect;

    [Header("Optional Permanent Stat Delta (applies directly to target creatures on play)")]
    public bool applyPermanentStatDelta;
    public int bodyDelta;
    public int speedDelta;
    [Tooltip("If true, this card may only modify a given creature once for the entire game.")]
    public bool oncePerCreature;

    [Header("Costs & Constraints")]
    [Tooltip("If true, pay 1 food from the global pile when played (block if unavailable)")]
    public bool costOneFood;

    public bool IsValidTarget(Creature candidate, SlotOwner player)
    {
        if (isGlobal || candidate == null || candidate.data == null) return false;
        // Side filter
        switch (targetSide)
        {
            case EffectTargetSide.Ally:
                if (candidate.owner != player) return false; break;
            case EffectTargetSide.Enemy:
                if (candidate.owner == player) return false; break;
            case EffectTargetSide.Any:
                break;
        }
        // Type filter
        switch (targetType)
        {
            case EffectTargetType.Herbivore: if (candidate.data.type != CardType.Herbivore) return false; break;
            case EffectTargetType.Carnivore: if (candidate.data.type != CardType.Carnivore) return false; break;
            case EffectTargetType.Avian: if (candidate.data.type != CardType.Avian) return false; break;
            case EffectTargetType.Any: break;
        }
        return true;
    }
}

public enum EffectTargetSide { Ally, Enemy, Any }
public enum EffectTargetType { Any, Herbivore, Carnivore, Avian }
public enum EffectTargetCount { One, ManySelectUpToN, AllValid }


