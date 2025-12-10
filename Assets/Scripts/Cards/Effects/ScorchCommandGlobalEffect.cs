using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Scorch Command")]
public class ScorchCommandGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        GameManager.Instance.weatherVideoBackground.ForceTo(WeatherType.Wildfire);
    }
}

