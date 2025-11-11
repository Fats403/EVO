using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Effects/Feral Rage")]
public class FeralRageTrait : EffectTraitBase
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        return Mathf.Max(0, baseDamage * 2);
    }

    public override void CollectStatusTags(Creature self, List<StatusTag> into)
    {
        if (into == null) return;
        into.Add(StatusTag.Rage);
    }
}


