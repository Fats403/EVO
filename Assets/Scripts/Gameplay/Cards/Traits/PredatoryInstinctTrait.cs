using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Predatory Instinct")]
public class PredatoryInstinctTrait : Trait
{
    public override Creature ChooseAttackTarget(
        Creature self,
        IEnumerable<Creature> candidates,
        Creature defaultTarget
    )
    {
        if (self == null)
            return defaultTarget;
        if (self.HasStatus(StatusTag.Suppress))
            return defaultTarget;
        if (candidates == null)
            return defaultTarget;

        // Pick the lowest HP valid target with deterministic tie-breaking
        var validCandidates = candidates.Where(c => c != null && c.currentHealth > 0);

        // Order by HP, then distance, then slot index for full determinism
        var slotLookup = DeterministicHelpers.GetSlotIndexLookup();
        var picked = validCandidates
            .OrderBy(c => c.currentHealth)
            .ThenBy(c => Vector3.SqrMagnitude(c.transform.position - self.transform.position))
            .ThenBy(c => DeterministicHelpers.GetSlotIndex(c, slotLookup))
            .FirstOrDefault();

        return picked ?? defaultTarget;
    }
}
