using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/First Blood")]
public class FirstBloodTrait : Trait
{
    // TODO: This need to get modified into a different hook cause it does dmg + bleed!
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppressed))
            return baseDamage;
        // Ensure the attack attempts at least 1 damage before shields/immune/absorb.
        return Mathf.Max(baseDamage, 1);
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;

        target.AddStatus(StatusTag.Bleeding, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Bleeding +1",
            target.transform.position,
            new Color(1f, 0.3f, 0.3f)
        );
    }
}
