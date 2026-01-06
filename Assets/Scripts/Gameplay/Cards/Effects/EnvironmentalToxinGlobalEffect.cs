using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Environmental Toxin")]
public class EnvironmentalToxinGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        // Last for 2 rounds by default.
        remainingRounds = 2;
    }

    public override void OnRoundStart(ResolutionManager rm)
    {
        if (rm == null)
            return;

        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Environmental Toxin",
                GameColorPalette.TextSpecial
            );
            FeedbackManager.Instance.Log(
                "Global Effect: Environmental Toxin deals 1 damage to all creatures."
            );
        }

        foreach (var c in rm.AllCreatures())
        {
            if (c == null)
                continue;
            int applied = c.ApplyDamage(1, null, null, "Toxin");
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP (Toxin)",
                    c.transform.position,
                    GameColorPalette.TextSpecial
                );
            }
        }
    }
}
