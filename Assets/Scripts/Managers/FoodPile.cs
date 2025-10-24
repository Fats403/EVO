using UnityEngine;
using TMPro;

public class FoodPile : MonoBehaviour
{
	public int count = 10;
	public int refillPerRound = 10;
	public int maxCap = 12;
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
		count = Mathf.Min(maxCap, refillPerRound);
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (label != null) label.text = $"Food: {count}";
	}
}


