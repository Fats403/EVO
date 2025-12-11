using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Living Shield")]
public class LivingShieldTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var adj = BoardUtils.GetAdjacentAllies(self);
        foreach (var ally in adj)
        {
            if (ally == null)
                continue;
            if (ally.currentHealth <= 0 || ally.isDying)
                continue;

            ally.AddStatus(StatusTag.Absorb, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Living Shield",
                self.transform.position,
                GameColorPalette.TextWarning
            );
            FeedbackManager.Instance?.ShowFloatingText(
                "Absorb +1",
                ally.transform.position,
                GameColorPalette.Absorb
            );
        }
    }
}
