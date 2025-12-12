using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Blood Rush")]
public class BloodRushTrait : Trait
{
    private static readonly HashSet<Creature> grantNextRound = new();

    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // Immediate benefit: heal now.
        self.Heal(2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Blood Rush",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "+2 HP",
            self.transform.position,
            GameColorPalette.Heal
        );

        // Flag to gain DamageUp at the start of the next round.
        grantNextRound.Add(self);
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
            "Blood Rush",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "DamageUp +1",
            self.transform.position,
            GameColorPalette.Rage
        );
    }
}
