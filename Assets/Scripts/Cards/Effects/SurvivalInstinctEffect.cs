using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Survival Instinct")]
public class SurvivalInstinctEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.maxHealth += 1;
        // Heal +1 now (after increasing max health)
        self.currentHealth = Mathf.Min(self.maxHealth, self.currentHealth + 1);
        self.AddStatus(StatusTag.Immune, 1);
        self.RefreshStatsUI();
        remainingRounds = 0;
    }
}
