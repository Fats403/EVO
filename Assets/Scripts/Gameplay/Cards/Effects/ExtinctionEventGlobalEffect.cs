using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Global/Extinction Event")]
public class ExtinctionEventGlobalEffect : GlobalEffectBase
{
    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        // Get all creatures in deterministic slot order
        var all = DeterministicHelpers.GetAllCreaturesSorted();

        static void ApplyExtinctionDamage(Creature c)
        {
            if (c == null || c.isDying || c.currentHealth <= 0)
                return;

            int baseDmg = 2;
            // Creatures with effective body 4+ take +1 additional damage.
            int effBody = ResolutionManager.Instance.GetEffectiveBody(c);

            if (effBody >= 4)
                baseDmg += 1;

            int applied = c.ApplyDamage(baseDmg, null, null, "Extinction Event");
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP",
                    c.transform.position,
                    GameColorPalette.Damage
                );
            }
        }

        foreach (var c in all)
        {
            var captured = c;
            VFXManager.Instance.SpawnMeteor(captured, ApplyExtinctionDamage);
        }

        if (rm.foodPile != null)
        {
            rm.foodPile.count += 3;
            rm.foodPile.UpdateUI();
            FeedbackManager.Instance?.ShowFloatingText(
                "+3 Food (Extinction Event)",
                rm.foodPile.transform.position,
                GameColorPalette.TextPositive
            );
        }

        remainingRounds = 0;
    }
}
