using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Thick Hide")]
public class ThickHideTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppress))
            return;
        self.AddStatus(StatusTag.Absorb, 2);
        FeedbackManager.Instance?.ShowFloatingText(
            "Thick Hide",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Absorb +2",
            self.transform.position,
            GameColorPalette.Absorb
        );
    }
}
