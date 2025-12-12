using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Apex Territory")]
public class ApexTerritoryGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        // All allies gain +1 body permanently; center-position ally gains +2 instead.
        // We approximate center by choosing the occupied slot whose x-position is
        // closest to the midpoint between the leftmost and rightmost ally slots.
        SlotOwner sideOwner = owner;
        var allies = rm.AllCreatures().Where(c => c != null && c.owner == sideOwner).ToList();
        if (allies.Count == 0)
        {
            remainingRounds = 0;
            return;
        }

        var centerSlot = BoardUtils.GetCenterSlot(sideOwner, requireOccupied: true);
        Creature center = centerSlot?.currentCreature;

        foreach (var c in allies)
        {
            if (c == null || c.data == null)
                continue;
            if (c == center)
                c.body += 2;
            else
                c.body += 1;
            c.RefreshStatsUI();
        }

        // Visual feedback for all affected allies, unless the source card suppressed it.
        if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(allies);
        }

        remainingRounds = 0;
    }
}
