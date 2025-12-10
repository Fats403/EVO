using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Bone Breaker")]
public class BoneBreakerTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;

        target.AddStatus(StatusTag.Malnourished, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Malnourished +2",
            target.transform.position,
            GameColorPalette.Starvation
        );
    }
}
