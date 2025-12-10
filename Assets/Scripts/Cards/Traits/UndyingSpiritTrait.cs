using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Undying Spirit")]
public class UndyingSpiritTrait : Trait
{
    private static readonly HashSet<Creature> used = new();

    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;
        // Trigger on any lethal damage, once per creature
        if (self.currentHealth == 0 && !used.Contains(self))
        {
            self.currentHealth = 1;
            self.RefreshStatsUI();
            self.AddStatus(StatusTag.Regen, 3);

            // Clear negative statuses
            self.ClearAllNegativeStatuses();

            used.Add(self);
            FeedbackManager.Instance?.ShowFloatingText(
                "Undying Spirit",
                self.transform.position,
                GameColorPalette.TextPositive
            );
        }
    }
}
