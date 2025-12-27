using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Resource Collapse")]
public class ResourceCollapseGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        rm.foodPile.count = 0;
        rm.foodPile.UpdateUI();

        FeedbackManager.Instance.ShowGlobalAlert(
            "Resource Collapse: Food pile is now empty",
            GameColorPalette.TextNegative
        );

        remainingRounds = 0;
    }
}
