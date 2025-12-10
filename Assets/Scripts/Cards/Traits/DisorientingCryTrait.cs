using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Disorienting Cry")]
public class DisorientingCryTrait : Trait
{
    public override bool TryOverrideFinalDamage(Creature self, Creature target, out int fixedDamage)
    {
        fixedDamage = 0;
        if (self == null || target == null)
            return false;
        if (self.HasStatus(StatusTag.Suppressed))
            return false;
        // Instead of damage, this attack only stuns.
        return true;
    }

    public override void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (wasNegated)
            return;

        target.AddStatus(StatusTag.Stunned, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Stunned",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
