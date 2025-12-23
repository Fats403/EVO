using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Extinction Flyer")]
public class ExtinctionFlyerTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppress))
            return baseDamage;
        if (GameManager.Instance == null)
            return baseDamage;
        if (GameManager.Instance.currentEra != Era.Extinction)
            return baseDamage;

        return Mathf.Max(0, baseDamage + 1);
    }

    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;
        if (GameManager.Instance == null)
            return;
        if (GameManager.Instance.currentEra != Era.Extinction)
            return;

        target.AddStatus(StatusTag.Suppress, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Extinction Flyer",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Suppress +1",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
