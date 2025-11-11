using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Generous")]
public class GenerousTrait : Trait
{
	public override void OnAfterEat(Creature self, int amountTaken, FoodPile pile)
	{
		if (self == null || amountTaken <= 0) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		var adj = BoardUtils.GetAdjacentAllies(self);
		foreach (var ally in adj)
		{
			if (ally == null || ally.data == null) continue;
			if (ally.data.type != CardType.Herbivore) continue;
			ally.eaten += 1;
			FeedbackManager.Instance?.ShowFloatingText("+1 food", ally.transform.position, new Color(0.3f, 1f, 0.3f));
		}
	}
}


