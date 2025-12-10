using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Nature's Blessing")]
public class NaturesBlessingEffect : EffectTraitBase
{
    public int healAmount = 3;

    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        int before = self.currentHealth;
        self.Heal(Mathf.Max(0, healAmount));
        int healed = self.currentHealth - before;
        if (healed > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"+{healed} HP",
                self.transform.position,
                GameColorPalette.Heal
            );
        }
        remainingRounds = 0;
    }
}
