using UnityEngine;
using TMPro;

public class FoodPile : MonoBehaviour
{
	public int count = 10;
	public int refillPerRound = 10;
	public TextMeshProUGUI label;
	public int baseFoodPerPlayer = 3;
	public int numPlayers = 2;
	public int boardSlotsPerPlayer = 3;

	public int Take(int amount)
	{
		int t = Mathf.Min(count, amount);
		count -= t;
		UpdateUI();
		return t;
	}

	public void RefillStartOfRound()
	{
		int rollSum = 0;

        // Roll one D6
		int die = (GameManager.Instance != null) ? GameManager.Instance.NextRandomInt(1, 7) : Random.Range(1, 7);
		rollSum += die;

		int baseFood = (boardSlotsPerPlayer+1) * numPlayers + baseFoodPerPlayer;
		int newFoodAmount = baseFood + rollSum; // no carryover; discard leftovers

		count = newFoodAmount;

		UpdateUI();
	}

	public void UpdateUI()
	{
		if (label != null) label.text = $"Food: {count}";
	}
}


