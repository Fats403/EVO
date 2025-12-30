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

        // Visual feedback for all buffed allies, unless the source card suppressed it.
        if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(allies);
        }

        // If behind on score, draw 1 card (respect hand limit).
        int myScore = owner == SlotOwner.Player1 ? ScoreManager.player1 : ScoreManager.player2;
        int oppScore = owner == SlotOwner.Player1 ? ScoreManager.player2 : ScoreManager.player1;
        if (myScore < oppScore)
        {
            // Only the local player draws cards. In networked games, the remote player
            // handles their own draw on their client.
            if (NetworkRoleHelper.IsLocalPlayer(owner))
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
            else if (!NetworkSessionStore.IsNetworkedGame && AIManager.Instance != null)
            {
                // AI mode only: mirror player draw rules for AI.
                AIManager.Instance.TryDrawOneCard();
            }
            // In networked games, the remote player's client handles their own draw.
        }

        remainingRounds = 0;
    }
}
