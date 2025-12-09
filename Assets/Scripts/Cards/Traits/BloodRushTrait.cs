using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Blood Rush")]
public class BloodRushTrait : Trait
{
    private static readonly HashSet<Creature> grantNextRound = new HashSet<Creature>();

    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // Immediate benefit: heal now.
        self.AddStatus(StatusTag.Regen, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Regen +2",
            self.transform.position,
            new Color(0.3f, 1f, 0.3f)
        );

        // Flag to gain DamageUp at the start of the next round.
        grantNextRound.Add(self);
        FeedbackManager.Instance?.ShowFloatingText(
            "DamageUp (next)",
            self.transform.position,
            new Color(1f, 0.7f, 0.3f)
        );
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (!grantNextRound.Contains(self))
            return;

        grantNextRound.Remove(self);
        self.AddStatus(StatusTag.DamageUp, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "DamageUp +1",
            self.transform.position,
            new Color(1f, 0.7f, 0.3f)
        );
    }
}
