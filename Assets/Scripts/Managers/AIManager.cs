using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AIManager : MonoBehaviour
{
	public static AIManager Instance;

	[Header("AI Deck Setup")]
	public List<CardData> allCards;
	public int cardsPerTurn = 1;
	[Header("Visuals")]
	public GameObject cardBackPrefab;

	private readonly List<CardData> drawPile = new List<CardData>();

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		BuildDeck();
	}

	public void BuildDeck()
	{
		drawPile.Clear();
		drawPile.AddRange(allCards);
		for (int i = drawPile.Count - 1; i > 0; i--)
		{
			int j = (GameManager.Instance != null) ? GameManager.Instance.NextRandomInt(0, i + 1) : Random.Range(0, i + 1);
			var temp = drawPile[i];
			drawPile[i] = drawPile[j];
			drawPile[j] = temp;
		}
	}

	public void TakeTurnPlace()
	{
		var freeSlots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
			.Where(s => s.owner == SlotOwner.Player2 && !s.occupied)
			.ToList();
		if (freeSlots.Count == 0) return;

		for (int n = 0; n < cardsPerTurn && drawPile.Count > 0 && freeSlots.Count > 0; n++)
		{
			CardData card = PickCard();
			if (card == null) break;
			BoardSlot slot = ChooseSlot(freeSlots);
			if (slot == null) break;
			bool ok = slot.SetPending(card);
			if (ok)
			{
				if (cardBackPrefab != null) slot.ShowPendingVisual(cardBackPrefab);
				freeSlots.Remove(slot);
			}
			else
			{
				// if failed, put card back to bottom
				drawPile.Insert(0, card);
			}
		}
	}

	CardData PickCard()
	{
		if (drawPile.Count == 0) return null;
		// Simple heuristic:
		// If player1 has herbivores, prefer carnivore; else if food pile high, prefer herbivore; else avian; else any.
		var enemy = FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.owner == SlotOwner.Player1)
			.ToList();
		bool enemyHasHerb = enemy.Any(c => c.data != null && c.data.type == CardType.Herbivore);
		int pile = GameManager.Instance != null && GameManager.Instance.foodPile != null ? GameManager.Instance.foodPile.count : 0;

		CardData pick = null;
		if (enemyHasHerb)
			pick = drawPile.FirstOrDefault(c => c != null && c.type == CardType.Carnivore);
		if (pick == null && pile >= 8)
			pick = drawPile.FirstOrDefault(c => c != null && c.type == CardType.Herbivore);
		if (pick == null)
			pick = drawPile.FirstOrDefault(c => c != null && c.type == CardType.Avian);
		if (pick == null)
			pick = drawPile[drawPile.Count - 1];

		drawPile.Remove(pick);
		return pick;
	}

	BoardSlot ChooseSlot(List<BoardSlot> free)
	{
		// Prefer the left-most free slot for readability
		return free.OrderBy(s => s.transform.position.x).FirstOrDefault();
	}
}


