using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Demoralize")]
public class DemoralizeTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        var enemies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.owner != self.owner)
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - self.transform.position))
            .ToList();
        if (enemies.Count > 0)
        {
            enemies[0].ApplyFatigue(1, true);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(description))
        {
            description = "At round start, the nearest enemy becomes Fatigued (−1 speed).";
        }
    }
}


