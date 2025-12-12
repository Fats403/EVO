using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Lay In Wake")]
public class LayInWakeTrait : Trait
{
    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;
        if (GameManager.Instance == null)
            return 0;

        var era = GameManager.Instance.currentEra;
        if (era != Era.Cretaceous && era != Era.Extinction)
            return 0;

        return 3;
    }

    public override int BodyBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;
        if (GameManager.Instance == null)
            return 0;

        var era = GameManager.Instance.currentEra;
        if (era != Era.Cretaceous && era != Era.Extinction)
            return 0;

        return 3;
    }
}
