using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Adaptive Armor")]
public class AdaptiveArmorTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || amountTaken <= 0)
            return;
        // Permanent body +1 on this creature instance
        self.body += 1;
        self.RefreshStatsUI();
        FeedbackManager.Instance?.ShowFloatingText(
            "Adaptive Armor",
            self.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Body +1",
            self.transform.position,
            GameColorPalette.TextPositive
        );
    }
}
