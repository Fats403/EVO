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

        // +2 Body this round via Bulk and allow this herbivore to attack.
        self.AddStatus(StatusTag.Bulk, 2);
    }

    public override bool CanAttack(Creature self)
    {
        if (self == null || self.data == null)
            return false;
        if (self.data.type != CardType.Herbivore)
            return base.CanAttack(self);
        if (self.HasStatus(StatusTag.Suppress))
            return base.CanAttack(self);
        return true;
    }

    public override bool AllowBonusAttackAfterForage(Creature self)
    {
        // Stampede: herbivores trample after feeding.
        if (self == null || self.data == null)
            return false;
        if (self.data.type != CardType.Herbivore)
            return false;
        if (self.HasStatus(StatusTag.Suppress))
            return false;
        return true;
    }

    public override void OnRoundEnd(Creature self)
    {
        self?.ClearStatus(StatusTag.Bulk);
        base.OnRoundEnd(self);
    }
}
