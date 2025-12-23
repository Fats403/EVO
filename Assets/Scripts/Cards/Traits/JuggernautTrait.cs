using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Juggernaut")]
public class JuggernautTrait : Trait
{
    public override void OnDamageDealt(Creature self, Creature target, int finalDamage)
    {
        if (self == null || target == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        if (finalDamage <= 0)
            return;

        target.AddStatus(StatusTag.Suppress, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Juggernaut",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Suppress +1",
            target.transform.position,
            GameColorPalette.TextWarning
        );
    }

    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (self == null)
            return;
        if (finalDamage <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;

        self.AddStatus(StatusTag.Absorb, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Juggernaut",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Absorb +1",
            self.transform.position,
            GameColorPalette.Absorb
        );
    }
}
