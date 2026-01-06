using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-shot runtime logic for an effect card that needs to reason
/// about the full set of chosen targets (e.g., swap positions of two allies).
/// Instances are created per play, so they may hold transient state if needed.
/// </summary>
public abstract class RuntimeEffectBase : ScriptableObject
{
    /// <summary>
    /// Optional payload from pre-play choices. Set by EffectsManager before Apply is called.
    /// </summary>
    [HideInInspector]
    public string choicePayload;

    /// <summary>
    /// Apply this effect's custom logic to the chosen targets.
    /// </summary>
    /// <param name="targets">Final resolved list of targets for this effect play.</param>
    /// <param name="owner">Owner of the card that triggered this effect.</param>
    /// <param name="rm">ResolutionManager in the current scene.</param>
    public abstract void Apply(List<Creature> targets, SlotOwner owner, ResolutionManager rm);
}
