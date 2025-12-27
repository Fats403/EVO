using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Adaptive Camouflage")]
public class AdaptiveCamouflageRuntimeEffect : RuntimeEffectBase
{
    public override void Apply(List<Creature> targets, SlotOwner owner, ResolutionManager rm)
    {
        if (targets == null)
            return;

        foreach (var self in targets)
        {
            if (self == null)
                continue;
            self.AddStatus(StatusTag.Stealth, 1);
        }
    }
}
