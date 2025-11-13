using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Genetic Mutation")]
public class GeneticMutationEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null)
            return;
        self.body += 1;
        self.speed += 1;
        self.RefreshStatsUI();
        remainingRounds = 0;
    }
}
