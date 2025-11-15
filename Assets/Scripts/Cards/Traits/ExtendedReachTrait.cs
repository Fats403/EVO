using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Extended Reach")]
public class ExtendedReachTrait : Trait
{
	public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
	{
		if (self == null || amountTaken <= 0) return;
		self.eaten += 1;
		FeedbackManager.Instance?.ShowFloatingText("Bonus +1", self.transform.position, new Color(0.3f, 1f, 0.3f));
	}
}





