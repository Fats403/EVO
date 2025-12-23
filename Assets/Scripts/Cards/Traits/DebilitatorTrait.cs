using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Debilitator")]
public class DebilitatorTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;
        target.AddStatus(StatusTag.Fatigue, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Debilitator",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigued +2",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
