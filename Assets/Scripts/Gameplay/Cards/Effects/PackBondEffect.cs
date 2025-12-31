using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Pack Bond")]
public class PackBondEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Bulk, 1);
        self.AddStatus(StatusTag.Haste, 1);
        remainingRounds = 0;
    }
}
