using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
	public FoodPile foodPile;
	[Header("Timing")]
	public float preStealDelay = 0.3f;
	public float eatDelay = 1.0f;
	public float attackWindup = 0.25f;
	public float attackResolvePause = 0.6f;
	public float afterCarnivoreDelay = 0.8f;
	public float starveDelay = 0.8f;
    public float statusEffectDelay = 0.8f;

	public IEnumerator RevealAndResolveRound()
	{
		// Reveal pending cards into creatures
		RevealPendings();

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
				if (taken > 0) Debug.Log($"[PreEat] {c.name} stole {taken}.");
			}
			yield return new WaitForSeconds(preStealDelay);
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
			if (taken > 0)
			{
				FeedbackManager.Instance?.ShowFloatingText($"+{taken} food", c.transform.position, new Color(0.3f, 1f, 0.3f));
				FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} ate {taken}.");
			}
			yield return new WaitForSeconds(eatDelay);
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
                // Avian require speed advantage to be targeted
                if (prey.data != null && prey.data.type == CardType.Avian)
                {
                    if (predator.speed <= prey.speed) return false;
                }
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
			if (negated)
			{
				FeedbackManager.Instance?.ShowFloatingText("Blocked", target.transform.position, new Color(1f, 0.8f, 0.2f));
				FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(predator.owner)} {predator.name} attack negated by {target.name}");
				yield return new WaitForSeconds(statusEffectDelay);
				continue;
			}

			// Attack bump tween on creature
			var predCreature = predator;
			if (predCreature != null)
				yield return predCreature.StartCoroutine(predCreature.PlayAttackBump(0.35f, attackWindup));

			// Brief red flash on target
			if (target != null)
				yield return target.StartCoroutine(target.FlashDamage(0.12f));

            // Predator gains food from prey up to its need
            int need = Mathf.Max(0, predator.body - predator.eaten);
            int gain = Mathf.Min(need, target.body);
            if (gain > 0)
            {
                predator.eaten += gain;
                FeedbackManager.Instance?.ShowFloatingText($"+{gain} food", predator.transform.position, new Color(0.9f, 0.6f, 0.3f));
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(predator.owner)} {predator.name} gains {gain} from {target.name}");
            }

            // Remove target
			var targetSlot = FindSlotOf(target);
			if (targetSlot != null)
			{
				FeedbackManager.Instance?.ShowFloatingText("EAT", target.transform.position, new Color(1f, 0.4f, 0.4f));
				FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(predator.owner)} {predator.name} eats {target.name}");
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
			yield return new WaitForSeconds(attackResolvePause);
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
            // Baseline avian survival: take 1 from pile after carnivore phase if not full
            if (c.data != null && c.data.type == CardType.Avian && c.eaten < c.body && foodPile.count > 0)
            {
                int tkn = foodPile.Take(1);
                c.eaten += tkn;
                if (tkn > 0)
                {
                    FeedbackManager.Instance?.ShowFloatingText("+1 food", c.transform.position, new Color(0.5f, 0.8f, 1f));
                    FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} gained 1 after carnivores.");
                }
            }
			yield return new WaitForSeconds(afterCarnivoreDelay);
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
				FeedbackManager.Instance?.ShowFloatingText("Starved", c.transform.position, Color.gray);
				FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} died of starvation");
				var s = FindSlotOf(c);
				Object.Destroy(c.gameObject);
				if (s != null) s.Vacate();
			}
            else if (c.eaten < c.body)
			{
                if (c.data != null && c.data.type == CardType.Carnivore)
                {
                    // Carnivores score partial, then shrink by deficit
                    int gainScore = c.eaten;
                    if (gainScore > 0)
                    {
                        ScoreManager.Add(c.owner, gainScore);
                        FeedbackManager.Instance?.ShowFloatingText($"Score +{gainScore}", c.transform.position, new Color(1f, 0.7f, 0.3f));
                        FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} partial scores {gainScore}");
                    }
                    int deficit = c.body - c.eaten;
                    c.body = Mathf.Max(0, c.body - deficit);
                    c.eaten = 0;
                    if (c.body == 0)
                    {
                        FeedbackManager.Instance?.ShowFloatingText("Collapsed", c.transform.position, Color.red);
                        FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} collapsed after underfeeding");
                        var s = FindSlotOf(c);
                        Object.Destroy(c.gameObject);
                        if (s != null) s.Vacate();
                    }
                    else
                    {
                        FeedbackManager.Instance?.ShowFloatingText("Underfed (shrunk)", c.transform.position, Color.yellow);
                        FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} shrinks to body {c.body}");
                    }
                }
                else
                {
                    FeedbackManager.Instance?.ShowFloatingText("Underfed", c.transform.position, Color.yellow);
                    FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} underfed, loses {c.eaten}");
                    c.eaten = 0;
                }
			}
			else
			{
				int gain = c.eaten;
				ScoreManager.Add(c.owner, gain);
				FeedbackManager.Instance?.ShowFloatingText($"Score +{gain}", c.transform.position, Color.cyan);
				FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} scores {gain}");
				c.eaten = 0;
			}
			yield return new WaitForSeconds(starveDelay);
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


