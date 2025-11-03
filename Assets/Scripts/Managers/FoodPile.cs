using UnityEngine;
using TMPro;

public class FoodPile : MonoBehaviour
{
	public int count = 10;
	public int refillPerRound = 10;
	public int maxCap = 13;
	public TextMeshProUGUI label;

	public int Take(int amount)
	{
		int t = Mathf.Min(count, amount);
		count -= t;
		UpdateUI();
		return t;
	}

	public void RefillStartOfRound()
	{
		int players = 2; // current game supports 2 players
		int rollSum = 0;
		for (int i = 0; i < players; i++)
		{
			int die = (GameManager.Instance != null) ? GameManager.Instance.NextRandomInt(1, 7) : Random.Range(1, 7);
			rollSum += die;
		}
		int newFood = 3 * players + rollSum; // no carryover; discard leftovers
		count = Mathf.Min(maxCap, newFood);
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (label != null) label.text = $"Food: {count}";
	}
}


