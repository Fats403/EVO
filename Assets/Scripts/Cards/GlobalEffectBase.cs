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

    public virtual void OnPlay(ResolutionManager rm) { }

    public virtual void OnRoundStart(ResolutionManager rm) { }

    public virtual void OnPreHerbivore(ResolutionManager rm) { }

    public virtual void OnHerbivores(ResolutionManager rm) { }

    public virtual void OnForaging(ResolutionManager rm) { }

    public virtual void OnRoundEnd(ResolutionManager rm) { }
}
