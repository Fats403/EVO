using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Scorch Command")]
public class ScorchCommandGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.ForceWeather(WeatherType.Wildfire);
        }
        else if (
            GameManager.Instance != null
            && GameManager.Instance.weatherVideoBackground != null
        )
        {
            GameManager.Instance.weatherVideoBackground.ForceTo(WeatherType.Wildfire);
        }
    }
}
