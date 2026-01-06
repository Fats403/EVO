using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inline choice definition for pre-play choices.
/// Define these directly on the EffectCard - no need for separate assets.
/// </summary>
[Serializable]
public class PrePlayChoiceDefinition
{
    [Tooltip(
        "Unique identifier for this choice (e.g., 'fight', 'flight'). Used by the effect to determine which option was picked."
    )]
    public string optionId;

    [Tooltip("Display title for this choice.")]
    public string title;

    [Tooltip("Description explaining what this choice does.")]
    [TextArea(1, 3)]
    public string description;

    [Tooltip("Icon type to display for this choice.")]
    public VirtualChoiceIconType iconType = VirtualChoiceIconType.Default;

    /// <summary>
    /// Converts this inline definition to a VirtualChoiceOption for use with CardChoiceManager.
    /// </summary>
    public VirtualChoiceOption ToVirtualChoiceOption()
    {
        return VirtualChoiceOption.Create(title, description, iconType, optionId: optionId);
    }
}

[CreateAssetMenu(menuName = "Cards/Effect Card")]
public class EffectCard : CardDefinition
{
    [Header("Identity")]
    public string effectName;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Pre-Play Choices")]
    [Tooltip(
        "If populated, player must choose one of these options before the card is played. Define choices inline - no separate assets needed."
    )]
    public List<PrePlayChoiceDefinition> prePlayChoices;

    /// <summary>
    /// Returns true if this effect requires the player to make a choice before playing.
    /// </summary>
    public bool RequiresPrePlayChoice => prePlayChoices != null && prePlayChoices.Count > 0;

    [Header("Targeting")]
    public EffectTargetSide targetSide = EffectTargetSide.Any;
    public EffectTargetType targetType = EffectTargetType.Any;
    public EffectTargetCount targetCount = EffectTargetCount.One;

    [Tooltip("For targetCount = ManySelectUpToN")]
    public int maxTargets = 1;

    [Tooltip("Marks this card as a global effect (no creature targets)")]
    public bool isGlobal;

    [Header("Runtime Effect Manual Selection")]
    [Tooltip(
        "If true, this card is played and then the player must click targets manually using targetCount/maxTargets instead of auto-selecting on drag release."
    )]
    public bool requiresManualSelection = false;

    [Tooltip(
        "For manual ManySelectUpToN effects: minimum number of targets required before the player can confirm. If 0, a sensible default is chosen (1 when allowFewerThanMax is true, otherwise maxTargets)."
    )]
    [Min(0)]
    public int minTargets = 0;

    [Tooltip(
        "For manual ManySelectUpToN effects: if true, the player may confirm once they have selected at least minTargets (up to maxTargets). If false, they must select exactly maxTargets."
    )]
    public bool allowFewerThanMax = false;

    [Header("For multi hover select behaviour (UI)")]
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
        // Stealth filter: you may still target your own stealthed allies,
        // but enemy stealthed creatures cannot be directly targeted by effects.
        if (candidate.HasStatus(StatusTag.Stealth) && candidate.owner != player)
            return false;
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

    // CardDefinition implementation
    public override string DisplayName => effectName;
    public override Sprite Artwork => icon;
    public override int MomentumCost => momentumCost;
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
