using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Retaliate")]
public class RetaliateTrait : Trait
{
    public override void OnTargetedByAttack(Creature self, Creature attacker)
    {
        if (self == null || attacker == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        int applied = attacker.ApplyDamage(1, self);
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP [Retaliate]",
                attacker.transform.position,
                GameColorPalette.Damage
            );
        }
    }
}
