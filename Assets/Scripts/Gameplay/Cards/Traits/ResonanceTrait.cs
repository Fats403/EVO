using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Resonance")]
public class ResonanceTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppress))
            return baseDamage;

        // If target already has Suppress, deal +1 damage instead.
        if (target.GetStatus(StatusTag.Suppress) > 0)
        {
            return Mathf.Max(0, baseDamage + 1);
        }
        return baseDamage;
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;

        // All attacks apply Suppress (1).
        target.AddStatus(StatusTag.Suppress, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Resonance",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Suppress +1",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
