using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Lay In Wake")]
public class LayInWakeTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (GameManager.Instance == null)
            return;

        var era = GameManager.Instance.currentEra;
        if (era != Era.Cretaceous && era != Era.Extinction)
            return;

        int currentBodyUp = self.GetStatus(StatusTag.BodyUp);
        int currentSpeedUp = self.GetStatus(StatusTag.SpeedUp);

        // Refresh era-based buff each round while in Cretaceous/Extinction.
        self.ClearStatus(StatusTag.BodyUp);
        self.ClearStatus(StatusTag.SpeedUp);

        self.AddStatus(StatusTag.SpeedUp, 2 + currentSpeedUp);
        self.AddStatus(StatusTag.BodyUp, 1 + currentBodyUp);
    }
}
