using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Reckless Charge")]
public class RecklessChargeTrait : Trait
{
    // Always allow this creature to consider any enemy as a potential target. This lets
    // Reckless Charge ignore the normal "don't target carnivores" and body-size
    // restrictions, while still respecting Stealth, Taunt, and the Carnivore-vs-Avian
    // speed gate enforced in ResolutionManager.
    public override bool CanTargetAny(Creature self)
    {
        return true;
    }

    // Force the attacker to pick the closest valid target from the candidate list. This
    // runs after ResolutionManager has filtered out illegal targets, so it will always
    // "charge straight at" the nearest enemy it is allowed to hit.
    public override Creature ChooseAttackTarget(
        Creature self,
        System.Collections.Generic.IEnumerable<Creature> candidates,
        Creature defaultTarget
    )
    {
        if (candidates == null)
            return defaultTarget;
        Creature best = null;
        float bestDistSq = float.MaxValue;
        foreach (var c in candidates)
        {
            if (c == null || self == null)
                continue;
            float d = (c.transform.position - self.transform.position).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = c;
            }
        }
        return best ?? defaultTarget;
    }

    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        // Recoil only on successful (non-negated) attacks.
        if (wasNegated)
            return;
        int applied = self.ApplyDamage(1, self);
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP [Recoil]",
                self.transform.position,
                GameColorPalette.Damage
            );
        }
    }
}
