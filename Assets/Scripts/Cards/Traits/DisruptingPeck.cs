using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Disrupting Peck")]
public class DisruptingPeckTrait : Trait
{
    private readonly static HashSet<Creature> usedThisRound = new();

    public override void OnRoundStart(Creature self)
    {
        usedThisRound.Remove(self);
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (target == null) return;
        if (!usedThisRound.Contains(self))
        {
            target.fatigued = true;
            usedThisRound.Add(self);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(traitName)) traitName = "Disrupting Peck";
        if (string.IsNullOrEmpty(description))
        {
            description = "Your first harass each round inflicts Fatigued next round (speed −1).";
        }
    }
}


