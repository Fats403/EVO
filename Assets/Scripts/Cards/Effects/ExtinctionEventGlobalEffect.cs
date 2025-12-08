using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Extinction Event")]
public class ExtinctionEventGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        var all = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .ToList();

        // If we have a VFXManager + meteor prefab configured, drive the damage
        // from meteor impacts; otherwise, fall back to immediate damage.
        if (VFXManager.Instance != null && VFXManager.Instance.HasMeteorPrefab)
        {
            foreach (var c in all)
            {
                var captured = c;
                VFXManager.Instance.SpawnMeteor(
                    captured,
                    target =>
                    {
                        if (target == null || target.isDying || target.currentHealth <= 0)
                            return;
                        int applied = target.ApplyDamage(3, null, null, "Extinction Event");
                        if (applied > 0)
                        {
                            FeedbackManager.Instance?.ShowFloatingText(
                                $"-{applied} HP",
                                c.transform.position,
                                new Color(1f, 0.5f, 0.5f)
                            );
                        }
                    }
                );
            }
        }

        if (rm.foodPile != null)
        {
            rm.foodPile.count += 3;
            rm.foodPile.UpdateUI();
            FeedbackManager.Instance?.ShowFloatingText(
                "+3 Food (Extinction Event)",
                rm.foodPile.transform.position,
                new Color(1f, 0.8f, 0.5f)
            );
        }

        remainingRounds = 0;
    }
}
