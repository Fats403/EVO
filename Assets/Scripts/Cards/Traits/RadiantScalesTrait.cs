using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Radiant Scales")]
public class RadiantScalesTrait : Trait
{
    private static readonly HashSet<Creature> grantNextRound = new HashSet<Creature>();

    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (finalDamage <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // If this takes 2+ damage in a single attack, flag Reflect for next round.
        if (finalDamage >= 2)
        {
            grantNextRound.Add(self);
        }
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (grantNextRound.Contains(self))
        {
            self.AddStatus(StatusTag.Reflect, 1);
            grantNextRound.Remove(self);
            FeedbackManager.Instance?.ShowFloatingText(
                "Reflect",
                self.transform.position,
                Color.cyan
            );
        }
    }
}

