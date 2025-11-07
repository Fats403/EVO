using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herding")]
public class HerdingTrait : Trait
{
    // TODO: This may need to be looked at....
    public float adjacencySqrDist = 4f; // ~2 units

    public override int ModifyIncomingDamage(Creature self, Creature attacker, int baseDamage)
    {
        var allies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c != self && c.owner == self.owner);
        bool adjacent = allies.Any(a => (a.transform.position - self.transform.position).sqrMagnitude <= adjacencySqrDist);
        return adjacent ? Mathf.Max(0, baseDamage - 1) : baseDamage;
    }
}


