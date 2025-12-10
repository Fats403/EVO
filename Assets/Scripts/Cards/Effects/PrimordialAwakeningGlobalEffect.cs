using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Primordial Awakening")]
public class PrimordialAwakeningGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        var gm = GameManager.Instance;
        if (gm == null)
            return;

        // All allies gain +2 Body and +1 Speed permanently.
        var allies = rm.AllCreatures().Where(c => c != null && c.owner == owner).ToList();
        foreach (var c in allies)
        {
            if (c == null || c.data == null)
                continue;
            c.body += 2;
            c.speed += 1;
            c.RefreshStatsUI();
        }

        // If behind on score, draw 1 card (respect hand limit).
        int myScore = owner == SlotOwner.Player1 ? ScoreManager.player1 : ScoreManager.player2;
        int oppScore = owner == SlotOwner.Player1 ? ScoreManager.player2 : ScoreManager.player1;
        if (myScore < oppScore)
        {
            if (owner == SlotOwner.Player1)
            {
                var dm = DeckManager.Instance;
                if (dm != null)
                {
                    int canDraw = Mathf.Max(0, dm.maxHandSize - dm.CurrentHandCount());
                    if (canDraw > 0)
                    {
                        dm.DrawCard();
                        dm.UpdateHandUI();
                    }
                }
            }
            else if (AIManager.Instance != null)
            {
                // Mirror player draw rules for AI: one draw if hand not full.
                AIManager.Instance.TryDrawOneCard();
            }
        }

        remainingRounds = 0;
    }
}
