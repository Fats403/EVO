using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Apex Predator")]
public class ApexPredatorTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c =>
                c != null
                && c.owner == self.owner
                && c.currentHealth > 0
                && !c.isDying
                && c.data != null
                && c.data.type == CardType.Carnivore
            )
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - self.transform.position))
            .Take(2)
            .ToList();
        foreach (var ally in allies)
        {
            ally.AddStatus(StatusTag.Rage, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Rage",
                ally.transform.position,
                new Color(1f, 0.4f, 0.2f)
            );
        }
    }

    // TODO: this seems like a hack to ignore body-size restrictions for this attacker while not suppressed.
    // We should probably have a better way to handle this.

    public override int PredatorBodyBonusForTargeting(Creature self)
    {
        // Effectively ignore body-size restrictions for this attacker while not suppressed.
        if (self == null || self.HasStatus(StatusTag.Suppressed))
            return 0;
        return 100;
    }
}
