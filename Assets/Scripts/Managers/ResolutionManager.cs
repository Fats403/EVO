using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
	public FoodPile foodPile;
    [Header("Active Global Effects")]
    public System.Collections.Generic.List<GlobalEffectBase> activeGlobalEffects = new System.Collections.Generic.List<GlobalEffectBase>();
	[Header("Timing")]
    public float preStealDelay = 0.6f;
    public float eatDelay = 0.7f;
    public float attackWindup = 0.35f;
    public float attackResolvePause = 0.6f;
    public float afterCarnivoreDelay = 0.6f;
    public float starveDelay = 0.7f;
    public float statusEffectDelay = 0.6f;
    [Tooltip("Global pacing multiplier for all waits (higher = slower)")]
    public float pacingMultiplier = 1.75f;
    // Tracks last herbivore that ate during ResolveHerbivores this round
    Creature lastHerbivoreToEatThisRound = null;

	public IEnumerator RevealAndResolveRound()
	{
		// Reveal pending cards into creatures
		RevealPendings();

        // Reset per-round trackers
        lastHerbivoreToEatThisRound = null;

        // Round start hooks
        foreach (var c in AllCreatures())
        {
            // Status round-start ticks (e.g., Infected)
            c.TickStatusesAtRoundStart();
            if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
            {
                var snapshot = c.traits != null ? c.traits.ToArray() : System.Array.Empty<Trait>();
                foreach (var t in snapshot) { if (t != null) t.OnRoundStart(c); }
            }
            c.ResetRoundBookkeeping();
            c.RefreshStatsUI();
		}

        // Global effects: round start
        InvokeGlobal(g => g.OnRoundStart(this));

        // Pre-herbivore trait steals (e.g., Peregrine)
        InvokeGlobal(g => g.OnPreHerbivore(this));
		yield return StartCoroutine(ResolvePreHerbivoreSteals());

        // Resolve Herbivores eating
        InvokeGlobal(g => g.OnHerbivores(this));
		yield return StartCoroutine(ResolveHerbivores());

        // Resolve Attacks (Carnivores and Avians)
        yield return StartCoroutine(ResolveAttacks());

        // Avian foraging then starvation and scoring
        InvokeGlobal(g => g.OnForaging(this));
        yield return StartCoroutine(ResolveAvianForaging());
        yield return StartCoroutine(ResolveStarvationAndScoring());

        // Round end hooks
        // First apply status round-end ticks (fatigue decay, regen, bleeding, etc.)
        foreach (var c in AllCreatures())
        {
            if (c == null) continue;
            c.TickStatusesAtRoundEnd();
        }
        foreach (var c in AllCreatures())
		{
            if (c.traits == null) continue;
            if (!c.HasStatus(StatusTag.Suppressed))
            {
                var snapshot = c.traits != null ? c.traits.ToArray() : System.Array.Empty<Trait>();
                foreach (var t in snapshot) { if (t != null) t.OnRoundEnd(c); }
            }
		}
        // Global effects: round end, then decrement lifetimes and prune
        InvokeGlobal(g => g.OnRoundEnd(this));
        if (activeGlobalEffects != null && activeGlobalEffects.Count > 0)
        {
            for (int i = activeGlobalEffects.Count - 1; i >= 0; i--)
            {
                var ge = activeGlobalEffects[i];
                if (ge == null) { activeGlobalEffects.RemoveAt(i); continue; }
                ge.remainingRounds = Mathf.Max(0, ge.remainingRounds - 1);
                if (ge.remainingRounds == 0) activeGlobalEffects.RemoveAt(i);
            }
        }
		// Weather end-of-round effects
		if (WeatherManager.Instance != null)
		{
			WeatherManager.Instance.ApplyEndOfRoundEffects();
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

    public IEnumerable<Creature> AllCreatures()
    {
        var q = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .OrderByDescending(c =>
                c.speed
                - c.GetStatus(StatusTag.Fatigued)
                + ((!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                    ? c.traits.Sum(t => t != null ? t.SpeedBonus(c) : 0)
                    : 0)
            );
        // Deterministic tie-breaker: RNG-based shuffle for equals
        int Rand()
        {
            return GameManager.Instance != null ? GameManager.Instance.NextRandomInt(0, int.MaxValue) : UnityEngine.Random.Range(0, int.MaxValue);
        }
        return q.ThenBy(_ => Rand());
    }

    void InvokeGlobal(System.Action<GlobalEffectBase> call)
    {
        if (activeGlobalEffects == null || call == null) return;
        foreach (var ge in activeGlobalEffects)
        {
            if (ge == null) continue;
            call(ge);
        }
    }

    public void RegisterGlobalEffect(GlobalEffectBase effect)
    {
        if (effect == null) return;
        activeGlobalEffects ??= new System.Collections.Generic.List<GlobalEffectBase>();
        activeGlobalEffects.Add(effect);
        effect.OnPlay(this);
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
            if (c.HasStatus(StatusTag.Stunned) || c.HasStatus(StatusTag.NoForage)) continue;
			int need = Mathf.Max(0, c.body - c.eaten);
			if (need <= 0) continue;
			int desired = need;
            if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
			{
                var snap = c.traits.ToArray();
                foreach (var t in snap)
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
                // Record as last herbivore that ate this round
                lastHerbivoreToEatThisRound = c;
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
            // Stunned creatures cannot act this round
            if (attacker.HasStatus(StatusTag.Stunned)) { acted.Add(attacker); continue; }
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
                if (atk == null || tgt == null || atk.data == null || tgt.data == null) return false;

                // Effective speed considers fatigue and trait bonuses (mirrors ordering/UI logic)
                static int EffSpeed(Creature c)
                {
                    int traitSpeed = (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                        ? c.traits.Sum(t => t != null ? t.SpeedBonus(c) : 0)
                        : 0;
                    return c.speed - c.GetStatus(StatusTag.Fatigued) + traitSpeed;
                }

                // Avian attackers: speed-based harass targeting against non-carnivores
                if (atk.data.type == CardType.Avian)
                {
                    // Global filter already excludes carnivores, but keep guard for clarity
                    if (tgt.data.type == CardType.Carnivore) return false;
                    if (EffSpeed(atk) < EffSpeed(tgt)) return false;
                    // Respect attacker trait-based gating (e.g., camouflage/line-of-sight)
                    if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
                    {
                        var atkSnap = atk.traits.ToArray();
                        foreach (var t in atkSnap)
                        {
                            if (t != null && !t.CanTarget(atk, tgt)) return false;
                        }
                    }
                    return true;
                }

                // Non-avian attackers (Carnivores, or herbivores with attack traits):
                // Carnivore vs Avian requires speed >= target
                if (tgt.data.type == CardType.Avian && atk.data.type == CardType.Carnivore)
                {
                    if (EffSpeed(atk) < EffSpeed(tgt)) return false;
                }
                int bodyBonus = 0;
                if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
                {
                    var atkSnap = atk.traits.ToArray();
                    foreach (var t in atkSnap)
                    {
                        if (t != null) bodyBonus += t.PredatorBodyBonusForTargeting(atk);
                    }
                }
                int effAtkBody = atk.body + bodyBonus;
                if (tgt.body < effAtkBody) return true;
                if (tgt.body == effAtkBody)
                {
                    // allow equal-body if any trait grants it
                    if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
                    {
                        var atkSnap2 = atk.traits.ToArray();
                        foreach (var t in atkSnap2)
                        {
                            if (t != null && t.CanTargetEqualBody(atk, tgt)) return true;
                        }
                    }
                }
                // Additional target gating via traits (e.g., camouflage)
                if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
                {
                    var atkSnap3 = atk.traits.ToArray();
                    foreach (var t in atkSnap3)
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
            if (!target.HasStatus(StatusTag.Suppressed) && target.traits != null)
            {
                var tgtSnap2 = target.traits.ToArray();
                foreach (var tr in tgtSnap2)
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

            // Stealth: consumed on first attack attempt
            if (attacker.HasStatus(StatusTag.Stealth))
            {
                attacker.ClearStatus(StatusTag.Stealth);
                FeedbackManager.Instance?.ShowFloatingText("Revealed", attacker.transform.position, new Color(0.8f, 0.8f, 0.8f));
            }

			// Brief red flash on target
			if (target != null)
				yield return target.StartCoroutine(target.FlashDamage(0.25f));

            // Determine if this is an avian harass (baseline poke)
            bool isAvian = attacker.data != null && attacker.data.type == CardType.Avian;
            bool targetIsCarnivore = target.data != null && target.data.type == CardType.Carnivore;
            int AttkEffSpeed()
            {
                int traitSpeed = (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
                    ? attacker.traits.Sum(t => t != null ? t.SpeedBonus(attacker) : 0)
                    : 0;
                return attacker.speed - attacker.GetStatus(StatusTag.Fatigued) + traitSpeed;
            }
            int TgtEffSpeed()
            {
                int traitSpeed = (!target.HasStatus(StatusTag.Suppressed) && target.traits != null)
                    ? target.traits.Sum(t => t != null ? t.SpeedBonus(target) : 0)
                    : 0;
                return target.speed - target.GetStatus(StatusTag.Fatigued) + traitSpeed;
            }
            bool faster = AttkEffSpeed() >= TgtEffSpeed();
            int bodyBonus = 0;
            if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
            {
                var atkSnapshot = attacker.traits.ToArray();
                foreach (var tr in atkSnapshot) { if (tr != null) bodyBonus += tr.PredatorBodyBonusForTargeting(attacker); }
            }
            int effAtkBody = attacker.body + bodyBonus;
            bool meetsBodyRule = effAtkBody >= target.body;

            // Avian harass: faster-than-target, not vs Carnivores
            bool harass = isAvian && !targetIsCarnivore && faster;

            // Damage calculation
            int baseDmg = harass ? 1 : Mathf.Max(1, effAtkBody - target.body + 1);
            if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
            {
                var atkSnapshot2 = attacker.traits.ToArray();
                foreach (var tr in atkSnapshot2) { if (tr != null) baseDmg = tr.ModifyOutgoingDamage(attacker, target, baseDmg); }
            }
            if (!target.HasStatus(StatusTag.Suppressed) && target.traits != null)
            {
                var tgtSnapshot = target.traits.ToArray();
                foreach (var tr in tgtSnapshot) { if (tr != null) baseDmg = tr.ModifyIncomingDamage(target, attacker, baseDmg); }
            }
            // DamageUp adds flat damage
            baseDmg += Mathf.Max(0, attacker.GetStatus(StatusTag.DamageUp));
            baseDmg = Mathf.Max(0, baseDmg);
            // Rage doubles next damage, then clears
            if (attacker.HasStatus(StatusTag.Rage) && baseDmg > 0)
            {
                baseDmg *= 2;
                attacker.ClearStatus(StatusTag.Rage);
            }
            if (baseDmg > 0)
            {
                target.ApplyDamage(baseDmg, attacker);
                var dmgTag = harass ? "Harass" : "Hit";
                FeedbackManager.Instance?.ShowFloatingText($"-{baseDmg} HP ({dmgTag})", target.transform.position, new Color(1f, 0.3f, 0.3f));
                // Special: last herbivore to eat this round applies Bleeding on its first attack
                if (attacker != null && attacker == lastHerbivoreToEatThisRound)
                {
                    target.ApplyBleeding(1);
                    FeedbackManager.Instance?.ShowFloatingText("Bleeding (Stalked)", target.transform.position, new Color(0.9f, 0.1f, 0.1f));
                    // Consume so it doesn't apply multiple times if it attacks again
                    lastHerbivoreToEatThisRound = null;
                }
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
                    var atkSnapshot3 = attacker.traits.ToArray();
                    foreach (var tr in atkSnapshot3) { if (tr != null) tr.OnAfterKill(attacker, target); }
                }
                // Notify all about death (for scavengers)
                foreach (var c2 in AllCreatures())
                {
                    if (c2 == null || c2 == target) continue;
                    if (c2.traits == null) continue;
                    var trSnapshot = c2.traits.ToArray();
                    foreach (var tr in trSnapshot) { if (tr != null) tr.OnAnyDeath(c2, target); }
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
            if (c.HasStatus(StatusTag.Stunned) || c.HasStatus(StatusTag.NoForage)) continue;
            if (c.traits != null && c.traits.Any(t => t != null && !t.CanForage(c))) continue;

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
                c.ApplyFatigued(1);
                didAny = true;
                FeedbackManager.Instance?.ShowFloatingText("Fatigued", c.transform.position, Color.yellow);
            }
            if (didAny) yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
            if (c != null) c.eaten = 0;
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
		yield break;
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