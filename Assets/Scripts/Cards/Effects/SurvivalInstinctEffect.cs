using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Survival Instinct")]
public class SurvivalInstinctEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.maxHealth += 2;
        self.currentHealth = Mathf.Min(self.maxHealth, self.currentHealth + 2);
        self.ApplyImmune(1);
        self.RefreshStatsUI();
        remainingRounds = 0;
    }
}
