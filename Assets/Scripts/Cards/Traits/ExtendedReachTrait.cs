using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Extended Reach")]
public class ExtendedReachTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;
        self.eaten += 1;
        FeedbackManager.Instance?.ShowFloatingText(
            "Extended Reach",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "+1 Food",
            self.transform.position,
            GameColorPalette.TextPositive
        );
    }
}
