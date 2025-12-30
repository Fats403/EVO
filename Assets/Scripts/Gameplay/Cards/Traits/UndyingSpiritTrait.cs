using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Undying Spirit")]
public class UndyingSpiritTrait : Trait
{
    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;
        // Trigger on any lethal damage, once per creature lifetime
        // Use per-creature flag instead of static HashSet for determinism
        if (self.currentHealth == 0 && !self.traitUsedUndyingSpirit)
        {
            self.currentHealth = 1;
            self.RefreshStatsUI();
            self.AddStatus(StatusTag.Regen, 3);

            // Clear negative statuses
            self.ClearAllNegativeStatuses();

            self.traitUsedUndyingSpirit = true;
            FeedbackManager.Instance?.ShowFloatingText(
                "Undying Spirit",
                self.transform.position,
                GameColorPalette.TextPositive
            );
        }
    }

    // Per-creature flag persists until creature dies/is reinitialized
    // Reset happens in Creature.Initialize()
}
