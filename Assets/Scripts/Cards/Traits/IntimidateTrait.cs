using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Intimidate")]
public class IntimidateTrait : Trait
{
    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        var enemy = BoardUtils.GetClosestEnemy(self);
        if (enemy == null)
            return;
        enemy.AddStatus(StatusTag.Fatigued, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Fatigued +2",
            enemy.transform.position,
            Color.yellow
        );
    }
}
