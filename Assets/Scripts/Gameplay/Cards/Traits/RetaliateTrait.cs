using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Retaliate")]
public class RetaliateTrait : Trait
{
    public override void OnTargetedByAttack(Creature self, Creature attacker)
    {
        if (self == null || attacker == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        int applied = attacker.ApplyDamage(1, self);
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Retaliate",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP",
                attacker.transform.position,
                GameColorPalette.Damage
            );
        }
    }
}
