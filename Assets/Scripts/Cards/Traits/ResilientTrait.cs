using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Resilient")]
public class ResilientTrait : Trait
{
    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (finalDamage <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        self.AddStatus(StatusTag.Absorb, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Absorb +1",
            self.transform.position,
            Color.cyan
        );
    }
}

