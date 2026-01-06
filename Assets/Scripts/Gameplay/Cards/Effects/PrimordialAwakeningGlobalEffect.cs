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

        // All allies gain +2 Body (Bulk) and +1 Speed (Haste) this turn
        var allies = DeterministicHelpers.GetCreaturesSorted(c => c.owner == owner);
        foreach (var c in allies)
        {
            if (c == null || c.data == null)
                continue;
            c.AddStatus(StatusTag.Bulk, 2);
            c.AddStatus(StatusTag.Haste, 1);
        }

        // Visual feedback for all buffed allies, unless the source card suppressed it.
        if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(allies);
        }

        // If behind on score, draw 1 card (respect hand limit).
        int myScore = ScoreManager.GetScore(owner);
        int oppScore = ScoreManager.GetScore(NetworkRoleHelper.RemoteRole);
        if (myScore < oppScore)
        {
            // Only the local player draws cards via DeckManager. In networked games, the
            // remote client mirrors this via OpponentDeckTracker so HUD stays in sync.
            if (NetworkRoleHelper.IsLocalPlayer(owner))
            {
                var dm = DeckManager.Instance;
                if (dm != null)
                {
                    int canDraw = Mathf.Max(0, GameRules.MaxHandSize - dm.CurrentHandCount());
                    if (canDraw > 0)
                    {
                        dm.DrawCard();
                        dm.UpdateHandUI();
                    }
                }
            }
            // Networked remote opponent: only update tracker counts for HUD.
            else if (NetworkSessionStore.IsNetworkedGame && OpponentDeckTracker.Instance != null)
            {
                OpponentDeckTracker.Instance.OnOpponentDrew(1);
            }
            // Offline AI: mirror draw behavior using AIManager.
            else if (!NetworkSessionStore.IsNetworkedGame && AIManager.Instance != null)
            {
                AIManager.Instance.TryDrawOneCard();
            }
        }

        remainingRounds = 0;
    }
}
