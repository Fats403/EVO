using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Survival Instinct")]
public class SurvivalInstinctEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.maxHealth += 1;
        self.currentHealth = Mathf.Min(self.maxHealth, self.currentHealth + 2);
        self.AddStatus(StatusTag.Immune, 1);
        self.RefreshStatsUI();
        remainingRounds = 0;
    }
}
