using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Apex Ascension")]
public class ApexAscensionEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.maxHealth += 2;
        self.currentHealth = Mathf.Min(self.maxHealth, self.currentHealth + 2);
        self.body += 1;
        self.speed += 1;
        self.ClearAllNegativeStatuses();
        self.RefreshStatsUI();
        remainingRounds = 0;
    }
}
