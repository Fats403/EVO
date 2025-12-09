using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Extinction Survivor")]
public class ExtinctionSurvivorTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (GameManager.Instance == null)
            return;
        if (GameManager.Instance.currentEra != Era.Extinction)
            return;

        // Get the current body and speed up stacks.
        int currentBodyUp = self.GetStatus(StatusTag.BodyUp);
        int currentSpeedUp = self.GetStatus(StatusTag.SpeedUp);

        // Refresh the Extinction-era buff each round so it stays stable.
        self.ClearStatus(StatusTag.BodyUp);
        self.ClearStatus(StatusTag.SpeedUp);

        // Add the current stacks to the new stacks.
        self.AddStatus(StatusTag.BodyUp, 2 + currentBodyUp);
        self.AddStatus(StatusTag.SpeedUp, 1 + currentSpeedUp);
    }
}
