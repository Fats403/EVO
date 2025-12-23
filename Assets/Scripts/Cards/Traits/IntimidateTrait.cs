using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Intimidate")]
public class IntimidateTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        var enemy = BoardUtils.GetClosestEnemy(self);
        if (enemy == null)
            return;
        enemy.AddStatus(StatusTag.Fatigue, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Intimidate",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigued +2",
            enemy.transform.position,
            GameColorPalette.TextWarning
        );
    }
}
