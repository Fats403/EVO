using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Rallying Call")]
public class RallyingCallTrait : Trait
{
    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        // All allied avians: +2 Regen and remove all Fatigued
        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c =>
                c != null
                && c.currentHealth > 0
                && !c.isDying
                && c.owner == self.owner
                && c.data != null
                && c.data.type == CardType.Avian
            )
            .ToList();
        foreach (var ally in allies)
        {
            ally.AddStatus(StatusTag.Regen, 2);
            int f = ally.GetStatus(StatusTag.Fatigued);
            if (f > 0)
                ally.ClearStatus(StatusTag.Fatigued);
        }
    }
}
