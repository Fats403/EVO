using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Rabies")]
public class RabiesEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Infection, 2);
        self.AddStatus(StatusTag.NoForage, 1);
        remainingRounds = 0;
    }
}
