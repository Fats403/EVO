using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Nutrient Surge")]
public class NutrientSurgeGlobalEffect : GlobalEffectBase
{

    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null) return;

        rm.foodPile.count += 3;
        rm.foodPile.UpdateUI();
    }
}


