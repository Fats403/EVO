using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Regeneration Pulse")]
public class RegenerationPulseRuntimeEffect : RuntimeEffectBase
{
    public int healAmount = 2;

    public override void Apply(List<Creature> targets, SlotOwner owner, ResolutionManager rm)
    {
        if (targets == null)
            return;
        foreach (var self in targets)
        {
            if (self == null)
                continue;
            int before = self.currentHealth;
            self.Heal(Mathf.Max(0, healAmount));
            int healed = self.currentHealth - before;
            if (healed > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"+{healed} HP",
                    self.transform.position,
                    new Color(0.3f, 1f, 0.3f)
                );
            }
        }
    }
}
