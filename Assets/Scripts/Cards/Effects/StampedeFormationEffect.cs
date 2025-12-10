using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Stampede Formation")]
public class StampedeFormationEffect : EffectTraitBase
{
    public override void OnApply(Creature self)
    {
        if (self == null || self.data == null)
            return;
        if (self.data.type != CardType.Herbivore)
            return;

        // +2 Body this round via BodyUp and allow this herbivore to attack.
        self.AddStatus(StatusTag.BodyUp, 2);
    }

    public override bool CanAttack(Creature self)
    {
        if (self == null || self.data == null)
            return false;
        if (self.data.type != CardType.Herbivore)
            return base.CanAttack(self);
        if (self.HasStatus(StatusTag.Suppressed))
            return base.CanAttack(self);
        return true;
    }

    public override void OnRoundEnd(Creature self)
    {
        if (self != null)
        {
            self.ClearStatus(StatusTag.BodyUp);
        }
        base.OnRoundEnd(self);
    }
}

