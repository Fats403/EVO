using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/First Blood")]
public class FirstBloodTrait : Trait
{
    // First Blood: First attack each round deals reduced damage (1) but applies Bleed.
    // We override final damage to 1, then apply Bleed after the attack resolves.
    public override bool TryOverrideFinalDamage(Creature self, Creature target, out int fixedDamage)
    {
        if (self == null || target == null)
        {
            fixedDamage = 0;
            return false;
        }
        if (self.HasStatus(StatusTag.Suppress))
        {
            fixedDamage = 0;
            return false;
        }

        // Always deal 1 damage, plus the Bleed applied after.
        fixedDamage = 1;
        return true;
    }

    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (wasNegated)
            return;

        // Attribute Bleed to this creature so its ticks contribute to
        // this creature's roundDamageDealt scoring.
        target.AddStatus(StatusTag.Bleed, 1, self);
        FeedbackManager.Instance?.ShowFloatingText(
            "First Blood",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Bleed +1",
            target.transform.position,
            GameColorPalette.Bleed
        );
    }
}
