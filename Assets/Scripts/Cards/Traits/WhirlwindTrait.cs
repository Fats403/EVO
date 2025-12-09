using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Whirlwind")]
public class WhirlwindTrait : Trait
{
    private static readonly HashSet<Creature> usedThisRound = new HashSet<Creature>();

    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
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
        if (next == null || next == target)
            return;

        usedThisRound.Add(self);
        ResolutionManager.Instance.PerformImmediateAttack(self, next, ignoreBodyRules: false);
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        usedThisRound.Remove(self);
    }
}
