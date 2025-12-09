using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Plague Carrier")]
public class PlagueCarrierTrait : Trait
{
    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        // Apply only on successful attacks (not negated)
        if (wasNegated)
            return;
        target.AddStatus(StatusTag.Infected, 1);
        target.AddStatus(StatusTag.NoForage, 1);
    }
}
