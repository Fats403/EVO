using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Radiant Scales")]
public class RadiantScalesTrait : Trait
{
    [Tooltip("Minimum damage required to trigger Reflect.")]
    public int damageThreshold = 2;

    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (finalDamage < damageThreshold)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        // Immediately grant Reflect after taking threshold damage
        self.AddStatus(StatusTag.Reflect, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Radiant Scales",
            self.transform.position,
            GameColorPalette.Shield
        );
    }
}
