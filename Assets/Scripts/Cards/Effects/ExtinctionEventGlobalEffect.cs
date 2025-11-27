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
                        target.ApplyDamage(3, null);
                    }
                );
            }
        }
        else
        {
            foreach (var c in all)
            {
                c.ApplyDamage(3, null);
            }
        }

        if (rm.foodPile != null)
        {
            rm.foodPile.count += 3;
            rm.foodPile.UpdateUI();
        }
        remainingRounds = 0;
    }
}
