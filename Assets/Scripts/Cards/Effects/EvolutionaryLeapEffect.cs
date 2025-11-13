using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Evolutionary Leap")]
public class EvolutionaryLeapEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.speed += 1;
        self.ClearStatus(StatusTag.Fatigued);
        self.RefreshStatsUI();
        remainingRounds = 0;
    }
}
