using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Titan's Might")]
public class TitansMightEffect : EffectTraitBase
{
    private int appliedBonus = 0;

    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        // Snapshot current body and directly double it.
        appliedBonus = Mathf.Max(0, self.body);
        if (appliedBonus > 0)
        {
            self.body += appliedBonus; // body is now doubled
            self.RefreshStatsUI();
        }
    }

    public override void OnRoundEnd(Creature self)
    {
        if (self != null)
        {
            // Remove exactly the amount we added so body returns to its original value.
            if (appliedBonus > 0)
            {
                self.body = Mathf.Max(0, self.body - appliedBonus);
                self.RefreshStatsUI();
            }
        }
        base.OnRoundEnd(self);
    }
}
