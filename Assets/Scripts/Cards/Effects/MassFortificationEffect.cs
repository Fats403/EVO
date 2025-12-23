using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Mass Fortification")]
public class MassFortificationEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Shield, 1);
        remainingRounds = 0;
    }
}
