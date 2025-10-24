using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
	public FoodPile foodPile;
	public float stepDelay = 0.5f;

	public IEnumerator RevealAndResolveRound()
	{
		// Reveal pending cards into creatures
		RevealPendings();
		yield return new WaitForSeconds(stepDelay);

		// Resolve Herbivores eating
		yield return StartCoroutine(ResolveHerbivores());

		// Resolve Carnivores attacks
		yield return StartCoroutine(ResolveCarnivores());

		// Starvation and scoring
		yield return StartCoroutine(ResolveStarvationAndScoring());
	}

	void RevealPendings()
	{
		var slots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
		foreach (var s in slots)
		{
			if (s.hasPending && !s.occupied && s.pendingCard != null)
			{
				DeckManager.Instance.SpawnCreature(s.pendingCard, s);
				s.ClearPending();
			}
		}
	}

	IEnumerable<Creature> AllCreatures()
	{
		return FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.OrderByDescending(c => c.speed)
			.ThenBy(c => c.transform.position.x);
	}

	IEnumerator ResolveHerbivores()
	{
		if (foodPile == null) yield break;
		foreach (var c in AllCreatures())
		{
			if (c == null || c.data == null) continue;
			if (c.data.type != CardType.Herbivore) continue;
			int need = Mathf.Max(0, c.body - c.eaten);
			if (need <= 0) continue;
			int taken = foodPile.Take(need);
			c.eaten += taken;
			if (taken > 0) Debug.Log($"[Eat] {c.name} ate {taken}. Pile: {foodPile.count}");
			yield return new WaitForSeconds(stepDelay);
		}
	}

	IEnumerator ResolveCarnivores()
	{
		var creatures = AllCreatures().ToList();
		foreach (var predator in creatures)
		{
			if (predator == null || predator.data == null) continue;
			if (predator.data.type != CardType.Carnivore) continue;
			// Choose a target herbivore on the opposing side with smaller body
			var candidates = creatures
				.Where(c => c != null && c.data != null && c.data.type == CardType.Herbivore)
				.Where(c => c.owner != predator.owner)
				.Where(c => c.body < predator.body)
				.OrderBy(c => Vector3.SqrMagnitude(c.transform.position - predator.transform.position))
				.ToList();
			if (candidates.Count == 0) continue;
			var target = candidates[0];
			// Remove target
			var targetSlot = FindSlotOf(target);
			if (targetSlot != null)
			{
				Debug.Log($"[Attack] {predator.name} eats {target.name}");
				Object.Destroy(target.gameObject);
				targetSlot.Vacate();
			}
			yield return new WaitForSeconds(stepDelay);
		}
	}

	IEnumerator ResolveStarvationAndScoring()
	{
		var creatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);
		foreach (var c in creatures)
		{
			if (c == null) continue;
			if (c.eaten == 0)
			{
				Debug.Log($"[Starve] {c.name} died");
				var s = FindSlotOf(c);
				Object.Destroy(c.gameObject);
				if (s != null) s.Vacate();
			}
			else if (c.eaten < c.body)
			{
				Debug.Log($"[Starve] {c.name} underfed, loses {c.eaten}");
				c.eaten = 0;
			}
			else
			{
				int gain = c.eaten;
				ScoreManager.Add(c.owner, gain);
				Debug.Log($"[Score] {c.name} scores {gain} for {c.owner}");
				c.eaten = 0;
			}
			yield return new WaitForSeconds(stepDelay * 0.5f);
		}
	}

	BoardSlot FindSlotOf(Creature c)
	{
		var slots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
		foreach (var s in slots)
		{
			if (s.currentCreature == c) return s;
		}
		return null;
	}
}


