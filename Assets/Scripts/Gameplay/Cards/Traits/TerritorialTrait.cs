using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Territorial")]
public class TerritorialTrait : Trait
{
    public override void OnAfterKill(Creature self, Creature target)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        var slot = BoardUtils.GetSlotOf(target);
        if (slot == null)
            return;

        // Find enemy slots (same owner as the killed target) in deterministic order.
        // Use BoardUtils.GetSlotsForOwner which returns slots sorted by slot index.
        var allSlots = BoardUtils.GetSlotsForOwner(slot.owner, occupiedOnly: true);
        int idx = allSlots.FindIndex(s => s == slot);
        if (idx < 0)
            return;

        void HitSlot(int i)
        {
            if (i < 0 || i >= allSlots.Count)
                return;
            var c = allSlots[i].currentCreature;
            if (c == null || c.currentHealth <= 0 || c.isDying)
                return;
            int applied = c.ApplyDamage(1, self);
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP",
                    c.transform.position,
                    GameColorPalette.Damage
                );
            }
        }

        FeedbackManager.Instance?.ShowFloatingText(
            "Territorial",
            self.transform.position,
            GameColorPalette.TextWarning
        );

        // Left and right adjacent positions relative to the killed target.
        HitSlot(idx - 1);
        HitSlot(idx + 1);
    }
}
