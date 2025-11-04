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
    [Tooltip("Global pacing multiplier for all waits (higher = slower)")]
    public float pacingMultiplier = 1.4f;

	public IEnumerator RevealAndResolveRound()
	{
		// Reveal pending cards into creatures
		RevealPendings();

		// Round start hooks
        foreach (var c in AllCreatures())
		{
			c.defendedThisRound = false;
            c.tempSpeedMod = 0;
            if (c.traits != null)
            {
                foreach (var t in c.traits) { t?.OnRoundStart(c); }
            }
            c.ResetRoundBookkeeping();
            c.RefreshStatsUI();
		}

		// Pre-herbivore trait steals (e.g., Peregrine)
		yield return StartCoroutine(ResolvePreHerbivoreSteals());

		// Resolve Herbivores eating
		yield return StartCoroutine(ResolveHerbivores());

        // Resolve Attacks (Carnivores and Avians)
        yield return StartCoroutine(ResolveAttacks());

        // Avian foraging then starvation and scoring
        yield return StartCoroutine(ResolveAvianForaging());
        yield return StartCoroutine(ResolveStarvationAndScoring());

		// Round end hooks
		foreach (var c in AllCreatures())
		{
			if (c.traits == null) continue;
			foreach (var t in c.traits) { if (t != null) t.OnRoundEnd(c); }
		}
		// Weather end-of-round effects
		if (WeatherManager.Instance != null)
		{
			WeatherManager.Instance.ApplyEndOfRoundEffects();
			yield return new WaitForSeconds(statusEffectDelay);
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
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .OrderByDescending(c => c.speed + c.tempSpeedMod - c.fatigueStacks + (c.traits?.Sum(t => t != null ? t.SpeedBonus(c) : 0) ?? 0));
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
                if (taken > 0)
                {
                    Debug.Log($"[PreEat] {c.name} stole {taken}.");
                    FeedbackManager.Instance?.ShowFloatingText($"Steal +{taken}", c.transform.position, new Color(0.8f, 0.9f, 0.3f));
                }
				if (taken > 0) yield return new WaitForSeconds(preStealDelay * pacingMultiplier);
            }
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
					yield return new WaitForSeconds(eatDelay * pacingMultiplier);
            }
		}
	}

    IEnumerator ResolveAttacks()
    {
        var acted = new HashSet<Creature>();
        while (true)
        {
            var attacker = AllCreatures().FirstOrDefault(c => c != null && !acted.Contains(c));
            if (attacker == null) break;
            if (attacker == null || attacker.data == null) continue;
            if (attacker.data.type == CardType.Herbivore)
            {
                bool traitAllows = attacker.traits != null && attacker.traits.Any(t => t != null && t.CanAttack(attacker));
                if (!traitAllows)
                {
                    acted.Add(attacker);
                    continue; // herbivores don't attack by default
                }
            }
            // Candidates: opponent, cannot target Carnivores
            var candidates = AllCreatures()
                .Where(c => c != null && c.data != null && c.owner != attacker.owner)
                .Where(c => c.data.type != CardType.Carnivore)
                .Where(c => IsValidTarget(attacker, c))
                .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - attacker.transform.position))
                .ToList();

            static bool IsValidTarget(Creature atk, Creature tgt)
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
            
            if (candidates.Count == 0)
            {
                acted.Add(attacker);
                continue;
            }
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
				yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
                acted.Add(attacker);
                continue;
            }

			// Brief red flash on target
			if (target != null)
				yield return target.StartCoroutine(target.FlashDamage(0.12f));

            // Determine if this is an avian harass (baseline poke)
            bool isAvian = attacker.data != null && attacker.data.type == CardType.Avian;
            bool targetIsCarnivore = target.data != null && target.data.type == CardType.Carnivore;
            bool faster = attacker.speed >= target.speed;
            int bodyBonus = 0;
            if (attacker.traits != null)
            {
                foreach (var tr in attacker.traits) { if (tr != null) bodyBonus += tr.PredatorBodyBonusForTargeting(attacker); }
            }
            int effAtkBody = attacker.body + bodyBonus;
            bool meetsBodyRule = effAtkBody >= target.body;

            // Avian harass: faster-than-target, not vs Carnivores, and doesn't meet body rule
            bool harass = isAvian && !targetIsCarnivore && faster && !meetsBodyRule;

            // Damage calculation
            int baseDmg = harass ? 1 : Mathf.Max(1, effAtkBody - target.body + 1);
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
                var dmgTag = harass ? "Harass" : "Hit";
                FeedbackManager.Instance?.ShowFloatingText($"-{baseDmg} HP ({dmgTag})", target.transform.position, new Color(1f, 0.3f, 0.3f));
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
			yield return new WaitForSeconds(attackResolvePause * pacingMultiplier);
            acted.Add(attacker);
        }
	}

    IEnumerator ResolveAvianForaging()
    {
        if (foodPile == null) yield break;
        bool any = false;
        foreach (var c in AllCreatures())
        {
            if (c == null || c.data == null) continue;
            if (c.data.type != CardType.Avian) continue;
            int need = Mathf.Max(0, c.body - c.eaten);
            if (need <= 0) continue;
            if (foodPile.count <= 0) continue;

            int taken = foodPile.Take(1);
            if (taken > 0)
            {
                c.eaten += taken;
                FeedbackManager.Instance?.ShowFloatingText("+1 food", c.transform.position, new Color(0.5f, 0.8f, 1f));
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} forages +1");
                any = true;
				yield return new WaitForSeconds(eatDelay * pacingMultiplier);
            }
        }
        if (!any) yield break;
    }

    IEnumerator ResolveStarvationAndScoring()
	{
        var creatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);
        foreach (var c in creatures)
        {
            if (c == null) continue;
            bool didAny = false;
            // Food scoring for herbivores only
            if (c.data != null && c.data.type == CardType.Herbivore && c.eaten > 0)
            {
                int gain = c.eaten;
                ScoreManager.Add(c.owner, gain);
                FeedbackManager.Instance?.ShowFloatingText($"Score +{gain}", c.transform.position, Color.cyan);
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} scores {gain} from food");
                // small spacing before other texts for this creature
				yield return new WaitForSeconds(0.5f * pacingMultiplier);
                didAny = true;
            }
            // Starvation chip if ate nothing
            if (c.eaten == 0)
            {
                // Cache position/name/owner in case the creature dies and is destroyed
                Vector3 pos = c.transform.position;
                string cname = c.name;
                var owner = c.owner;
				int starve = (WeatherManager.Instance != null) ? WeatherManager.Instance.GetStarvationDamageOrDefault(2) : 2;
				c.ApplyDamage(starve, null);
                // allow damage text to show first
                yield return new WaitForSeconds(0.5f);
				FeedbackManager.Instance?.ShowFloatingText($"Starved -{starve} HP", pos, Color.gray);
				FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(owner)} {cname} takes {starve} starvation damage");
                // If the creature died, skip further per-creature steps
                if (c == null || c.currentHealth == 0)
                {
					if (didAny) yield return new WaitForSeconds(starveDelay * pacingMultiplier);
                    continue;
                }
                didAny = true;
            }
            // Fatigue if partially fed
            if (c.eaten > 0 && c.eaten < c.body)
            {
                c.ApplyFatigue(1, true);
				yield return new WaitForSeconds(0.5f * pacingMultiplier);
                didAny = true;
            }
            if (c != null) c.eaten = 0;
			if (didAny) yield return new WaitForSeconds(starveDelay * pacingMultiplier);
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
            // End-of-round fatigue recovery: remove one stack
            if (c.fatigueStacks > 0)
            {
                c.fatigueStacks -= 1;
                c.RefreshStatsUI();
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


