using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
	public static ResolutionManager Instance { get; private set; }
	public FoodPile foodPile;
    [Header("Active Global Effects")]
    public List<GlobalEffectBase> activeGlobalEffects = new();
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

	void Awake()
	{
		Instance = this;
	}

	// --- Effective stat helpers ---
	int EffBody(Creature c)
	{
		if (c == null) return 0;
		int temp = c.GetStatus(StatusTag.BodyUp) - c.GetStatus(StatusTag.Malnourished);
		return c.body + temp;
	}
	int EffSpeed(Creature c)
	{
		if (c == null) return 0;
		int traitSpeed = (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
			? c.traits.Sum(t => t != null ? t.SpeedBonus(c) : 0)
			: 0;
		int temp = c.GetStatus(StatusTag.SpeedUp) - c.GetStatus(StatusTag.Fatigued);
		return c.speed + temp + traitSpeed;
	}

	public IEnumerator RevealAndResolveRound()
	{
		// Reveal pending cards into creatures
		RevealPendings();

        // Reset per-round trackers (none currently)

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
                EffSpeed(c)
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
			int need = Mathf.Max(0, EffBody(c) - c.eaten);
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
				// Notify eater traits
				if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
				{
					var eatSnap = c.traits.ToArray();
					foreach (var t in eatSnap) { if (t != null) t.OnAfterEat(c, taken, foodPile); }
				}
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
            // Candidates: opponent
            var enemies = AllCreatures()
                .Where(c => c != null && c.data != null && c.owner != attacker.owner);
            // Taunt: if any enemy has Taunt, restrict to only taunt targets (closest wins)
            var tauntTargets = enemies.Where(c => c.HasStatus(StatusTag.Taunt)).ToList();
            bool canTargetAny = !attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null &&
                attacker.traits.Any(tr => tr != null && tr.CanTargetAny(attacker));
            var basePool = (tauntTargets.Count > 0)
                ? tauntTargets.AsEnumerable()
                : (canTargetAny ? enemies : enemies.Where(c => c.data.type != CardType.Carnivore));
            var candidates = basePool
                .Where(c => IsValidAttackTarget(attacker, c))
                .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - attacker.transform.position))
                .ToList();
            
            if (candidates.Count == 0)
            {
                acted.Add(attacker);
                continue;
            }
			var target = candidates[0];
			// Allow attacker traits to override target selection (e.g., lowest HP)
			if (attacker != null && attacker.traits != null && !attacker.HasStatus(StatusTag.Suppressed))
			{
				var snapChoose = attacker.traits.ToArray();
				foreach (var tr in snapChoose)
				{
					if (tr == null) continue;
					var picked = tr.ChooseAttackTarget(attacker, candidates, target);
					if (picked != null && candidates.Contains(picked)) { target = picked; break; }
				}
			}
            // Attack bump tween on creature (always perform to show attempted attack)
            var atkCreature = attacker;
            if (atkCreature != null)
                yield return atkCreature.StartCoroutine(atkCreature.PlayAttackBump(0.35f, attackWindup));

            // Pre-hit reactions (trigger even if attack is later negated)
            if (target != null && !target.HasStatus(StatusTag.Suppressed) && target.traits != null)
            {
                var tgtPre = target.traits.ToArray();
                foreach (var tr in tgtPre) { if (tr != null) tr.OnTargetedByAttack(target, attacker); }
            }
            foreach (var ally in AllCreatures().Where(x => x != null && x.owner == target.owner && x != target))
            {
                if (!ally.HasStatus(StatusTag.Suppressed) && ally.traits != null)
                {
                    var allySnap = ally.traits.ToArray();
                    foreach (var tr in allySnap) { if (tr != null) tr.OnAllyTargeted(ally, target, attacker); }
                }
            }
            if (attacker == null || attacker.currentHealth == 0)
            {
                acted.Add(attacker);
                continue;
            }

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
				// After-attack resolution callback for attacker traits (negated)
				if (attacker != null && attacker.traits != null)
				{
					var afterNeg = attacker.traits.ToArray();
					foreach (var tr in afterNeg) { if (tr != null) tr.OnAfterAttackResolved(attacker, target, wasNegated: true); }
				}
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
            bool faster = EffSpeed(attacker) >= EffSpeed(target);
            int bodyBonus = 0;
            if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
            {
                var atkSnapshot = attacker.traits.ToArray();
                foreach (var tr in atkSnapshot) { if (tr != null) bodyBonus += tr.PredatorBodyBonusForTargeting(attacker); }
            }
            int effAtkBody = EffBody(attacker) + bodyBonus;
            bool meetsBodyRule = effAtkBody >= EffBody(target);

            // Avian harass: faster-than-target, not vs Carnivores
            bool harass = isAvian && !targetIsCarnivore && faster;

            // Damage calculation
            int baseDmg = harass ? 1 : Mathf.Max(1, effAtkBody - EffBody(target) + 1);
            bool overridden = false;
            if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
            {
                var atkSnapshot2 = attacker.traits.ToArray();
                foreach (var tr in atkSnapshot2)
                {
                    if (tr != null && tr.TryOverrideFinalDamage(attacker, target, out var fixedDmg))
                    {
                        baseDmg = Mathf.Max(0, fixedDmg);
                        overridden = true;
                        break;
                    }
                }
                if (!overridden)
                {
                    foreach (var tr in atkSnapshot2) { if (tr != null) baseDmg = tr.ModifyOutgoingDamage(attacker, target, baseDmg); }
                }
            }
            if (!overridden && !target.HasStatus(StatusTag.Suppressed) && target.traits != null)
            {
                var tgtSnapshot = target.traits.ToArray();
                foreach (var tr in tgtSnapshot) { if (tr != null) baseDmg = tr.ModifyIncomingDamage(target, attacker, baseDmg); }
            }
            if (!overridden)
            {
                // DamageUp adds flat damage
                baseDmg += Mathf.Max(0, attacker.GetStatus(StatusTag.DamageUp));
                // Rage doubles next damage, then clears
                if (attacker.HasStatus(StatusTag.Rage) && baseDmg > 0)
                {
                    baseDmg *= 2;
                    attacker.ClearStatus(StatusTag.Rage);
                }
            }
            baseDmg = Mathf.Max(0, baseDmg);
            if (baseDmg > 0)
            {
                target.ApplyDamage(baseDmg, attacker);
                var dmgTag = harass ? "Harass" : "Hit";
                FeedbackManager.Instance?.ShowFloatingText($"-{baseDmg} HP ({dmgTag})", target.transform.position, new Color(1f, 0.3f, 0.3f));
                // Carnivores count a successful damaging hit as "eating" for starvation purposes
                if (attacker != null && attacker.data != null && attacker.data.type == CardType.Carnivore)
                {
                    attacker.eaten = Mathf.Max(attacker.eaten, 1);
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
			}
			// After-attack resolution callback for attacker traits (successful or zero-damage hit)
			if (attacker != null && attacker.traits != null)
			{
				var afterSnap = attacker.traits.ToArray();
				foreach (var tr in afterSnap) { if (tr != null) tr.OnAfterAttackResolved(attacker, target, wasNegated: false); }
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
            int need = Mathf.Max(0, EffBody(c) - c.eaten);
            if (need <= 0) continue;
            if (foodPile.count <= 0) continue;
            if (c.HasStatus(StatusTag.Stunned) || c.HasStatus(StatusTag.NoForage)) continue;
            if (c.traits != null && c.traits.Any(t => t != null && !t.CanForage(c))) continue;

            int taken = foodPile.Take(1);
            if (taken > 0)
            {
                c.eaten += taken;
				// Notify eater traits
				if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
				{
					var eatSnap = c.traits.ToArray();
					foreach (var t in eatSnap) { if (t != null) t.OnAfterEat(c, taken, foodPile); }
				}
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
            bool didEat = c.eaten > 0;
            bool isAvianOrCarnivore = c.data != null && (c.data.type == CardType.Avian || c.data.type == CardType.Carnivore);

            // Food scoring for herbivores only
            if (c.data != null && c.data.type == CardType.Herbivore && didEat)
            {
                int gain = c.eaten;
                ScoreManager.Add(c.owner, gain);
                FeedbackManager.Instance?.ShowFloatingText($"Score +{gain}", c.transform.position, Color.cyan);
                FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} scores {gain} from food");
                didAny = true;
            }

            // New starvation rules for Avian/Carnivore
            if (isAvianOrCarnivore)
            {
                int prevStacks = c.GetStatus(StatusTag.Starvation);
                if (didEat)
                {
                    if (prevStacks > 0)
                    {
                        c.ClearStatus(StatusTag.Starvation);
                        c.ApplyFatigued(1);
                        FeedbackManager.Instance?.ShowFloatingText("Recovered (Fatigue +1)", c.transform.position, Color.yellow);
                        didAny = true;
                    }
                }
                else
                {
                    c.AddStatus(StatusTag.Starvation, 1);
                    int stacksNow = c.GetStatus(StatusTag.Starvation);
                    FeedbackManager.Instance?.ShowFloatingText($"Starving +{1} (x{stacksNow})", c.transform.position, Color.gray);
                    FeedbackManager.Instance?.Log($"{FeedbackManager.TagOwner(c.owner)} {c.name} gains Starvation (x{stacksNow})");
                    didAny = true;
					// Lethal at 3 stacks: apply lethal damage so death-preventing traits can trigger
                    if (stacksNow >= 3)
                    {
						int lethal = c.currentHealth;
						if (lethal > 0) c.ApplyDamage(lethal, null);
						if (c == null || c.currentHealth == 0)
						{
							yield return new WaitForSeconds(starveDelay * pacingMultiplier);
							continue;
						}
                    }
                }
                // End-of-round starvation damage equal to stacks
                int dmg = c.GetStatus(StatusTag.Starvation);
                if (dmg > 0)
                {
                    c.ApplyDamage(dmg, null);
                    FeedbackManager.Instance?.ShowFloatingText($"Starvation -{dmg} HP", c.transform.position, Color.gray);
                    didAny = true;
                    if (c == null || c.currentHealth == 0)
                    {
                        yield return new WaitForSeconds(starveDelay * pacingMultiplier);
                        continue;
                    }
                }
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

	public bool IsValidAttackTarget(Creature atk, Creature tgt)
	{
		if (atk == null || tgt == null || atk.data == null || tgt.data == null) return false;
		// Stealth blocks targeting
		if (tgt.HasStatus(StatusTag.Stealth)) return false;
		// Taunt always valid
		if (tgt.HasStatus(StatusTag.Taunt)) return true;
		bool isAvianAtk = atk.data.type == CardType.Avian;
		bool isCarnivoreAtk = atk.data.type == CardType.Carnivore;
		// Trait-level general gating
		if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
		{
			foreach (var t in atk.traits.ToArray())
			{
				if (t != null && !t.CanTarget(atk, tgt)) return false;
			}
		}
		// Avian harass rules
		if (isAvianAtk)
		{
			if (tgt.data.type == CardType.Carnivore) return false;
			bool ignoreSpeed = !atk.HasStatus(StatusTag.Suppressed) && atk.traits != null &&
				atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
			if (!ignoreSpeed && EffSpeed(atk) < EffSpeed(tgt)) return false;
			return true;
		}
		// If attacker has Stealth, ignore body rule for this attempt
		if (atk.HasStatus(StatusTag.Stealth))
		{
			// Carnivore vs Avian speed gate still applies unless trait ignores it
			if (tgt.data.type == CardType.Avian && isCarnivoreAtk)
			{
				bool ignoreSpeed = !atk.HasStatus(StatusTag.Suppressed) && atk.traits != null &&
					atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
				if (!ignoreSpeed && EffSpeed(atk) < EffSpeed(tgt)) return false;
			}
			return true;
		}
		// Non-avian normal path
		if (tgt.data.type == CardType.Avian && isCarnivoreAtk)
		{
			bool ignoreSpeed = !atk.HasStatus(StatusTag.Suppressed) && atk.traits != null &&
				atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
			if (!ignoreSpeed && EffSpeed(atk) < EffSpeed(tgt)) return false;
		}
		int bodyBonus = 0;
		if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
		{
			foreach (var t in atk.traits.ToArray())
			{
				if (t != null) bodyBonus += t.PredatorBodyBonusForTargeting(atk);
			}
		}
		int effAtkBody = EffBody(atk) + bodyBonus;
		if (EffBody(tgt) < effAtkBody) return true;
		if (EffBody(tgt) == effAtkBody)
		{
			if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
			{
				foreach (var t in atk.traits.ToArray())
				{
					if (t != null && t.CanTargetEqualBody(atk, tgt)) return true;
				}
			}
		}
		// Trait allowing any targeting skips carnivore exclusion/body gate but not stealth/taunt/speed-into-avian rule
		bool canTargetAny = !atk.HasStatus(StatusTag.Suppressed) && atk.traits != null &&
			atk.traits.Any(tr => tr != null && tr.CanTargetAny(atk));
		if (canTargetAny)
		{
			if (tgt.data.type == CardType.Avian && isCarnivoreAtk)
			{
				bool ignoreSpeed = atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
				if (!ignoreSpeed && EffSpeed(atk) < EffSpeed(tgt)) return false;
			}
			return true;
		}
		return false;
	}

	public Creature FindBestTarget(Creature attacker)
	{
		if (attacker == null) return null;
		var enemies = AllCreatures().Where(c => c != null && c.data != null && c.owner != attacker.owner);
		var tauntTargets = enemies.Where(c => c.HasStatus(StatusTag.Taunt)).ToList();
		bool canTargetAny = !attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null &&
			attacker.traits.Any(tr => tr != null && tr.CanTargetAny(attacker));
		var basePool = (tauntTargets.Count > 0)
			? tauntTargets.AsEnumerable()
			: (canTargetAny ? enemies : enemies.Where(c => c.data.type != CardType.Carnivore));
		var candidates = basePool
			.Where(c => IsValidAttackTarget(attacker, c))
			.OrderBy(c => Vector3.SqrMagnitude(c.transform.position - attacker.transform.position))
			.ToList();
		return candidates.Count > 0 ? candidates[0] : null;
	}

	// Immediate single attack for reactive traits
	public void PerformImmediateAttack(Creature attacker, Creature target, bool ignoreBodyRules = false)
	{
		if (attacker == null || target == null) return;
		if (attacker.currentHealth <= 0 || target.currentHealth <= 0) return;
		if (target.HasStatus(StatusTag.Stealth)) return;

		if (!ignoreBodyRules && !IsValidAttackTarget(attacker, target)) return;

		bool isAvian = attacker.data != null && attacker.data.type == CardType.Avian;
		bool targetIsCarnivore = target.data != null && target.data.type == CardType.Carnivore;
		bool faster = EffSpeed(attacker) >= EffSpeed(target);
		int bodyBonus = 0;
		if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
		{
			foreach (var tr in attacker.traits.ToArray()) { if (tr != null) bodyBonus += tr.PredatorBodyBonusForTargeting(attacker); }
		}
		int effAtkBody = EffBody(attacker) + bodyBonus;
		bool harass = isAvian && !targetIsCarnivore && faster;
		int baseDmg = harass ? 1 : Mathf.Max(1, effAtkBody - EffBody(target) + 1);
		// Try fixed-damage override first
		bool overridden = false;
		if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
		{
			foreach (var tr in attacker.traits.ToArray())
			{
				if (tr != null && tr.TryOverrideFinalDamage(attacker, target, out var fixedDmg))
				{
					baseDmg = Mathf.Max(0, fixedDmg);
					overridden = true;
					break;
				}
			}
		}
		if (!overridden && !attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
		{
			foreach (var tr in attacker.traits.ToArray()) { if (tr != null) baseDmg = tr.ModifyOutgoingDamage(attacker, target, baseDmg); }
		}
		if (!overridden && !target.HasStatus(StatusTag.Suppressed) && target.traits != null)
		{
			foreach (var tr in target.traits.ToArray()) { if (tr != null) baseDmg = tr.ModifyIncomingDamage(target, attacker, baseDmg); }
		}
		if (!overridden)
		{
			baseDmg += Mathf.Max(0, attacker.GetStatus(StatusTag.DamageUp));
			if (attacker.HasStatus(StatusTag.Rage) && baseDmg > 0)
			{
				baseDmg *= 2;
				attacker.ClearStatus(StatusTag.Rage);
			}
		}
		baseDmg = Mathf.Max(0, baseDmg);
		if (baseDmg > 0)
		{
			target.ApplyDamage(baseDmg, attacker);
			FeedbackManager.Instance?.ShowFloatingText($"-{baseDmg} HP", target.transform.position, new Color(1f, 0.3f, 0.3f));
			if (attacker != null && attacker.data != null && attacker.data.type == CardType.Carnivore)
			{
				attacker.eaten = Mathf.Max(attacker.eaten, 1);
			}
		}
	}
}