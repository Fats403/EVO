using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Clear Skies")]
public class ClearSkiesGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.ForceWeather(WeatherType.Clear);
        }
        else if (
            GameManager.Instance != null
            && GameManager.Instance.weatherVideoBackground != null
        )
        {
            GameManager.Instance.weatherVideoBackground.ForceTo(WeatherType.Clear);
        }

        if (rm != null && rm.foodPile != null)
        {
            rm.foodPile.count = Mathf.Max(0, rm.foodPile.count + 2);
            rm.foodPile.UpdateUI();
        }
    }
}
