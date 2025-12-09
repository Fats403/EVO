using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Bloodthirsty")]
public class BloodthirstyTrait : Trait
{
    private static readonly System.Collections.Generic.HashSet<Creature> usedThisRound =
        new System.Collections.Generic.HashSet<Creature>();

    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (usedThisRound.Contains(self))
            return;
        if (ResolutionManager.Instance == null)
            return;
        var next = ResolutionManager.Instance.FindBestTarget(self);
        if (next == null)
            return;
        // If the killed target is already gone, we just attack the next best available
        if (next == target)
            return; // avoid pointless call if somehow still same reference
        ResolutionManager.Instance.PerformImmediateAttack(self, next, ignoreBodyRules: false);
        usedThisRound.Add(self);
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        // Reset per-round extra-attack flag for this creature.
        usedThisRound.Remove(self);
    }
}
