using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Cleansing Rain")]
public class CleansingRainEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;

        int before = self.currentHealth;

        self.Heal(1);

        int healed = Mathf.Max(0, before - self.currentHealth);
        if (healed > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"+{healed} HP",
                self.transform.position,
                GameColorPalette.Heal
            );
        }

        self.ClearAllNegativeStatuses();
        remainingRounds = 0;
    }
}
