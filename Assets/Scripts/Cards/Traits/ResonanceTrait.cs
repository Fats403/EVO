using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Resonance")]
public class ResonanceTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppressed))
            return baseDamage;

        // If target already has Suppressed, deal +1 damage instead.
        if (target.GetStatus(StatusTag.Suppressed) > 0)
        {
            return Mathf.Max(0, baseDamage + 1);
        }
        return baseDamage;
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;

        // All attacks apply Suppressed (1).
        target.AddStatus(StatusTag.Suppressed, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Suppressed",
            target.transform.position,
            Color.yellow
        );
    }
}
