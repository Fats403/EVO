using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Stealth Veil")]
public class StealthVeilEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Stealth, 1);
        remainingRounds = 0;
    }
}







