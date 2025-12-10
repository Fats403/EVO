using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Meteor Strike")]
public class MeteorStrikeEffect : EffectTraitBase
{
    public int primaryDamage = 3;
    public int splashDamage = 2;

    public override void OnApply(Creature self)
    {
        if (self == null)
            return;

        void ApplyDamageWithSplash(Creature target)
        {
            if (target == null || target.isDying || target.currentHealth <= 0)
                return;

            var adjacentAllies = BoardUtils.GetAdjacentAllies(target);

            // Primary hit
            int applied = target.ApplyDamage(primaryDamage, null, null, "Meteor Strike");
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP",
                    target.transform.position,
                    new Color(1f, 0.5f, 0.3f)
                );
            }

            // Adjacent splash using BoardUtils
            foreach (var neighbor in adjacentAllies)
            {
                if (neighbor == null || neighbor.currentHealth <= 0 || neighbor.isDying)
                    continue;

                int splash = neighbor.ApplyDamage(splashDamage, null, null, "Meteor Strike");
                if (splash > 0)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        $"-{splash} HP",
                        neighbor.transform.position,
                        new Color(1f, 0.5f, 0.3f)
                    );
                }
            }
        }

        // `self` here is the creature being hit (enemy)
        VFXManager.Instance.SpawnMeteor(self, ApplyDamageWithSplash);

        remainingRounds = 0;
    }
}
