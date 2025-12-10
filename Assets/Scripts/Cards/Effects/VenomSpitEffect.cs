using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Venom Spit")]
public class VenomSpitEffect : EffectTraitBase
{
    public int damage = 2;

    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        int applied = self.ApplyDamage(Mathf.Max(0, damage), null, null, "Venom Spit");
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP",
                self.transform.position,
                GameColorPalette.Damage
            );
        }
        remainingRounds = 0;
    }
}
