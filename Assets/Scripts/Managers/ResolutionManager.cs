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

    // Queued animations that should fully complete during the start-of-round
    // reveal step before normal resolution (attacks, etc.) proceeds.
    private readonly Queue<IEnumerator> startOfRoundAnimations = new();

    [Header("Timing")]
    public float eatDelay = 0.4f;
    public float attackWindup = 0.2f;
    public float attackResolvePause = 0.3f;

    [Tooltip("Small pause after the attack phase completes before moving to the next phase.")]
    public float afterCarnivoreDelay = 0.3f;
    public float starveDelay = 0.3f;

    [Tooltip(
        "Per-creature delay used between individual start/end-of-round status and trait procs so they don't all fire at once."
    )]
    public float statusEffectDelay = 0.2f;

    [Tooltip(
        "Minimum time to pause after start-of-round statuses/global effects so players can read them."
    )]
    public float roundStartEffectPause = 2.0f;

    [Tooltip(
        "Minimum time to pause after end-of-round statuses/global effects so players can read them."
    )]
    public float roundEndEffectPause = 1.0f;

    [Tooltip("Global pacing multiplier for all waits (higher = slower)")]
    public float pacingMultiplier = 1.0f;

    [Header("VFX")]
    public GameObject attackVFX;
    public GameObject eatVFX;

    void Awake()
    {
        Instance = this;
    }

    public IEnumerator RevealAndResolveRound()
    {
        bool hadStartStatusOrGlobalEffects = false;

        // Round start hooks
        foreach (var c in AllCreatures())
        {
            bool thisCreatureDidStatus = false;
            bool thisCreatureDidTrait = false;

            // Status round-start ticks (e.g., Infected)
            if (c.TickStatusesAtRoundStart())
            {
                hadStartStatusOrGlobalEffects = true;
                thisCreatureDidStatus = true;
            }
            if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
            {
                var snapshot = c.traits != null ? c.traits.ToArray() : System.Array.Empty<Trait>();
                foreach (var t in snapshot)
                {
                    if (t != null)
                    {
                        t.OnRoundStart(c);
                        thisCreatureDidTrait = true;
                    }
                }
            }

            // Run any start-of-round animations enqueued by traits (e.g., swaps/moves)
            while (startOfRoundAnimations.Count > 0)
            {
                var anim = startOfRoundAnimations.Dequeue();
                if (anim != null)
                    yield return StartCoroutine(anim);
            }

            // Small per-creature delay after visible start-of-round status/trait procs so
            // multiple effects don't all fire at once.
            if ((thisCreatureDidStatus || thisCreatureDidTrait) && statusEffectDelay > 0f)
            {
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
            }

            // A start-of-round status (e.g., Infected) or trait may have killed this creature
            // and begun its fade/destroy sequence. Guard against calling methods on a now-
            // destroyed or dying object to avoid MissingReferenceExceptions.
            if (c != null && !c.isDying && c.currentHealth > 0)
            {
                c.ResetRoundBookkeeping();
                c.RefreshStatsUI();
            }
        }

        // Global effects: round start
        InvokeGlobal(g => g.OnRoundStart(this));
        if (activeGlobalEffects != null && activeGlobalEffects.Count > 0)
        {
            hadStartStatusOrGlobalEffects = true;
        }

        // Allow start-of-round statuses and global effects to be visually digested before moving on.
        float startPause = Mathf.Max(0f, roundStartEffectPause) * pacingMultiplier;
        if (startPause > 0f && hadStartStatusOrGlobalEffects)
            yield return new WaitForSeconds(startPause);

        // Mixed action phase: feeding and attacking interleaved by action priority + speed.
        yield return StartCoroutine(ResolveMixedActions());

        // Post-action scavenging, starvation and scoring
        yield return StartCoroutine(ResolveStarvationAndScoring());

        // Round end hooks
        // Apply end-of-round status ticks and per-creature trait hooks with a unified
        // pacing delay so they don't all fire at once.
        bool hadEndStatusOrGlobalEffects = false;
        float endPause = Mathf.Max(0f, roundEndEffectPause) * pacingMultiplier;

        var endRoundCreatures = AllCreatures().ToList();
        foreach (var c in endRoundCreatures)
        {
            if (c == null)
                continue;

            bool thisCreatureDidStatus = c.TickStatusesAtRoundEnd();
            bool thisCreatureDidTrait = false;

            if (thisCreatureDidStatus)
                hadEndStatusOrGlobalEffects = true;

            // A status tick may have killed this creature; skip trait hooks if it's now gone.
            if (c.currentHealth > 0 && !c.isDying && c.traits != null)
            {
                if (!c.HasStatus(StatusTag.Suppressed))
                {
                    var snapshot = c.traits.ToArray();
                    foreach (var t in snapshot)
                    {
                        if (t != null)
                        {
                            t.OnRoundEnd(c);
                            thisCreatureDidTrait = true;
                        }
                    }
                }
            }

            if (thisCreatureDidTrait)
                hadEndStatusOrGlobalEffects = true;

            // Single per-creature delay after any end-of-round status/trait activity.
            if ((thisCreatureDidStatus || thisCreatureDidTrait) && statusEffectDelay > 0f)
            {
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
            }
        }

        // Global effects: round end, then decrement lifetimes and prune
        InvokeGlobal(g => g.OnRoundEnd(this));
        if (activeGlobalEffects != null && activeGlobalEffects.Count > 0)
        {
            hadEndStatusOrGlobalEffects = true;
            for (int i = activeGlobalEffects.Count - 1; i >= 0; i--)
            {
                var ge = activeGlobalEffects[i];
                if (ge == null)
                {
                    activeGlobalEffects.RemoveAt(i);
                    continue;
                }
                ge.remainingRounds = Mathf.Max(0, ge.remainingRounds - 1);
                if (ge.remainingRounds == 0)
                    activeGlobalEffects.RemoveAt(i);
            }
        }
        // Weather end-of-round effects (paced via coroutine so alerts can appear
        // slightly before damage, e.g., Wildfire burn).
        if (WeatherManager.Instance != null)
        {
            bool weatherDidAny = false;
            yield return StartCoroutine(
                WeatherManager.Instance.ApplyEndOfRoundEffects(endPause, did => weatherDidAny = did)
            );
            if (weatherDidAny)
                hadEndStatusOrGlobalEffects = true;
        }

        // Brief pause after end-of-round statuses/global effects so players can see what happened,
        // but only if something actually happened (no random waits on empty rounds).
        if (endPause > 0f && hadEndStatusOrGlobalEffects)
            yield return new WaitForSeconds(endPause);
    }

    public IEnumerable<Creature> AllCreatures()
    {
        var q = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
            .OrderByDescending(c => GetEffectiveSpeed(c));
        // Deterministic tie-breaker: RNG-based shuffle for equals
        int Rand()
        {
            return GameManager.Instance != null
                ? GameManager.Instance.NextRandomInt(0, int.MaxValue)
                : UnityEngine.Random.Range(0, int.MaxValue);
        }
        return q.ThenBy(_ => Rand());
    }

    void InvokeGlobal(System.Action<GlobalEffectBase> call)
    {
        if (activeGlobalEffects == null || call == null)
            return;
        foreach (var ge in activeGlobalEffects)
        {
            if (ge == null)
                continue;
            call(ge);
        }
    }

    public void RegisterGlobalEffect(GlobalEffectBase effect)
    {
        if (effect == null)
            return;
        activeGlobalEffects ??= new System.Collections.Generic.List<GlobalEffectBase>();
        activeGlobalEffects.Add(effect);
        effect.OnPlay(this);
    }

    /// <summary>
    /// Traits that perform start-of-round movements/animations (e.g., swaps)
    /// can enqueue their coroutines here so the RevealAndResolveRound flow
    /// will wait for them to complete before moving on.
    /// </summary>
    public void EnqueueStartOfRoundAnimation(IEnumerator routine)
    {
        if (routine == null)
            return;
        startOfRoundAnimations.Enqueue(routine);
    }

    int GetActionPriority(Creature c)
    {
        if (c == null)
            return 0;
        if (c.HasStatus(StatusTag.Suppressed) || c.traits == null)
            return 0;
        int best = 0;
        var snap = c.traits.ToArray();
        foreach (var t in snap)
        {
            if (t == null)
                continue;
            best = Mathf.Max(best, t.ActionPriorityBonus(c));
        }
        return best;
    }

    public IEnumerable<Creature> AllCreaturesInActionOrder()
    {
        var q = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.currentHealth > 0 && !c.isDying);

        static int Rand()
        {
            return GameManager.Instance != null
                ? GameManager.Instance.NextRandomInt(0, int.MaxValue)
                : UnityEngine.Random.Range(0, int.MaxValue);
        }

        // Priority first, then speed.
        return q.OrderByDescending(c => GetActionPriority(c))
            .ThenByDescending(c => GetEffectiveSpeed(c))
            .ThenBy(_ => Rand());
    }

    IEnumerator ResolveMixedActions()
    {
        // Snapshot order at start of action phase.
        var order = AllCreaturesInActionOrder().ToList();
        bool any = false;

        foreach (var actor in order)
        {
            if (actor == null)
                continue;
            if (actor.isDying || actor.currentHealth <= 0)
                continue;
            if (actor.HasStatus(StatusTag.Stunned))
                continue;

            if (actor.data == null)
                continue;

            bool did = false;

            switch (actor.data.type)
            {
                case CardType.Herbivore:
                    yield return StartCoroutine(ResolveHerbivoreSingleAction(actor, r => did = r));
                    break;
                case CardType.Carnivore:
                case CardType.Avian:
                    yield return StartCoroutine(ResolveSingleAttackAction(actor, r => did = r));
                    break;
            }

            if (did)
                any = true;

            if (did && statusEffectDelay > 0f)
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
        }

        if (any && afterCarnivoreDelay > 0f)
            yield return new WaitForSeconds(afterCarnivoreDelay * pacingMultiplier);
    }

    IEnumerator ResolveHerbivoreSingleAction(Creature c, System.Action<bool> onComplete)
    {
        bool did = false;
        bool didForage = false;
        if (c == null || c.data == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Default herbivore behavior: forage if hungry and able. If it cannot/doesn't forage,
        // it may attack only if a trait explicitly allows attacking.
        bool canForage = !c.HasStatus(StatusTag.Stunned) && !c.HasStatus(StatusTag.NoForage);

        if (foodPile != null && canForage)
        {
            int need = Mathf.Max(0, GetEffectiveBody(c) - c.eaten);
            if (need > 0 && foodPile.count > 0)
            {
                int desired = need;
                if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                {
                    var snap = c.traits.ToArray();
                    foreach (var t in snap)
                    {
                        if (t == null)
                            continue;
                        desired = t.ModifyHerbivoreEatAmount(c, desired, foodPile);
                    }
                    desired = Mathf.Max(0, desired);
                }

                if (desired > 0 && foodPile.count > 0)
                {
                    c.RevealIfStealthed();

                    yield return StartCoroutine(
                        c.PlayEatAnimation(foodPile.transform.position, eatDelay * pacingMultiplier)
                    );

                    int taken = foodPile.Take(desired);
                    if (taken > 0)
                    {
                        c.eaten += taken;
                        did = true;
                        didForage = true;

                        if (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                        {
                            var eatSnap = c.traits.ToArray();
                            foreach (var t in eatSnap)
                            {
                                if (t != null)
                                    t.OnAfterEat(c, taken, foodPile);
                            }
                        }

                        if (eatVFX != null)
                            c.PlayVFX(eatVFX);

                        FeedbackManager.Instance?.ShowFloatingText(
                            $"+{taken} Food",
                            c.transform.position,
                            GameColorPalette.TextPositive
                        );
                    }
                }
            }
        }

        // Stampede-like effects: if this herbivore foraged and a trait allows it,
        // it may make a bonus attack immediately after eating.
        if (didForage && c.traits != null && !c.HasStatus(StatusTag.Suppressed))
        {
            bool allowBonus = c.traits.Any(t => t != null && t.AllowBonusAttackAfterForage(c));
            if (allowBonus)
            {
                bool attacked = false;
                yield return StartCoroutine(ResolveSingleAttackAction(c, r => attacked = r));
                did = did || attacked;
            }
        }

        if (!did)
        {
            // No forage happened; allow a trait-enabled herbivore attack.
            bool attacked = false;
            yield return StartCoroutine(ResolveSingleAttackAction(c, r => attacked = r));
            did = attacked;
        }

        onComplete?.Invoke(did);
    }

    IEnumerator ResolveSingleAttackAction(Creature attacker, System.Action<bool> onComplete)
    {
        bool did = false;
        if (attacker == null || attacker.data == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Stunned creatures cannot act.
        if (attacker.HasStatus(StatusTag.Stunned))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Herbivores only attack if a trait explicitly allows it.
        if (attacker.data.type == CardType.Herbivore)
        {
            bool traitAllows =
                attacker.traits != null
                && attacker.traits.Any(t => t != null && t.CanAttack(attacker));
            if (!traitAllows)
            {
                onComplete?.Invoke(false);
                yield break;
            }
        }

        // Candidates: opponent
        var enemies = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.data != null && c.currentHealth > 0 && !c.isDying)
            .Where(c => c.owner != attacker.owner);

        // Taunt: if any enemy has Taunt, restrict to only taunt targets (closest wins)
        var tauntTargets = enemies.Where(c => c.HasStatus(StatusTag.Taunt)).ToList();

        bool canTargetAny =
            !attacker.HasStatus(StatusTag.Suppressed)
            && attacker.traits != null
            && attacker.traits.Any(tr => tr != null && tr.CanTargetAny(attacker));

        bool isAvianAttacker = attacker.data.type == CardType.Avian;

        var basePool =
            (tauntTargets.Count > 0)
                ? tauntTargets.AsEnumerable()
                : (
                    (canTargetAny || isAvianAttacker)
                        ? enemies
                        : enemies.Where(c => c.data.type != CardType.Carnivore)
                );

        var candidates = basePool
            .Where(c => IsValidAttackTarget(attacker, c))
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - attacker.transform.position))
            .ToList();

        // Carnivore-vs-carnivore fallback as in the original loop.
        if (candidates.Count == 0 && attacker.data.type == CardType.Carnivore)
        {
            var carnivoreEnemies = enemies.Where(c => c.data.type == CardType.Carnivore);
            var tauntCarnivores = carnivoreEnemies
                .Where(c => c.HasStatus(StatusTag.Taunt))
                .ToList();
            var carniPool =
                (tauntCarnivores.Count > 0) ? tauntCarnivores.AsEnumerable() : carnivoreEnemies;
            candidates = carniPool
                .Where(c => IsValidAttackTarget(attacker, c, ignoreBodyRule: true))
                .OrderBy(c =>
                    Vector3.SqrMagnitude(c.transform.position - attacker.transform.position)
                )
                .ToList();
        }

        if (candidates.Count == 0)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        var target = candidates[0];

        // Allow attacker traits to override target selection (e.g., lowest HP)
        if (!attacker.HasStatus(StatusTag.Suppressed) && attacker.traits != null)
        {
            var snapChoose = attacker.traits.ToArray();
            foreach (var tr in snapChoose)
            {
                if (tr == null)
                    continue;
                var picked = tr.ChooseAttackTarget(attacker, candidates, target);
                if (picked != null && candidates.Contains(picked))
                {
                    target = picked;
                    break;
                }
            }
        }

        // Attack bump tween on creature (always perform to show attempted attack)
        yield return attacker.StartCoroutine(attacker.PlayAttackBump(0.35f, attackWindup));

        // Pre-hit reactions (trigger even if attack is later negated)
        if (target != null && !target.HasStatus(StatusTag.Suppressed) && target.traits != null)
        {
            var tgtPre = target.traits.ToArray();
            foreach (var tr in tgtPre)
            {
                tr?.OnTargetedByAttack(target, attacker);
            }
        }
        foreach (
            var ally in FindObjectsByType<Creature>(FindObjectsSortMode.None)
                .Where(x =>
                    x != null
                    && x.currentHealth > 0
                    && !x.isDying
                    && x.owner == target.owner
                    && x != target
                )
        )
        {
            if (!ally.HasStatus(StatusTag.Suppressed) && ally.traits != null)
            {
                var allySnap = ally.traits.ToArray();
                foreach (var tr in allySnap)
                {
                    tr?.OnAllyTargeted(ally, target, attacker);
                }
            }
        }

        if (attacker == null || attacker.currentHealth <= 0 || attacker.isDying)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Check target defense traits – may negate attack (after windup to still show attempt)
        bool negated = false;
        if (target != null && !target.HasStatus(StatusTag.Suppressed) && target.traits != null)
        {
            var tgtSnap2 = target.traits.ToArray();
            foreach (var tr in tgtSnap2)
            {
                if (tr != null && tr.TryNegateAttack(target, attacker))
                {
                    negated = true;
                    break;
                }
            }
        }

        if (negated)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                "Blocked",
                target.transform.position,
                GameColorPalette.TextWarning
            );
            did = true;

            // After-attack resolution callback for attacker traits (negated)
            if (attacker != null && attacker.traits != null)
            {
                var afterNeg = attacker.traits.ToArray();
                foreach (var tr in afterNeg)
                {
                    tr?.OnAfterAttackResolved(attacker, target, wasNegated: true);
                }
            }

            yield return new WaitForSeconds(attackResolvePause * pacingMultiplier);
            onComplete?.Invoke(did);
            yield break;
        }

        // Stealth: any attack attempt reveals the attacker.
        attacker.RevealIfStealthed();

        if (target != null)
            yield return target.StartCoroutine(target.FlashDamage(0.25f));

        bool isAvian = attacker.data.type == CardType.Avian;
        bool faster = GetEffectiveSpeed(attacker) >= GetEffectiveSpeed(target);
        bool harass = isAvian && faster;

        int baseDmg = harass
            ? 1
            : Mathf.Max(1, GetEffectiveBody(attacker) - GetEffectiveBody(target) + 1);
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
                foreach (var tr in atkSnapshot2)
                {
                    if (tr != null)
                        baseDmg = tr.ModifyOutgoingDamage(attacker, target, baseDmg);
                }
            }
        }
        if (
            !overridden
            && target != null
            && !target.HasStatus(StatusTag.Suppressed)
            && target.traits != null
        )
        {
            var tgtSnapshot = target.traits.ToArray();
            foreach (var tr in tgtSnapshot)
            {
                if (tr != null)
                    baseDmg = tr.ModifyIncomingDamage(target, attacker, baseDmg);
            }
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

        if (baseDmg > 0 && target != null)
        {
            int applied = target.ApplyDamage(baseDmg, attacker, attackVFX);
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP",
                    target.transform.position,
                    GameColorPalette.Damage
                );
                did = true;
            }
        }

        // Notify attacker traits about kills (used by some traits for bonus effects).
        // Kill credit for scoring is recorded in Creature.ApplyDamageInternal.
        if (
            target != null
            && target.currentHealth == 0
            && attacker != null
            && attacker.traits != null
        )
        {
            var atkSnapshot3 = attacker.traits.ToArray();
            foreach (var tr in atkSnapshot3)
            {
                tr?.OnAfterKill(attacker, target);
            }
        }

        // After-attack resolution callback for attacker traits (successful or zero-damage hit)
        if (attacker != null && attacker.traits != null)
        {
            var afterSnap = attacker.traits.ToArray();
            foreach (var tr in afterSnap)
            {
                tr?.OnAfterAttackResolved(attacker, target, wasNegated: false);
            }
        }

        yield return new WaitForSeconds(attackResolvePause * pacingMultiplier);
        onComplete?.Invoke(did);
    }

    IEnumerator ResolveStarvationAndScoring()
    {
        var creatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);

        static void ShowScoreBreakdown(Creature c, int gain, string label)
        {
            if (c == null || gain <= 0)
                return;

            string text = !string.IsNullOrEmpty(label)
                ? $"Score +{gain} ({label})"
                : $"Score +{gain}";

            FeedbackManager.Instance?.ShowFloatingText(
                text,
                c.transform.position,
                GameColorPalette.ScoreGain
            );
        }

        // --- Pre-Pass: Avian fallback scavenging from leftover pile (survival only, no VP) ---
        // After the mixed action phase, any avian that has not eaten may scavenge 1 remaining food
        // from the pile (if any) so they don't constantly starve. This grants no score by default.
        if (foodPile != null && foodPile.count > 0)
        {
            foreach (var c in creatures)
            {
                if (c == null || c.data == null)
                    continue;
                if (c.currentHealth <= 0 || c.isDying)
                    continue;
                if (c.data.type != CardType.Avian)
                    continue;
                if (c.HasStatus(StatusTag.NoForage))
                    continue;
                if (c.eaten > 0)
                    continue;
                if (foodPile.count <= 0)
                    break;

                int taken = foodPile.Take(1);
                if (taken > 0)
                {
                    c.eaten = Mathf.Max(c.eaten, 1);
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Scavenge +1 Food",
                        c.transform.position,
                        GameColorPalette.ScavengeGain
                    );
                    yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
                }
            }
        }

        // --- Pass 1: Apply starvation warnings/stacks/damage with a small delay per creature ---
        foreach (var c in creatures)
        {
            if (c == null)
                continue;
            if (c.isDying || c.currentHealth <= 0)
                continue;
            bool didEat = c.eaten > 0;
            bool isStarvable =
                c.data != null
                && (
                    c.data.type == CardType.Herbivore
                    || c.data.type == CardType.Avian
                    || c.data.type == CardType.Carnivore
                );

            bool didAnyStarveChange = false;
            bool starveKilled = false;
            // Starvation rules for all starvable creature types (Herbivore / Avian / Carnivore).
            // First stack is a warning (no damage). On subsequent rounds of not eating,
            // damage per round is (stacks - 1).
            if (isStarvable)
            {
                int prevStacks = c.GetStatus(StatusTag.Starvation);
                if (didEat)
                {
                    if (prevStacks > 0)
                    {
                        c.ClearStatus(StatusTag.Starvation);
                        didAnyStarveChange = true;
                    }
                }
                else
                {
                    c.AddStatus(StatusTag.Starvation, 1);
                    int stacksNow = c.GetStatus(StatusTag.Starvation);
                    FeedbackManager.Instance?.Log(
                        $"{FeedbackManager.TagOwner(c.owner)} {c.name} gains Starvation (x{stacksNow})"
                    );
                    didAnyStarveChange = true;
                }

                // End-of-round starvation damage: first stack is a warning, so only
                // stacks beyond the first deal damage.
                int stacksFinal = c.GetStatus(StatusTag.Starvation);
                int dmg = Mathf.Max(0, stacksFinal - 1);
                if (dmg > 0)
                {
                    int applied = c.ApplyDamage(dmg, null, null, "Starve");
                    if (applied > 0)
                    {
                        FeedbackManager.Instance?.ShowFloatingText(
                            $"-{applied} HP (Starve)",
                            c.transform.position,
                            GameColorPalette.Starvation
                        );
                    }
                    didAnyStarveChange = true;
                    if (c == null || c.currentHealth == 0)
                    {
                        starveKilled = true;
                        yield return new WaitForSeconds(starveDelay * pacingMultiplier);
                        continue;
                    }
                }
            }

            if (didAnyStarveChange && !starveKilled)
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
        }

        // --- Pass 2: Herbivore food scoring (alive-only, only if fully fed) ---
        foreach (var c in creatures)
        {
            if (c == null)
                continue;
            if (c.isDying || c.currentHealth <= 0)
                continue;

            if (c.data != null && c.data.type == CardType.Herbivore)
            {
                int need = Mathf.Max(1, GetEffectiveBody(c));
                if (c.eaten >= need)
                {
                    int gain = need;
                    ScoreManager.Instance?.Add(c.owner, gain);
                    ShowScoreBreakdown(c, gain, "Food");
                    yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
                }
            }
        }

        // --- Pass 3: Kill-based scoring (alive-only) + Carnivore survival bonus + Avian scavenge bonus ---
        foreach (var c in creatures)
        {
            if (c == null || c.data == null)
                continue;
            if (c.isDying || c.currentHealth <= 0)
                continue;

            // Kills: award victim-body points (only if killer survived to scoring).
            if (c.roundKillBody > 0)
            {
                int gain = Mathf.Max(0, c.roundKillBody);
                ScoreManager.Instance?.Add(c.owner, gain);
                ShowScoreBreakdown(c, gain, "Kills");
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
            }

            // Carnivore survival: +1 if fed (eaten > 0) and alive.
            if (c.data.type == CardType.Carnivore && c.eaten > 0)
            {
                ScoreManager.Instance?.Add(c.owner, 1);
                ShowScoreBreakdown(c, 1, "Survival");
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
            }

            // Avian scavenge point: +1 once per round if any enemy died while this avian was alive.
            if (c.data.type == CardType.Avian && c.roundHasScavengePoint)
            {
                ScoreManager.Instance?.Add(c.owner, 1);
                ShowScoreBreakdown(c, 1, "Scavenge");
                yield return new WaitForSeconds(statusEffectDelay * pacingMultiplier);
            }
        }

        // Cleanup: clear eaten counters for the next round.
        foreach (var c in creatures)
        {
            if (c != null)
                c.eaten = 0;
        }
    }

    public bool IsValidAttackTarget(Creature atk, Creature tgt, bool ignoreBodyRule = false)
    {
        if (atk == null || tgt == null || atk.data == null || tgt.data == null)
            return false;
        // Stealth blocks targeting
        if (tgt.HasStatus(StatusTag.Stealth))
            return false;
        // Taunt always valid
        if (tgt.HasStatus(StatusTag.Taunt))
            return true;
        bool isAvianAtk = atk.data.type == CardType.Avian;
        bool isCarnivoreAtk = atk.data.type == CardType.Carnivore;
        // Trait-level general gating
        if (!atk.HasStatus(StatusTag.Suppressed) && atk.traits != null)
        {
            foreach (var t in atk.traits.ToArray())
            {
                if (t != null && !t.CanTarget(atk, tgt))
                    return false;
            }
        }
        // Avian harass rules: Avians can harass any enemy type as long as they are at
        // least as fast as the target (or have a trait that ignores the speed gate).
        if (isAvianAtk)
        {
            bool ignoreSpeed =
                !atk.HasStatus(StatusTag.Suppressed)
                && atk.traits != null
                && atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
            if (!ignoreSpeed && GetEffectiveSpeed(atk) < GetEffectiveSpeed(tgt))
                return false;
            return true;
        }
        // If attacker has Stealth, ignore body rule for this attempt (but still respect
        // special Carnivore-vs-Avian speed gate below).
        if (atk.HasStatus(StatusTag.Stealth))
        {
            // Carnivore vs Avian speed gate still applies unless trait ignores it
            if (tgt.data.type == CardType.Avian && isCarnivoreAtk)
            {
                bool ignoreSpeed =
                    !atk.HasStatus(StatusTag.Suppressed)
                    && atk.traits != null
                    && atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
                if (!ignoreSpeed && GetEffectiveSpeed(atk) < GetEffectiveSpeed(tgt))
                    return false;
            }
            return true;
        }
        // Non-avian normal path
        if (tgt.data.type == CardType.Avian && isCarnivoreAtk)
        {
            bool ignoreSpeed =
                !atk.HasStatus(StatusTag.Suppressed)
                && atk.traits != null
                && atk.traits.Any(tr => tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt));
            if (!ignoreSpeed && GetEffectiveSpeed(atk) < GetEffectiveSpeed(tgt))
                return false;
        }
        // Body rule: by default, attacker can target same-body-or-smaller prey. Traits may
        // still override this (CanTargetAny / IgnoreBodySizeRequirement). Some callers
        // (e.g. Carnivore-vs-Carnivore fallback, reactive attacks) may request to ignore
        // the body rule entirely via ignoreBodyRule.
        bool traitIgnoresBody =
            !atk.HasStatus(StatusTag.Suppressed)
            && atk.traits != null
            && atk.traits.Any(tr => tr != null && tr.IgnoreBodySizeRequirement(atk, tgt));
        bool effectiveIgnoreBody = ignoreBodyRule || traitIgnoresBody;

        int effAtkBody = GetEffectiveBody(atk);
        int tgtBody = GetEffectiveBody(tgt);
        if (!effectiveIgnoreBody)
        {
            // Simple rule: can attack same size or smaller by default.
            if (effAtkBody >= tgtBody)
                return true;
        }
        else
        {
            // When ignoreBodyRule is true (e.g., Carnivore-vs-Carnivore fallback),
            // we have already respected stealth, taunt, trait gating, and any
            // special avian speed checks above, so we treat the target as valid
            // regardless of relative body size.
            return true;
        }

        // Trait allowing any targeting skips carnivore exclusion/body gate but not
        // stealth/taunt/speed-into-avian rule.
        bool canTargetAny =
            !atk.HasStatus(StatusTag.Suppressed)
            && atk.traits != null
            && atk.traits.Any(tr => tr != null && tr.CanTargetAny(atk));
        if (canTargetAny)
        {
            if (tgt.data.type == CardType.Avian && isCarnivoreAtk)
            {
                bool ignoreSpeed = atk.traits.Any(tr =>
                    tr != null && tr.IgnoreAvianSpeedRequirement(atk, tgt)
                );
                if (!ignoreSpeed && GetEffectiveSpeed(atk) < GetEffectiveSpeed(tgt))
                    return false;
            }
            return true;
        }
        return false;
    }

    public Creature FindBestTarget(Creature attacker)
    {
        if (attacker == null)
            return null;
        var enemies = AllCreatures()
            .Where(c => c != null && c.data != null && c.owner != attacker.owner);
        var tauntTargets = enemies.Where(c => c.HasStatus(StatusTag.Taunt)).ToList();
        bool canTargetAny =
            !attacker.HasStatus(StatusTag.Suppressed)
            && attacker.traits != null
            && attacker.traits.Any(tr => tr != null && tr.CanTargetAny(attacker));
        bool isAvianAttacker = attacker.data != null && attacker.data.type == CardType.Avian;
        var basePool =
            (tauntTargets.Count > 0)
                ? tauntTargets.AsEnumerable()
                : (
                    // For AI/util targeting, match the same rule as the mixed action phase:
                    // Avians may consider any enemy (including carnivores) as
                    // candidates; speed/other rules are enforced in IsValidAttackTarget.
                    (canTargetAny || isAvianAttacker)
                        ? enemies
                        : enemies.Where(c => c.data.type != CardType.Carnivore)
                );
        var candidates = basePool
            .Where(c => IsValidAttackTarget(attacker, c))
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - attacker.transform.position))
            .ToList();

        // If no valid non-carnivore (or unrestricted) targets exist, Carnivores will still
        // fight each other as a fallback, ignoring body size rules but respecting stealth,
        // taunt, and any special trait gating.
        if (
            candidates.Count == 0
            && attacker.data != null
            && attacker.data.type == CardType.Carnivore
        )
        {
            var carnivoreEnemies = enemies.Where(c => c.data.type == CardType.Carnivore);
            var tauntCarnivores = carnivoreEnemies
                .Where(c => c.HasStatus(StatusTag.Taunt))
                .ToList();
            var carniPool =
                (tauntCarnivores.Count > 0) ? tauntCarnivores.AsEnumerable() : carnivoreEnemies;
            candidates = carniPool
                .Where(c => IsValidAttackTarget(attacker, c, ignoreBodyRule: true))
                .OrderBy(c =>
                    Vector3.SqrMagnitude(c.transform.position - attacker.transform.position)
                )
                .ToList();
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    // Immediate single attack for reactive traits.
    // This now plays a short attack animation + hit flash so that
    // extra attacks are visually readable instead of "instant".
    public void PerformImmediateAttack(
        Creature attacker,
        Creature target,
        bool ignoreBodyRules = false,
        System.Action<bool> onComplete = null
    )
    {
        if (!isActiveAndEnabled)
        {
            // Fallback: if for some reason the ResolutionManager isn't active,
            // run the logic synchronously without animation.
            bool success = PerformImmediateAttackInternal(attacker, target, ignoreBodyRules);
            onComplete?.Invoke(success);
            return;
        }

        StartCoroutine(
            PerformImmediateAttackRoutine(attacker, target, ignoreBodyRules, onComplete)
        );
    }

    private IEnumerator PerformImmediateAttackRoutine(
        Creature attacker,
        Creature target,
        bool ignoreBodyRules,
        System.Action<bool> onComplete
    )
    {
        if (attacker == null || target == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }
        if (attacker.currentHealth <= 0 || target.currentHealth <= 0)
        {
            onComplete?.Invoke(false);
            yield break;
        }
        if (target.HasStatus(StatusTag.Stealth))
        {
            onComplete?.Invoke(false);
            yield break;
        }
        if (!ignoreBodyRules && !IsValidAttackTarget(attacker, target))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Simple bump on the attacker so reactive hits feel like real attacks.
        if (attacker != null)
        {
            yield return attacker.StartCoroutine(
                attacker.PlayAttackBump(0.35f, attackWindup * 0.7f)
            );
        }

        // Brief damage flash on the target.
        if (target != null)
        {
            yield return target.StartCoroutine(target.FlashDamage(0.18f));
        }

        bool success = PerformImmediateAttackInternal(attacker, target, ignoreBodyRules);
        onComplete?.Invoke(success);
        yield return new WaitForSeconds(attackResolvePause * 0.6f * pacingMultiplier);
    }

    // Core damage logic for immediate attacks (no animations).
    private bool PerformImmediateAttackInternal(
        Creature attacker,
        Creature target,
        bool ignoreBodyRules
    )
    {
        if (attacker == null || target == null)
            return false;
        if (attacker.currentHealth <= 0 || target.currentHealth <= 0)
            return false;
        if (target.HasStatus(StatusTag.Stealth))
            return false;

        if (!ignoreBodyRules && !IsValidAttackTarget(attacker, target))
            return false;

        bool isAvian = attacker.data != null && attacker.data.type == CardType.Avian;
        bool faster = GetEffectiveSpeed(attacker) >= GetEffectiveSpeed(target);
        int effAtkBody = GetEffectiveBody(attacker);
        bool harass = isAvian && faster;
        int baseDmg = harass ? 1 : Mathf.Max(1, effAtkBody - GetEffectiveBody(target) + 1);
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
            foreach (var tr in attacker.traits.ToArray())
            {
                if (tr != null)
                    baseDmg = tr.ModifyOutgoingDamage(attacker, target, baseDmg);
            }
        }
        if (!overridden && !target.HasStatus(StatusTag.Suppressed) && target.traits != null)
        {
            foreach (var tr in target.traits.ToArray())
            {
                if (tr != null)
                    baseDmg = tr.ModifyIncomingDamage(target, attacker, baseDmg);
            }
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
        if (baseDmg <= 0)
            return false;

        // Pass attackVFX here too and use the actual applied damage value
        // so feedback never exceeds the victim's remaining HP.
        int applied = target.ApplyDamage(baseDmg, attacker, attackVFX);
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP",
                target.transform.position,
                GameColorPalette.Damage
            );
        }
        if (attacker != null && attacker.data != null && attacker.data.type == CardType.Carnivore)
        {
            attacker.eaten = Mathf.Max(attacker.eaten, 1);
        }

        return applied > 0;
    }

    // --- Effective stat helpers ---
    public int GetEffectiveBody(Creature c)
    {
        if (c == null)
            return 0;
        int traitBody =
            (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                ? c.traits.Sum(t => t != null ? t.BodyBonus(c) : 0)
                : 0;
        int temp = c.GetStatus(StatusTag.BodyUp) - c.GetStatus(StatusTag.Malnourished);
        return c.body + temp + traitBody;
    }

    public int GetEffectiveSpeed(Creature c)
    {
        if (c == null)
            return 0;
        int traitSpeed =
            (!c.HasStatus(StatusTag.Suppressed) && c.traits != null)
                ? c.traits.Sum(t => t != null ? t.SpeedBonus(c) : 0)
                : 0;
        int temp = c.GetStatus(StatusTag.SpeedUp) - c.GetStatus(StatusTag.Fatigued);
        return c.speed + temp + traitSpeed;
    }
}
