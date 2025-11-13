using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Reflective Scales")]
public class ReflectiveScalesEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.AddStatus(StatusTag.Reflect, 1);
        remainingRounds = 0;
    }
}
