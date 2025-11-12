using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AIManager : MonoBehaviour
{
	public static AIManager Instance;

	[Header("AI Deck Setup")]
	public int cardsPerTurn = 1;
	[Header("Visuals")]
	public GameObject cardBackPrefab;

	private readonly List<ScriptableObject> drawPile = new List<ScriptableObject>();

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
		// Source of truth: DeckManager.Instance.allCards 
		var src = DeckManager.Instance.allCards;
		var pool = new List<ScriptableObject>(src);
		// Shuffle pool
		for (int i = pool.Count - 1; i > 0; i--)
		{

			int j = (GameManager.Instance != null) ? GameManager.Instance.NextRandomInt(0, i + 1) : Random.Range(0, i + 1);
			var temp = pool[i];
			pool[i] = pool[j];
			pool[j] = temp;
		}
		// Take up to 20 unique
		var picked = new List<ScriptableObject>(20);
		var seen = new System.Collections.Generic.HashSet<ScriptableObject>();
		for (int i = 0; i < pool.Count && picked.Count < 20; i++)
		{
			var card = pool[i];
			if (card == null) continue;
			if (seen.Add(card)) picked.Add(card);
		}
		drawPile.AddRange(picked);
		// Optional: shuffle draw order again
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
			CreatureCard card = PickCreatureCard();
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

	CreatureCard PickCreatureCard()
	{
		if (drawPile.Count == 0) return null;
		var enemy = FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.owner == SlotOwner.Player1)
			.ToList();
		bool enemyHasHerb = enemy.Any(c => c.data != null && c.data.type == CardType.Herbivore);
		int pile = GameManager.Instance != null && GameManager.Instance.foodPile != null ? GameManager.Instance.foodPile.count : 0;

		var candidates = drawPile.OfType<CreatureCard>().ToList();
		if (candidates.Count == 0) return null;

		CreatureCard pick = null;
		if (enemyHasHerb)
			pick = candidates.FirstOrDefault(c => c != null && c.type == CardType.Carnivore);
		if (pick == null && pile >= 8)
			pick = candidates.FirstOrDefault(c => c != null && c.type == CardType.Herbivore);
		if (pick == null)
			pick = candidates.FirstOrDefault(c => c != null && c.type == CardType.Avian);
		if (pick == null)
			pick = candidates[candidates.Count - 1];

		int removeIdx = drawPile.FindLastIndex(o => ReferenceEquals(o, pick));
		if (removeIdx >= 0) drawPile.RemoveAt(removeIdx);
		return pick;
	}

	BoardSlot ChooseSlot(List<BoardSlot> free)
	{
		// Prefer the left-most free slot for readability
		return free.OrderBy(s => s.transform.position.x).FirstOrDefault();
	}
}


