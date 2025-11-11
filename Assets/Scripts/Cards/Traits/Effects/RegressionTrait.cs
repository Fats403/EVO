using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(menuName = "Traits/Effects/Evolutionary Regression")]
public class RegressionTrait : EffectTraitBase
{
    private bool applied = false;
    private List<Trait> backup;

    public override void OnRoundStart(Creature self)
    {
        if (self == null || applied) return;
        backup = new List<Trait>(self.traits);
        self.traits.Clear();
        self.traits.Add(this);
        applied = true;
        self.RefreshStatsUI();
    }

    public override void OnRoundEnd(Creature self)
    {
        if (self != null && applied && backup != null)
        {
            self.traits.Clear();
            foreach (var t in backup) if (t != null && t != this) self.traits.Add(t);
            self.traits.Add(this); // keep this until base removes due to duration
            self.RefreshStatsUI();
        }
        base.OnRoundEnd(self);
    }
}


