using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Environmental Toxin")]
public class EnvironmentalToxinGlobalEffect : GlobalEffectBase
{
    public override void OnRoundStart(ResolutionManager rm)
    {
        if (rm == null)
            return;

        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Environmental Toxin: all creatures take 1 damage.",
                new Color(0.7f, 0.9f, 1f)
            );
            FeedbackManager.Instance.Log(
                "Global Effect: Environmental Toxin deals 1 damage to all creatures."
            );
        }

        foreach (var c in rm.AllCreatures())
        {
            if (c == null)
                continue;
            c.ApplyDamage(1, null);
        }
    }
}
