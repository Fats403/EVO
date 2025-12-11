using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Extinction Survivor")]
public class ExtinctionSurvivorTrait : Trait
{
    public override int BodyBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;
        if (GameManager.Instance == null)
            return 0;
        if (GameManager.Instance.currentEra != Era.Extinction)
            return 0;

        return 2;
    }

    public override int SpeedBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppressed))
            return 0;
        if (GameManager.Instance == null)
            return 0;
        if (GameManager.Instance.currentEra != Era.Extinction)
            return 0;

        return 1;
    }
}
