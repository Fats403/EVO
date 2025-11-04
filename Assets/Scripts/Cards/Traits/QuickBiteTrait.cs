using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Disrupting Peck")]
public class QuickBiteTrait : Trait
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
            target.ApplyFatigue(1, false);
            FeedbackManager.Instance?.ShowFloatingText($"Fatigued [{traitName}]", target.transform.position, Color.yellow);
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


