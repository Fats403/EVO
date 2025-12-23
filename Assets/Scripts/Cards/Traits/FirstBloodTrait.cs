using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/First Blood")]
public class FirstBloodTrait : Trait
{
    // First Blood: Instead of dealing normal damage, this attack causes Bleeding.
    // We override final damage to 0, then apply Bleed after the attack resolves.
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

        // No HP loss from this hit; all value comes from the Bleeding it applies.
        fixedDamage = 0;
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

        target.AddStatus(StatusTag.Bleed, 1);
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
