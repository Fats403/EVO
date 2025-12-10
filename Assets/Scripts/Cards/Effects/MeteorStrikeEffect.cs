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

            int applied = target.ApplyDamage(primaryDamage, null, null, "Meteor Strike");
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP",
                    target.transform.position,
                    new Color(1f, 0.5f, 0.3f)
                );
            }

            var slot = BoardUtils.GetSlotOf(target);
            if (slot == null)
                return;

            var allSlots = Object
                .FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
                .Where(s =>
                    s != null && s.owner == slot.owner && s.occupied && s.currentCreature != null
                )
                .OrderBy(s => s.transform.position.x)
                .ToList();
            int idx = allSlots.FindIndex(s => s == slot);
            if (idx < 0)
                return;

            void HitNeighbor(int i)
            {
                if (i < 0 || i >= allSlots.Count)
                    return;
                var c = allSlots[i].currentCreature;
                if (c == null || c.currentHealth <= 0 || c.isDying)
                    return;
                int splash = c.ApplyDamage(splashDamage, null, null, "Meteor Strike");
                if (splash > 0)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        $"-{splash} HP",
                        c.transform.position,
                        new Color(1f, 0.5f, 0.3f)
                    );
                }
            }

            HitNeighbor(idx - 1);
            HitNeighbor(idx + 1);
        }

        var target = self;
        VFXManager.Instance.SpawnMeteor(target, ApplyDamageWithSplash);

        remainingRounds = 0;
    }
}
