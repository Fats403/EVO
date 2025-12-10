using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Nurturing")]
public class NurturingTrait : Trait
{
    public override void OnRoundEnd(Creature self)
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

            int fatigue = ally.GetStatus(StatusTag.Fatigued);
            if (fatigue > 0)
            {
                ally.DecrementStatus(StatusTag.Fatigued, 1);
                FeedbackManager.Instance?.ShowFloatingText(
                    "Fatigue -1",
                    ally.transform.position,
                    GameColorPalette.TextPositive
                );
            }
        }
    }
}
