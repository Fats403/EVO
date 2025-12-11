using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Bleed Master")]
public class BleedMasterTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;
        target.AddStatus(StatusTag.Bleeding, 1);
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
