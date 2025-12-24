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
            int i = 0;
            if (GameManager.Instance != null)
            {
                i = GameManager.Instance.NextRandomInt(0, p1.Count);
            }
            else
            {
                Debug.LogWarning("SuddenDeathGlobalEffect: GameManager.Instance is null. Determinism may be compromised.");
                i = Random.Range(0, p1.Count);
            }
            var pick = p1[i];
            pick?.Kill("Sudden Death");
        }
        if (p2.Count > 0)
        {
            int i = 0;
            if (GameManager.Instance != null)
            {
                i = GameManager.Instance.NextRandomInt(0, p2.Count);
            }
            else
            {
                Debug.LogWarning("SuddenDeathGlobalEffect: GameManager.Instance is null. Determinism may be compromised.");
                i = Random.Range(0, p2.Count);
            }
            var pick = p2[i];
            if (pick != null)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    "Sudden Death",
                    pick.transform.position,
                    GameColorPalette.Damage
                );
                pick.Kill("Sudden Death");
            }
        }
        remainingRounds = 0;
    }
}
