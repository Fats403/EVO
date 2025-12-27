using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Coordinated Hunt")]
public class CoordinatedHuntGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        var all = rm.AllCreatures()
            .Where(c => c != null && c.data != null && c.data.type == CardType.Carnivore)
            .ToList();

        foreach (var c in all)
        {
            c.AddStatus(StatusTag.SpeedUp, 1);
        }

        // If the caster owns 3+ carnivores, also grant Rage to their carnivores only.
        var mine = all.Where(c => c.owner == owner).ToList();
        if (mine.Count >= 3)
        {
            foreach (var c in mine)
            {
                c.AddStatus(StatusTag.Rage, 1);
            }
        }

        // Visual feedback for all affected carnivores, unless the source card suppressed it.
        if (!suppressHitBounceFromSource && EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(all);
        }

        remainingRounds = 0;
    }
}
