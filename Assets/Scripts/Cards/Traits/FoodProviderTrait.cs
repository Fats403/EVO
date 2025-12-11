using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Food Provider")]
public class FoodProviderTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || pile == null)
            return;
        if (amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        pile.count = Mathf.Max(0, pile.count + 1);
        pile.UpdateUI();
        FeedbackManager.Instance?.ShowFloatingText(
            "Food Provider",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "+1 Food",
            pile.transform.position,
            GameColorPalette.TextPositive
        );
    }
}
