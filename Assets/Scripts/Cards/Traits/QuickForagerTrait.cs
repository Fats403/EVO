using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Quick Forager")]
public class QuickForagerTrait : Trait
{
    public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
    {
        if (self == null || pile == null)
            return;
        if (amountTaken <= 0)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        // Reconstruct the pile size before this creature ate.
        int prePile = Mathf.Max(0, pile.count + amountTaken);
        if (prePile >= 4)
        {
            self.ApplyRegen(1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Regen +1",
                self.transform.position,
                new Color(0.3f, 1f, 0.3f)
            );
        }
    }
}
