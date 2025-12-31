using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Resource Collapse")]
public class ResourceCollapseGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        var enemies = DeterministicHelpers.GetCreaturesSorted(c => c.owner != owner);

        // Appy 1 malnourishment to all enemy creatures
        foreach (var c in enemies)
        {
            if (c == null || c.data == null)
                continue;
            c.AddStatus(StatusTag.Malnourish, 1);
        }

        rm.foodPile.count = 0;
        rm.foodPile.UpdateUI();

        FeedbackManager.Instance.ShowGlobalAlert(
            "Resource Collapse: Food pile is now empty!",
            GameColorPalette.TextNegative
        );

        remainingRounds = 0;
    }
}
