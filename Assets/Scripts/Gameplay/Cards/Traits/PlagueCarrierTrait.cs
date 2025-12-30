using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Plague Carrier")]
public class PlagueCarrierTrait : Trait
{
    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        // Apply only on successful attacks (not negated)
        if (wasNegated)
            return;
        // Attribute Infection stacks to this creature so ticks contribute
        // to its roundDamageDealt scoring.
        target.AddStatus(StatusTag.Infection, 1, self);
        target.AddStatus(StatusTag.NoForage, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Plague Carrier",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Infected +1",
            target.transform.position,
            GameColorPalette.TextDoTPoison
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "No Forage",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
