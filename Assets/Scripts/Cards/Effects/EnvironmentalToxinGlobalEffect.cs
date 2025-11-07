using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Environmental Toxin")]
public class EnvironmentalToxinGlobalEffect : GlobalEffectBase
{
    public override void OnRoundStart(ResolutionManager rm)
    {
        if (rm == null) return;
        foreach (var c in rm.AllCreatures())
        {
            if (c == null) continue;
            c.ApplyDamage(1, null);
        }
    }
}


