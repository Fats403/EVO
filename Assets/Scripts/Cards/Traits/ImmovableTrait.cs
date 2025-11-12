using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Immovable")]
public class ImmovableTrait : Trait
{
	public override void OnRoundStart(Creature self)
	{
		if (self == null) return;
		// Gains Taunt each round (refresh)
		self.AddStatus(StatusTag.Taunt, 1);
	}

	public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
	{
		if (self == null || amountTaken <= 0) return;
		if (pile == null) return;
		pile.count = Mathf.Max(0, pile.count + 2);
		pile.UpdateUI();
		FeedbackManager.Instance?.ShowFloatingText("Food +2", pile.transform.position, new Color(0.5f, 0.9f, 0.5f));
	}
}




