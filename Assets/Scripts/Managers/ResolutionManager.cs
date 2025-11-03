using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
	public FoodPile foodPile;
	[Header("Timing")]
    public float preStealDelay = 0.6f;
    public float eatDelay = 0.7f;
    public float attackWindup = 0.35f;
    public float attackResolvePause = 0.6f;
    public float afterCarnivoreDelay = 0.6f;
    public float starveDelay = 0.7f;
    public float statusEffectDelay = 0.6f;

	public IEnumerator RevealAndResolveRound()
	{
		// Reveal pending cards into creatures
		RevealPendings();

		// Round start hooks
        foreach (var c in AllCreatures())
		{
			c.defendedThisRound = false;
            c.tempSpeedMod = 0;
            // Apply fatigue penalty for this round before trait start hooks
            if (c.fatigued)
            {
                c.tempSpeedMod -= 1;
            }
            if (c.traits != null)
            {
                foreach (var t in c.traits) { if (t != null) t.OnRoundStart(c); }
            }
            c.ResetRoundBookkeeping();
            c.RefreshStatsUI();
            // Clear fatigue flag after it has been applied for this round
            c.fatigued = false;
		}

		// Pre-herbivore trait steals (e.g., Peregrine)
		yield return StartCoroutine(ResolvePreHerbivoreSteals());

		// Resolve Herbivores eating
		yield return StartCoroutine(ResolveHerbivores());

        // Resolve Attacks (Carnivores and Avians)
        yield return StartCoroutine(ResolveAttacks());

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
        var q = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .OrderByDescending(c => c.speed + c.tempSpeedMod + (c.traits?.Sum(t => t != null ? t.SpeedBonus(c) : 0) ?? 0));
        // Deterministic tie-breaker: RNG-based shuffle for equals
        int Rand()
        {
            return GameManager.Instance != null ? GameManager.Instance.NextRandomInt(0, int.MaxValue) : UnityEngine.Random.Range(0, int.MaxValue);
        }
        return q.ThenBy(_ => Rand());
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

    IEnumerator ResolveAttacks()
	{
        var creatures = AllCreatures().ToList();
        foreach (var attacker in creatures)
		{
            if (attacker == null || attacker.data == null) continue;
            if (attacker.data.type == CardType.Herbivore)
            {
                bool traitAllows = attacker.traits != null && attacker.traits.Any(t => t != null && t.CanAttack(attacker));
                if (!traitAllows) continue; // herbivores don't attack by default
            }
            // Candidates: opponent, cannot target Carnivores
            var candidates = creatures
                .Where(c => c != null && c.data != null && c.owner != attacker.owner)
                .Where(c => c.data.type != CardType.Carnivore)
                .Where(c => IsValidTarget(attacker, c))
                .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - attacker.transform.position))
                .ToList();

            bool IsValidTarget(Creature atk, Creature tgt)
            {
                // Carnivore attacking Avian requires speed >= target
                if (tgt.data != null && tgt.data.type == CardType.Avian && atk.data.type == CardType.Carnivore)
                {
                    if (atk.speed < tgt.speed) return false;
                }
                int bodyBonus = 0;
                if (atk.traits != null)
                {
                    foreach (var t in atk.traits)
                    {
                        if (t != null) bodyBonus += t.PredatorBodyBonusForTargeting(atk);
                    }
                }
                int effAtkBody = atk.body + bodyBonus;
                if (tgt.body < effAtkBody) return true;
                if (tgt.body == effAtkBody)
                {
                    // allow equal-body if any trait grants it
                    if (atk.traits != null)
                    {
                        foreach (var t in atk.traits)
                        {
                            if (t != null && t.CanTargetEqualBody(atk, tgt)) return true;
                        }
                    }
                }
                // Additional target gating via traits (e.g., camouflage)
                if (atk.traits != null)
                {
                    foreach (var t in atk.traits)
                    {
                        if (t != null && !t.CanTarget(atk, tgt)) return false;
                    }
                }
                return false;
            }
            
			if (candidates.Count == 0) continue;
            var target = candidates[0];
            // Attack bump tween on creature (always perform to show attempted attack)
            var atkCreature = attacker;
            if (atkCreature != null)
                yield return atkCreature.StartCoroutine(atkCreature.PlayAttackBump(0.35f, attackWindup));

            // Check target defense traits – may negate attack (after windup to still show attempt)
            bool negated = false;
            if (target.traits != null)
            {
                foreach (var tr in target.traits)
                {
                    if (tr != null && tr.TryNegateAttack(target, attacker)) { negated = true; break; }
                }
            }
            if (negated)
            {
                FeedbackManager.Instance?.ShowFloatingText("Blocked", target.transform.position, new Color(1f, 0.8f, 0.2f));
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(attacker.owner)} {attacker.name} attack negated by {target.name}");
                yield return new WaitForSeconds(statusEffectDelay);
                continue;
            }

			// Brief red flash on target
			if (target != null)
				yield return target.StartCoroutine(target.FlashDamage(0.12f));

            // Damage calculation
            int baseDmg = Mathf.Max(1, attacker.body - target.body + 1);
            if (attacker.traits != null)
            {
                foreach (var tr in attacker.traits) { if (tr != null) baseDmg = tr.ModifyOutgoingDamage(attacker, target, baseDmg); }
            }
            if (target.traits != null)
            {
                foreach (var tr in target.traits) { if (tr != null) baseDmg = tr.ModifyIncomingDamage(target, attacker, baseDmg); }
            }
            baseDmg = Mathf.Max(0, baseDmg);
            if (baseDmg > 0)
            {
                target.ApplyDamage(baseDmg, attacker);
                FeedbackManager.Instance?.ShowFloatingText($"-{baseDmg} HP", target.transform.position, new Color(1f, 0.3f, 0.3f));
            }

            // Remove target if dead
            if (target == null)
			{
                // target destroyed by ApplyDamage -> Kill, send death notifications
            }
            else if (target.currentHealth == 0)
            {
                // Notify attacker traits
                if (attacker.traits != null)
                {
                    foreach (var tr in attacker.traits) { if (tr != null) tr.OnAfterKill(attacker, target); }
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

    

    IEnumerator ResolveStarvationAndScoring()
	{
        var creatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);
        foreach (var c in creatures)
        {
            if (c == null) continue;
            // Food scoring for herbivores only
            if (c.data != null && c.data.type == CardType.Herbivore && c.eaten > 0)
            {
                int gain = c.eaten;
                ScoreManager.Add(c.owner, gain);
                FeedbackManager.Instance?.ShowFloatingText($"Score +{gain}", c.transform.position, Color.cyan);
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} scores {gain} from food");
                // small spacing before other texts for this creature
                yield return new WaitForSeconds(0.25f);
            }
            // Starvation chip if ate nothing
            if (c.eaten == 0)
            {
                // Cache position/name/owner in case the creature dies and is destroyed
                Vector3 pos = c.transform.position;
                string cname = c.name;
                var owner = c.owner;
                c.ApplyDamage(2, null);
                // allow damage text to show first
                yield return new WaitForSeconds(0.25f);
                FeedbackManager.Instance?.ShowFloatingText("Starved -2 HP", pos, Color.gray);
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(owner)} {cname} takes 2 starvation damage");
                // If the creature died, skip further per-creature steps
                if (c == null || c.currentHealth == 0)
                {
                    yield return new WaitForSeconds(starveDelay);
                    continue;
                }
            }
            // Fatigue if partially fed
            if (c.eaten > 0 && c.eaten < c.body)
            {
                c.fatigued = true;
                FeedbackManager.Instance?.ShowFloatingText("Fatigued", c.transform.position, Color.yellow);
                yield return new WaitForSeconds(0.25f);
            }
            if (c != null) c.eaten = 0;
            yield return new WaitForSeconds(starveDelay);
        }

        // Net damage scoring after processing food/starvation
        foreach (var c in AllCreatures())
        {
            if (c == null) continue;
            int net = Mathf.Max(0, c.roundDamageDealt - c.roundHealingUndone);
            if (net > 0)
            {
                ScoreManager.Add(c.owner, net);
                FeedbackManager.Instance?.ShowFloatingText($"Damage +{net}", c.transform.position, new Color(1f, 0.7f, 0.3f));
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} nets {net} from combat");
            }
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


