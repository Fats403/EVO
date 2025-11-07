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
            target.ApplyFatigued(1);
            FeedbackManager.Instance?.ShowFloatingText($"Fatigued [{traitName}]", target.transform.position, Color.yellow);
            usedThisRound.Add(self);
        }
    }
}


