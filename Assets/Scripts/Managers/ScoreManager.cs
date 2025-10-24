using TMPro;
using UnityEngine;

public static class ScoreManager
{
	public static int player1;
	public static int player2;
	public static TextMeshProUGUI p1Label;
	public static TextMeshProUGUI p2Label;

	public static void Reset()
	{
		player1 = 0;
		player2 = 0;
		UpdateUI();
	}

	public static void Add(SlotOwner owner, int amount)
	{
		if (owner == SlotOwner.Player1) player1 += amount; else player2 += amount;
		UpdateUI();
	}

	public static void UpdateUI()
	{
		if (p1Label != null) p1Label.text = $"P1: {player1}";
		if (p2Label != null) p2Label.text = $"P2: {player2}";
	}
}


