using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Juggernaut")]
public class JuggernautTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        if (finalDamage <= 0)
            return;

        target.AddStatus(StatusTag.Suppressed, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Suppressed",
            target.transform.position,
            Color.yellow
        );
    }

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

