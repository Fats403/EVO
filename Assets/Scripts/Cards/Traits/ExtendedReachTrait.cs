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
            "Food +1 [Extended Reach]",
            self.transform.position,
            GameColorPalette.TextPositive
        );
    }
}
