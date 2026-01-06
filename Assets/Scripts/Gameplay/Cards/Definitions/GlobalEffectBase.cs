using UnityEngine;

public abstract class GlobalEffectBase : ScriptableObject
{
    [Header("Global Effect")]
    public string effectName;
    public int remainingRounds = 1;

    [HideInInspector]
    public bool suppressHitBounceFromSource = false;

    // Owner of the effect card that spawned this global effect.
    public SlotOwner owner;

    /// <summary>
    /// Reference to the source EffectCard that created this global effect.
    /// Set by EffectsManager when instantiating. Useful for refund logic.
    /// </summary>
    [HideInInspector]
    public EffectCard sourceEffectCard;

    /// <summary>
    /// The choice payload from the GameAction, if the effect has pre-play choices.
    /// Contains the optionId of the selected VirtualChoiceOption.
    /// Set by EffectsManager when instantiating.
    /// </summary>
    [HideInInspector]
    public string choicePayload;

    public virtual void OnPlay(ResolutionManager rm) { }

    public virtual void OnRoundStart(ResolutionManager rm) { }

    public virtual void OnPreHerbivore(ResolutionManager rm) { }

    public virtual void OnHerbivores(ResolutionManager rm) { }

    public virtual void OnForaging(ResolutionManager rm) { }

    public virtual void OnRoundEnd(ResolutionManager rm) { }
}
