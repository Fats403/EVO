using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Sudden Death")]
public class SuddenDeathGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;
        var all = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .ToList();
        var p1 = all.Where(c => c.owner == SlotOwner.Player1).ToList();
        var p2 = all.Where(c => c.owner == SlotOwner.Player2).ToList();
        if (p1.Count > 0)
        {
            int i =
                (GameManager.Instance != null)
                    ? GameManager.Instance.NextRandomInt(0, p1.Count)
                    : Random.Range(0, p1.Count);
            var pick = p1[i];
            if (pick != null)
                pick.Kill("Sudden Death");
        }
        if (p2.Count > 0)
        {
            int i =
                (GameManager.Instance != null)
                    ? GameManager.Instance.NextRandomInt(0, p2.Count)
                    : Random.Range(0, p2.Count);
            var pick = p2[i];
            if (pick != null)
                pick.Kill("Sudden Death");
        }
        remainingRounds = 0;
    }
}
