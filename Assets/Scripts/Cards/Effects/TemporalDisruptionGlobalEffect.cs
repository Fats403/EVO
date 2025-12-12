using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Temporal Disruption")]
public class TemporalDisruptionGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (GameManager.Instance == null)
            return;

        var gm = GameManager.Instance;
        SlotOwner opponent = owner == SlotOwner.Player1 ? SlotOwner.Player2 : SlotOwner.Player1;

        int myScore = owner == SlotOwner.Player1 ? ScoreManager.player1 : ScoreManager.player2;
        int oppScore = opponent == SlotOwner.Player1 ? ScoreManager.player1 : ScoreManager.player2;

        // If behind on score, set opponent momentum to 0 this round.
        if (myScore < oppScore)
        {
            int current = gm.GetMomentum(opponent);
            if (current > 0)
            {
                gm.TrySpendMomentum(opponent, current);
            }
        }
        else
        {
            // Otherwise, reduce opponent's momentum by 3, not below 0.
            int current = gm.GetMomentum(opponent);
            int reduceBy = Mathf.Min(3, Mathf.Max(0, current));
            if (reduceBy > 0)
            {
                gm.TrySpendMomentum(opponent, reduceBy);
            }
        }

        remainingRounds = 0;
    }
}



