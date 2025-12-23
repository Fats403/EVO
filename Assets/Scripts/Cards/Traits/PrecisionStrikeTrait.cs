using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Precision Strike")]
public class PrecisionStrikeTrait : Trait
{
    public override bool CanTargetAny(Creature self)
    {
        if (self == null)
            return false;
        if (self.HasStatus(StatusTag.Suppress))
            return false;
        return true;
    }

    public override bool TryOverrideFinalDamage(Creature self, Creature target, out int fixedDamage)
    {
        fixedDamage = 1;
        return true;
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        target.AddStatus(StatusTag.Stun, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Precision Strike",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Stunned",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
