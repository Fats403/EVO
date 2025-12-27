using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Nutrient Surge")]
public class NutrientSurgeGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        if (rm.foodPile != null)
        {
            rm.foodPile.count += 3;
            rm.foodPile.UpdateUI();
        }

        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Nutrient Surge: +3 food added to the pile.",
                GameColorPalette.TextPositiveSoft
            );
            FeedbackManager.Instance.Log("Global Effect: Nutrient Surge adds +3 food to the pile.");
        }
    }
}
