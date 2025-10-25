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

		// Round start hooks
		foreach (var c in AllCreatures())
		{
			c.defendedThisRound = false;
			c.tempSpeedMod = 0;
			if (c.traits == null) continue;
			foreach (var t in c.traits) { if (t != null) t.OnRoundStart(c); }
		}

		// Pre-herbivore trait steals (e.g., Peregrine)
		yield return StartCoroutine(ResolvePreHerbivoreSteals());

		// Resolve Herbivores eating
		yield return StartCoroutine(ResolveHerbivores());

		// Resolve Carnivores attacks
		yield return StartCoroutine(ResolveCarnivores());

		// Starvation and scoring
		yield return StartCoroutine(ResolveStarvationAndScoring());

		// Round end hooks
		foreach (var c in AllCreatures())
		{
			if (c.traits == null) continue;
			foreach (var t in c.traits) { if (t != null) t.OnRoundEnd(c); }
		}
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
			.OrderByDescending(c => c.speed + c.tempSpeedMod + (c.traits?.Sum(t => t != null ? t.SpeedBonus(c) : 0) ?? 0))
			.ThenBy(c => c.transform.position.x);
	}

	IEnumerator ResolvePreHerbivoreSteals()
	{
		if (foodPile == null) yield break;
		foreach (var c in AllCreatures())
		{
			if (c.traits == null) continue;
			int steal = 0;
			foreach (var t in c.traits)
			{
				if (t == null) continue;
				steal += Mathf.Max(0, t.PreHerbivorePileSteal(c, foodPile));
			}
			if (steal > 0)
			{
				int taken = foodPile.Take(steal);
				c.eaten += taken;
				if (taken > 0) Debug.Log($"[PreEat] {c.name} stole {taken}. Pile: {foodPile.count}");
			}
			yield return new WaitForSeconds(stepDelay * 0.3f);
		}
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
			int desired = need;
			if (c.traits != null)
			{
				foreach (var t in c.traits)
				{
					if (t == null) continue;
					desired = t.ModifyHerbivoreEatAmount(c, desired, foodPile);
				}
				desired = Mathf.Max(0, desired);
			}
			int taken = foodPile.Take(desired);
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
				.Where(c => IsValidPrey(predator, c))
				.OrderBy(c => Vector3.SqrMagnitude(c.transform.position - predator.transform.position))
				.ToList();
	bool IsValidPrey(Creature predator, Creature prey)
	{
		if (prey.body < predator.body) return true;
		if (prey.body == predator.body)
		{
			// allow equal-body if any trait grants it
			if (predator.traits != null)
			{
				foreach (var t in predator.traits)
				{
					if (t != null && t.CanTargetEqualBody(predator, prey)) return true;
				}
			}
		}
		return false;
	}
			if (candidates.Count == 0) continue;
			var target = candidates[0];
			// Check target defense traits – may negate attack
			bool negated = false;
			if (target.traits != null)
			{
				foreach (var tr in target.traits)
				{
					if (tr != null && tr.TryNegateAttack(target, predator)) { negated = true; break; }
				}
			}
			if (negated) { Debug.Log($"[Attack] {predator.name} attack negated by {target.name}"); yield return new WaitForSeconds(stepDelay * 0.5f); continue; }

			// Remove target
			var targetSlot = FindSlotOf(target);
			if (targetSlot != null)
			{
				Debug.Log($"[Attack] {predator.name} eats {target.name}");
				Object.Destroy(target.gameObject);
				targetSlot.Vacate();
				// Notify predator traits
				if (predator.traits != null)
				{
					foreach (var tr in predator.traits) { if (tr != null) tr.OnAfterKill(predator, target); }
				}
				// Notify all about death (for scavengers)
				foreach (var c2 in AllCreatures())
				{
					if (c2 == null || c2 == target) continue;
					if (c2.traits == null) continue;
					foreach (var tr in c2.traits) { if (tr != null) tr.OnAnyDeath(c2, target); }
				}
			}
			yield return new WaitForSeconds(stepDelay);
		}
	}

	IEnumerator ResolveAfterCarnivorePhase()
	{
		if (foodPile == null) yield break;
		foreach (var c in AllCreatures())
		{
			if (c.traits == null) continue;
			foreach (var t in c.traits)
			{
				if (t == null) continue;
				t.OnAfterCarnivorePhase(c, foodPile);
			}
			yield return new WaitForSeconds(stepDelay * 0.3f);
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


