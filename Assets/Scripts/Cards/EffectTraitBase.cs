using UnityEngine;

public abstract class EffectTraitBase : Trait
{
    [SerializeField] public int remainingRounds = 1;

    public override void OnRoundEnd(Creature self)
    {
        remainingRounds = Mathf.Max(0, remainingRounds - 1);
        if (remainingRounds == 0 && self != null && self.traits != null)
        {
            self.traits.Remove(this);
            self.RefreshStatsUI();
        }
    }
}


