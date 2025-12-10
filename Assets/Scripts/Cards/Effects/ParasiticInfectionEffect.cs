using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Parasitic Infection")]
public class ParasiticInfectionEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        // Deal 1 damage at the start of each round for 2 rounds.
        remainingRounds = 2;
    }

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        int applied = self.ApplyDamage(1, null, null, "Parasitic Infection");
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP (Parasitic Infection)",
                self.transform.position,
                GameColorPalette.Poison
            );
        }
    }
}
