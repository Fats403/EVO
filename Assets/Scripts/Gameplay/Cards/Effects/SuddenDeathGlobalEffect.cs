using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Sudden Death")]
public class SuddenDeathGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        // Get all creatures in deterministic slot order
        var all = DeterministicHelpers.GetAllCreaturesSorted();
        var p1 = all.Where(c => c.owner == SlotOwner.Player1).ToList();
        var p2 = all.Where(c => c.owner == SlotOwner.Player2).ToList();

        // Kill one random creature from each player
        var p1Pick = DeterministicHelpers.PickRandom(p1);
        if (p1Pick != null)
        {
            p1Pick.Kill("Sudden Death");
        }

        var p2Pick = DeterministicHelpers.PickRandom(p2);
        if (p2Pick != null)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Sudden Death",
                p2Pick.transform.position,
                GameColorPalette.Damage
            );
            p2Pick.Kill("Sudden Death");
        }

        remainingRounds = 0;
    }
}
