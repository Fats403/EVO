using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Bleed Master")]
public class BleedMasterTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;
        // Attribute Bleed to this creature so ticks contribute to its
        // roundDamageDealt scoring.
        target.AddStatus(StatusTag.Bleed, 1, self);
        FeedbackManager.Instance?.ShowFloatingText(
            "Bleed Master",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Bleed +1",
            target.transform.position,
            GameColorPalette.Bleed
        );
    }
}
