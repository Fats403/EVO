using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class AIManager : MonoBehaviour
{
    public static AIManager Instance;

    [Header("AI Deck & Hand")]
    [Tooltip("Optional card back visual if you later expose the AI hand/deck in the UI.")]
    public GameObject cardBackPrefab;

    // Logical AI deck/hand – mirrors DeckManager rules (deckSize, startingHandSize, etc.)
    private readonly List<ScriptableObject> drawPile = new List<ScriptableObject>();
    private readonly List<ScriptableObject> hand = new List<ScriptableObject>();

    [Header("UI")]
    [Tooltip("Text label showing the AI's remaining deck size.")]
    public TMP_Text deckCountText;

    [Tooltip("Text label showing the AI's hand size.")]
    public TMP_Text handCountText;

    [Header("Debugging")]
    [Tooltip("If true, the AI will log its hand and top-scoring actions each time it acts.")]
    public bool debugAI = false;

    [Tooltip("Maximum number of candidate actions to include in debug logs.")]
    [Range(1, 20)]
    public int debugMaxActionsToLog = 8;

    [Header("Heuristic Weights (Normal AI)")]
    [Tooltip("Value per creature size/body when considering plays.")]
    [Range(0f, 10f)]
    public float creatureSizeWeight = 1.5f;

    [Tooltip("Base value for playing any creature at all.")]
    [Range(0f, 10f)]
    public float creatureBaseValue = 2f;

    [Tooltip("Extra value for carnivores, since they threaten herbivores.")]
    [Range(0f, 10f)]
    public float carnivoreBonus = 2f;

    [Tooltip("Extra value for avians, reflecting mobility/harass potential.")]
    [Range(0f, 10f)]
    public float avianBonus = 1f;

    [Tooltip("Penalty per point of momentum cost when evaluating any action.")]
    [Range(0f, 5f)]
    public float momentumCostWeight = 0.8f;

    [Tooltip("Additional penalty factor when remaining momentum is low.")]
    [Range(0.5f, 4f)]
    public float lowMomentumPenalty = 1.2f;

    [Tooltip("Base value per allied target affected by a positive effect.")]
    [Range(0f, 5f)]
    public float allyEffectPerTargetValue = 1.5f;

    [Tooltip("Base value per enemy target affected by a negative effect.")]
    [Range(0f, 5f)]
    public float enemyEffectPerTargetValue = 1.75f;

    [Tooltip("Threshold below which the AI prefers to pass instead of playing.")]
    [Range(0f, 5f)]
    public float passThreshold = 0.1f;

    [Header("Board Development Preferences")]
    [Tooltip("Flat bonus to creature plays when the AI has no creatures on board.")]
    [Range(0f, 10f)]
    public float emptyBoardCreatureBonus = 4f;

    [Tooltip("Bonus per creature the enemy leads by when evaluating creature plays.")]
    [Range(0f, 5f)]
    public float boardDeficitCreatureBonusPerGap = 1.5f;

    [Tooltip("Multiplier applied to effect scores when the AI has no creatures on board.")]
    [Range(0f, 1f)]
    public float emptyBoardEffectMultiplier = 0.25f;

    [Tooltip(
        "Multiplier applied to effect scores when the AI has very few creatures (1 or fewer)."
    )]
    [Range(0f, 1f)]
    public float fewAlliesEffectMultiplier = 0.6f;

    [Header("Board Control")]
    [Tooltip(
        "Minimum total threat value of enemies before AI considers symmetric effects highly valuable."
    )]
    [Range(5f, 25f)]
    public float symmetricEffectThreatThreshold = 12f;

    [Tooltip(
        "How much AI weights its own board when evaluating symmetric effects (1.0 = equal, <1.0 = more willing to sacrifice)."
    )]
    [Range(0.3f, 1.5f)]
    public float ownBoardValueMultiplier = 0.7f;

    [Header("Deck Building")]
    [Tooltip("DraftConfig used to build balanced AI decks (same rules as player draft).")]
    public DraftConfig draftConfig;

    [Header("Effect Evaluation Weights")]
    [Tooltip(
        "Extra value per stack of negative status on an allied target when cleanse-synergy effects are used."
    )]
    [Range(0f, 5f)]
    public float allyNegativeStatusWeight = 1.0f;

    [Tooltip(
        "Extra value per stack of positive status on an enemy target when targeting them with effects."
    )]
    [Range(0f, 5f)]
    public float enemyPositiveStatusWeight = 1.0f;

    [Tooltip(
        "Bonus for targeting an ally that currently has at least one valid attack target (helps avoid wasting Rage-like buffs)."
    )]
    [Range(0f, 10f)]
    public float allyAttackOpportunityBonus = 2.0f;

    // Local AI view of which statuses are generally harmful/beneficial.
    // Mirrors StatusTagGroups.Negative/Positive so there's a single source of truth.
    static readonly StatusTag[] NegativeStatusTagsForAI = StatusTagGroups.Negative;

    static readonly StatusTag[] PositiveStatusTagsForAI = StatusTagGroups.Positive;

    void Awake()
    {
        Instance = this;
    }

    // Called by GameManager at game start so both players follow the same deck rules.
    public void BuildDeckAndDrawStartingHand()
    {
        BuildDeck();
        DrawStartingHand();
    }

    // Build an AI-owned logical deck that mirrors the same rules as the
    // player's draft (when a DraftConfig is provided). Falls back to a
    // simple random-unique deck if no config is assigned.
    void BuildDeck()
    {
        drawPile.Clear();
        hand.Clear();

        var dm = DeckManager.Instance;
        if (dm == null)
            return;

        var src = dm.allCards ?? new List<ScriptableObject>();

        if (draftConfig != null)
        {
            // Build a balanced deck using the same rules as the player draft.
            var built = BalancedDeckBuilder.BuildDeck(src, draftConfig);
            drawPile.AddRange(built);
        }
        else
        {
            // Fallback: simple random-unique deck if no config is assigned.
            var pool = new List<ScriptableObject>(src);

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j =
                    (GameManager.Instance != null)
                        ? GameManager.Instance.NextRandomInt(0, i + 1)
                        : Random.Range(0, i + 1);
                (pool[j], pool[i]) = (pool[i], pool[j]);
            }

            int deckSize = dm.deckSize > 0 ? dm.deckSize : pool.Count;
            var picked = new List<ScriptableObject>(deckSize);
            var seen = new HashSet<ScriptableObject>();
            for (int i = 0; i < pool.Count && picked.Count < deckSize; i++)
            {
                var card = pool[i];
                if (card == null)
                    continue;
                if (seen.Add(card))
                    picked.Add(card);
            }

            drawPile.AddRange(picked);
        }

        UpdateDeckUI();
        UpdateHandUI();
    }

    public int RemainingDeckCount => drawPile.Count;
    public int HandCount => hand.Count;

    int MaxHandSize
    {
        get
        {
            var dm = DeckManager.Instance;
            return dm != null && dm.maxHandSize > 0 ? dm.maxHandSize : 6;
        }
    }

    int StartingHandSize
    {
        get
        {
            var dm = DeckManager.Instance;
            return dm != null && dm.startingHandSize > 0 ? dm.startingHandSize : 3;
        }
    }

    int CardsPerRound
    {
        get
        {
            var dm = DeckManager.Instance;
            return dm != null && dm.cardsPerRound > 0 ? dm.cardsPerRound : 2;
        }
    }

    void DrawStartingHand()
    {
        int canDraw = Mathf.Max(0, MaxHandSize - hand.Count);
        int drawNow = Mathf.Min(StartingHandSize, canDraw);
        for (int i = 0; i < drawNow; i++)
        {
            DrawCardToHand();
        }

        UpdateHandUI();
    }

    ScriptableObject DrawCardData()
    {
        if (drawPile.Count == 0)
            return null;
        int last = drawPile.Count - 1;
        var c = drawPile[last];
        drawPile.RemoveAt(last);
        UpdateDeckUI();
        return c;
    }

    void DrawCardToHand()
    {
        if (hand.Count >= MaxHandSize)
            return;
        var data = DrawCardData();
        if (data != null)
        {
            hand.Add(data);
            UpdateHandUI();
        }
    }

    /// <summary>
    /// Public helper to draw a single card into the AI hand,
    /// respecting the current max hand size. Returns true if a
    /// card was actually drawn.
    /// </summary>
    public bool TryDrawOneCard()
    {
        int before = hand.Count;
        DrawCardToHand();
        return hand.Count > before;
    }

    // Public round-start entry used by GameManager.BeginDraw.
    public void DrawCardsForRoundStart()
    {
        int canDraw = Mathf.Max(0, MaxHandSize - hand.Count);
        int drawNow = Mathf.Min(CardsPerRound, canDraw);
        for (int i = 0; i < drawNow; i++)
        {
            DrawCardToHand();
        }
        UpdateHandUI();
    }

    void UpdateDeckUI()
    {
        if (deckCountText != null)
            deckCountText.text = RemainingDeckCount.ToString();
    }

    void UpdateHandUI()
    {
        if (handCountText != null)
            handCountText.text = HandCount.ToString();
    }

    enum AIActionType
    {
        Pass,
        PlayCreature,
        PlayEffect,
    }

    struct AIAction
    {
        public AIActionType type;
        public CreatureCard creatureCard;
        public BoardSlot creatureSlot;
        public EffectCard effectCard;
        public List<Creature> effectTargets;
        public float score;
    }

    public bool TryPlaySingleAction()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return false;

        // Clean out any nulls that might have slipped into the hand.
        hand.RemoveAll(h => h == null);

        // If we have no cards at all, we must pass.
        if (hand.Count == 0)
            return false;

        // Snapshot current board creature counts for board-aware heuristics and debug.
        var allCreaturesSnapshot = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.data != null && c.currentHealth > 0 && !c.isDying)
            .ToList();
        int allyCreatureCount = allCreaturesSnapshot.Count(c => c.owner == SlotOwner.Player2);
        int enemyCreatureCount = allCreaturesSnapshot.Count(c => c.owner == SlotOwner.Player1);

        var actions = new List<AIAction>();

        // Enumerate candidate creature plays.
        BuildCreatureActions(gm, actions, allyCreatureCount, enemyCreatureCount);

        // Enumerate candidate effect plays.
        BuildEffectActions(
            gm,
            actions,
            allyCreatureCount,
            enemyCreatureCount,
            allCreaturesSnapshot
        );

        // Always include an explicit pass option.
        actions.Add(new AIAction { type = AIActionType.Pass, score = 0f });

        if (actions.Count == 0)
            return false;

        var best = actions.OrderByDescending(a => a.score).First();

        if (debugAI)
        {
            DebugLogAIChoice(gm, actions, best, allyCreatureCount, enemyCreatureCount);
        }

        // If our best option is pass or the value is too low, signal pass to GameManager.
        if (best.type == AIActionType.Pass || best.score <= passThreshold)
            return false;

        switch (best.type)
        {
            case AIActionType.PlayCreature:
                return ExecuteCreatureAction(gm, best);
            case AIActionType.PlayEffect:
                return ExecuteEffectAction(gm, best);
            default:
                return false;
        }
    }

    void BuildCreatureActions(
        GameManager gm,
        List<AIAction> actions,
        int allyCreatureCount,
        int enemyCreatureCount
    )
    {
        var freeSlots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .Where(s => s != null && s.owner == SlotOwner.Player2 && !s.occupied)
            .ToList();
        if (freeSlots.Count == 0)
            return;

        var creatureCards = hand.OfType<CreatureCard>().ToList();
        if (creatureCards.Count == 0)
            return;

        int currentMomentum = gm.GetMomentum(SlotOwner.Player2);

        foreach (var card in creatureCards)
        {
            // Preview without spending momentum.
            if (!gm.CanPlayCreatureCardPreview(card, SlotOwner.Player2, out _))
                continue;

            int cost = gm.GetCreatureCost(card);
            if (cost < 0)
                cost = 0;

            foreach (var slot in freeSlots)
            {
                float baseValue = creatureBaseValue;
                baseValue += creatureSizeWeight * card.size;

                switch (card.type)
                {
                    case CardType.Carnivore:
                        baseValue += carnivoreBonus;
                        break;
                    case CardType.Avian:
                        baseValue += avianBonus;
                        break;
                }

                // Slightly favor more leftmost slots for readability.
                baseValue -= Mathf.Abs(slot.transform.position.x) * 0.05f;

                float momentumPenalty = cost * momentumCostWeight;
                if (currentMomentum <= cost)
                {
                    momentumPenalty *= lowMomentumPenalty;
                }

                float score = baseValue - momentumPenalty;

                // Board development preferences: strongly favor getting bodies down,
                // especially on an empty or losing board.
                score = AdjustCreatureScoreForBoard(score, allyCreatureCount, enemyCreatureCount);

                actions.Add(
                    new AIAction
                    {
                        type = AIActionType.PlayCreature,
                        creatureCard = card,
                        creatureSlot = slot,
                        score = score,
                    }
                );
            }
        }
    }

    void BuildEffectActions(
        GameManager gm,
        List<AIAction> actions,
        int allyCreatureCount,
        int enemyCreatureCount,
        List<Creature> allCreaturesSnapshot
    )
    {
        var effectCards = hand.OfType<EffectCard>().ToList();
        if (effectCards.Count == 0)
            return;

        int currentMomentum = gm.GetMomentum(SlotOwner.Player2);

        // Reuse the snapshot provided by TryPlaySingleAction to keep a consistent view.
        var allCreatures = allCreaturesSnapshot;

        foreach (var card in effectCards)
        {
            if (!gm.CanPlayEffectCardPreview(card, SlotOwner.Player2, out _))
                continue;

            int cost = Mathf.Max(0, card.momentumCost);
            float momentumPenalty = cost * momentumCostWeight;
            if (currentMomentum <= cost)
            {
                momentumPenalty *= lowMomentumPenalty;
            }

            // Global effects: no direct per-creature targets passed to GameManager; EffectsManager
            // handles them based on side/type filters. For evaluation, build a synthetic list of
            // affected creatures so ScoreEffectTargets / EvaluateSymmetricEffect can reason about
            // their impact.
            if (card.isGlobal)
            {
                // For symmetric globals (targetSide = Any), consider all creatures; the
                // internal scorer will route to EvaluateSymmetricEffect.
                List<Creature> evalTargets;
                if (card.targetSide == EffectTargetSide.Ally)
                {
                    evalTargets = allCreatures
                        .Where(c => c != null && c.owner == SlotOwner.Player2)
                        .ToList();
                }
                else if (card.targetSide == EffectTargetSide.Enemy)
                {
                    evalTargets = allCreatures
                        .Where(c => c != null && c.owner == SlotOwner.Player1)
                        .ToList();
                }
                else
                {
                    evalTargets = allCreatures.Where(c => c != null).ToList();
                }

                // Apply type filter when relevant so we approximate which bodies will
                // actually be affected by trait attachments / runtime logic.
                if (card.targetType != EffectTargetType.Any)
                {
                    evalTargets = evalTargets
                        .Where(c =>
                            c != null
                            && c.data != null
                            && (
                                (
                                    card.targetType == EffectTargetType.Herbivore
                                    && c.data.type == CardType.Herbivore
                                )
                                || (
                                    card.targetType == EffectTargetType.Carnivore
                                    && c.data.type == CardType.Carnivore
                                )
                                || (
                                    card.targetType == EffectTargetType.Avian
                                    && c.data.type == CardType.Avian
                                )
                            )
                        )
                        .ToList();
                }

                if (evalTargets.Count == 0)
                    continue;

                float baseValue = ScoreEffectTargets(
                    card,
                    evalTargets,
                    SlotOwner.Player2,
                    allCreatures
                );
                float score = baseValue - momentumPenalty;
                if (score <= 0f)
                    continue;

                // Board-aware adjustments for global effects (e.g., don't cast ally buffs with no board).
                score = AdjustEffectScoreForBoard(
                    card,
                    score,
                    allyCreatureCount,
                    enemyCreatureCount,
                    isGlobal: true
                );
                if (score <= 0f)
                    continue;

                actions.Add(
                    new AIAction
                    {
                        type = AIActionType.PlayEffect,
                        effectCard = card,
                        // Global resolution ignores explicit per-creature targets; EffectsManager
                        // derives targets from side/type filters. We pass an empty list here.
                        effectTargets = new List<Creature>(),
                        score = score,
                    }
                );

                continue;
            }

            // Non-global effects: build a small set of reasonable target lists.
            var validTargets = allCreatures.Where(c =>
                EffectsManager.Instance != null
                && EffectsManager.Instance.IsValidTarget(card, c, SlotOwner.Player2)
            );

            var allyTargets = validTargets.Where(c => c.owner == SlotOwner.Player2).ToList();
            var enemyTargets = validTargets.Where(c => c.owner == SlotOwner.Player1).ToList();

            // Single-target effects.
            if (card.targetCount == EffectTargetCount.One)
            {
                foreach (var tgt in validTargets)
                {
                    var list = new List<Creature> { tgt };
                    float baseValue = ScoreEffectTargets(
                        card,
                        list,
                        SlotOwner.Player2,
                        allCreatures
                    );
                    if (baseValue <= 0f)
                        continue;
                    float score = baseValue - momentumPenalty;
                    if (score <= 0f)
                        continue;

                    score = AdjustEffectScoreForBoard(
                        card,
                        score,
                        allyCreatureCount,
                        enemyCreatureCount,
                        isGlobal: false
                    );
                    if (score <= 0f)
                        continue;

                    actions.Add(
                        new AIAction
                        {
                            type = AIActionType.PlayEffect,
                            effectCard = card,
                            effectTargets = list,
                            score = score,
                        }
                    );
                }
            }
            // Many-select effects: choose up to maxTargets best candidates.
            else if (card.targetCount == EffectTargetCount.ManySelectUpToN)
            {
                int maxTargets = Mathf.Max(1, card.maxTargets);

                var ordered =
                    card.targetSide == EffectTargetSide.Enemy
                        ? enemyTargets.OrderByDescending(c =>
                            ScoreEffectTargets(
                                card,
                                new List<Creature> { c },
                                SlotOwner.Player2,
                                allCreatures
                            )
                        )
                        : allyTargets.OrderByDescending(c =>
                            ScoreEffectTargets(
                                card,
                                new List<Creature> { c },
                                SlotOwner.Player2,
                                allCreatures
                            )
                        );

                var picks = ordered.Take(maxTargets).Where(c => c != null).ToList();
                if (picks.Count == 0)
                    continue;

                float baseValue = ScoreEffectTargets(card, picks, SlotOwner.Player2, allCreatures);
                if (baseValue <= 0f)
                    continue;

                float score = baseValue - momentumPenalty;
                if (score <= 0f)
                    continue;

                score = AdjustEffectScoreForBoard(
                    card,
                    score,
                    allyCreatureCount,
                    enemyCreatureCount,
                    isGlobal: false
                );
                if (score <= 0f)
                    continue;

                actions.Add(
                    new AIAction
                    {
                        type = AIActionType.PlayEffect,
                        effectCard = card,
                        effectTargets = picks,
                        score = score,
                    }
                );
            }
            // AllValid: affect all valid targets at once.
            else if (card.targetCount == EffectTargetCount.AllValid)
            {
                var picks = validTargets.ToList();
                if (picks.Count == 0)
                    continue;

                float baseValue = ScoreEffectTargets(card, picks, SlotOwner.Player2, allCreatures);
                if (baseValue <= 0f)
                    continue;

                float score = baseValue - momentumPenalty;
                if (score <= 0f)
                    continue;

                score = AdjustEffectScoreForBoard(
                    card,
                    score,
                    allyCreatureCount,
                    enemyCreatureCount,
                    isGlobal: false
                );
                if (score <= 0f)
                    continue;

                actions.Add(
                    new AIAction
                    {
                        type = AIActionType.PlayEffect,
                        effectCard = card,
                        effectTargets = picks,
                        score = score,
                    }
                );
            }
        }
    }

    float ScoreEffectTargets(
        EffectCard card,
        List<Creature> targets,
        SlotOwner owner,
        List<Creature> allCreatures
    )
    {
        if (card == null || targets == null || targets.Count == 0)
            return 0f;

        float totalThreatValue = 0f;
        float allyStatusValue = 0f;
        float enemyStatusValue = 0f;
        float attackSynergyValue = 0f;
        float removalValue = 0f;

        foreach (var c in targets)
        {
            if (c == null || c.data == null)
                continue;

            bool isAlly = c.owner == owner;
            bool isEnemy = !isAlly;

            float threat = c.data.size * creatureSizeWeight + c.maxHealth * 0.25f;

            if (c.data.type == CardType.Carnivore)
                threat += carnivoreBonus;
            else if (c.data.type == CardType.Avian)
                threat += avianBonus;

            // Slight bonus if wounded for ally-targeted buffs (heals/protection),
            // and for low-HP enemies for enemy-targeted debuffs.
            if (isAlly && c.IsWounded)
                threat *= 1.1f;
            if (isEnemy && c.IsWounded)
                threat *= 1.15f;

            // If this effect is marked as improving body, slightly favor allies for whom
            // a bigger body is especially valuable (e.g., frontliners / carnivores).
            if (isAlly && card.aiBodyBuffMultiplier > 1f)
            {
                // Clamp how hard this can swing things by only partially applying the multiplier.
                float bodyFactor = Mathf.Lerp(1f, card.aiBodyBuffMultiplier, 0.5f);
                threat *= bodyFactor;
            }

            totalThreatValue += threat;

            // Removal value: extra scoring for effects that neutralize high-threat enemies.
            if (isEnemy && card.aiRemovalValue > 0f)
            {
                removalValue += threat * card.aiRemovalValue * 0.5f;
            }

            // Status-based value: assume negative statuses on allies and positive statuses on enemies
            // are high leverage for many common effects (cleanses, dispels, buffs).
            var tags = c.GetActiveStatusTags();
            int negativeStacks = tags.Count(t => IsNegativeStatusForAI(t));
            int positiveStacks = tags.Count(t => IsPositiveStatusForAI(t));

            if (isAlly && negativeStacks > 0 && allyNegativeStatusWeight > 0f)
            {
                // Scale by per-status weight and any card-specific cleanse synergy.
                float cleanseFactor = Mathf.Max(1f, card.aiCleanseSynergy);
                allyStatusValue += negativeStacks * allyNegativeStatusWeight * cleanseFactor;
            }

            if (isEnemy && positiveStacks > 0 && enemyPositiveStatusWeight > 0f)
            {
                enemyStatusValue += positiveStacks * enemyPositiveStatusWeight;
            }

            // Generic "don't waste offensive buffs" rule: if the card advertises attack synergy
            // and this ally can reasonably attack, give it a bump.
            if (isAlly && card.aiAttackSynergy > 0f && allyAttackOpportunityBonus > 0f)
            {
                if (HasPotentialAttackTarget(c))
                {
                    attackSynergyValue += allyAttackOpportunityBonus * card.aiAttackSynergy;
                }
            }
        }

        bool targetsEnemies = targets.Any(c => c != null && c.owner != owner);
        int targetCount = targets.Count(c => c != null);

        float perTarget = targetsEnemies ? enemyEffectPerTargetValue : allyEffectPerTargetValue;
        float countValue = targetCount * perTarget;

        // Special evaluation for symmetric global effects (affects both sides).
        // These need completely different logic since they hurt both players.
        if (card.isGlobal && card.targetSide == EffectTargetSide.Any)
        {
            return EvaluateSymmetricEffect(card, allCreatures, owner);
        }

        // For all other effects (including non-symmetric globals), use normal accumulated scoring.
        float finalValue =
            totalThreatValue
            + allyStatusValue
            + enemyStatusValue
            + attackSynergyValue
            + removalValue
            + countValue;

        return finalValue;
    }

    float EvaluateSymmetricEffect(EffectCard card, List<Creature> allCreatures, SlotOwner owner)
    {
        var enemies = allCreatures.Where(c => c != null && c.owner != owner).ToList();
        var allies = allCreatures.Where(c => c != null && c.owner == owner).ToList();

        // Calculate total threat value of each side.
        float enemyThreat = 0f;
        float allyThreat = 0f;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.data == null)
                continue;

            float threat = enemy.data.size * creatureSizeWeight + enemy.maxHealth * 0.25f;
            if (enemy.data.type == CardType.Carnivore)
                threat += carnivoreBonus;
            else if (enemy.data.type == CardType.Avian)
                threat += avianBonus;

            enemyThreat += threat;
        }

        foreach (var ally in allies)
        {
            if (ally == null || ally.data == null)
                continue;

            float threat = ally.data.size * creatureSizeWeight + ally.maxHealth * 0.25f;
            if (ally.data.type == CardType.Carnivore)
                threat += carnivoreBonus;
            else if (ally.data.type == CardType.Avian)
                threat += avianBonus;

            allyThreat += threat;
        }

        // Apply multiplier to own board (AI is more willing to sacrifice own creatures).
        allyThreat *= ownBoardValueMultiplier;

        // Net value: difference between what we affect on enemies vs allies.
        float netValue = enemyThreat - allyThreat;

        // Only consider symmetric effects highly valuable if enemy threat exceeds threshold.
        if (enemyThreat < symmetricEffectThreatThreshold)
        {
            // Heavily penalize symmetric effects against weak boards.
            netValue *= 0.15f;
        }

        // Additional consideration: if we have very few creatures, symmetric effect is less punishing.
        if (allies.Count <= 1)
        {
            netValue *= 1.3f; // Bonus for being behind on board
        }

        // If enemy has significantly more creatures, symmetric effect becomes more appealing.
        if (enemies.Count >= allies.Count + 2)
        {
            netValue *= 1.5f;
        }

        // High-cost symmetric effects should require proportionally more value to justify.
        float costScaling = 1f + (card.momentumCost * 0.1f);
        netValue /= costScaling;

        return Mathf.Max(0f, netValue);
    }

    static bool IsNegativeStatusForAI(StatusTag tag)
    {
        // Using Contains on a small static array; cost is negligible given small board sizes.
        return NegativeStatusTagsForAI.Contains(tag);
    }

    static bool IsPositiveStatusForAI(StatusTag tag)
    {
        return PositiveStatusTagsForAI.Contains(tag);
    }

    bool HasPotentialAttackTarget(Creature attacker)
    {
        if (attacker == null || attacker.data == null)
            return false;

        // Stunned creatures cannot act this round.
        if (attacker.HasStatus(StatusTag.Stun))
            return false;

        // Herbivores only attack if a trait explicitly allows it.
        if (attacker.data.type == CardType.Herbivore)
        {
            bool traitAllows =
                attacker.traits != null
                && attacker.traits.Any(t => t != null && t.CanAttack(attacker));
            if (!traitAllows)
                return false;
        }

        if (ResolutionManager.Instance == null)
            return false;

        // Reuse the same targeting logic ResolutionManager uses for normal attacks.
        var best = ResolutionManager.Instance.FindBestTarget(attacker);
        return best != null;
    }

    float AdjustCreatureScoreForBoard(float score, int allyCreatureCount, int enemyCreatureCount)
    {
        // Strongly favor playing the first creature onto an empty board.
        if (allyCreatureCount == 0)
        {
            score += emptyBoardCreatureBonus;
        }

        // If we're behind on board, increase the value of deploying more creatures.
        int boardGap = enemyCreatureCount - allyCreatureCount;
        if (boardGap > 0)
        {
            score += boardGap * boardDeficitCreatureBonusPerGap;
        }

        return score;
    }

    float AdjustEffectScoreForBoard(
        EffectCard card,
        float score,
        int allyCreatureCount,
        int enemyCreatureCount,
        bool isGlobal
    )
    {
        // Generic preference: when we have no or very few creatures, effects are usually worse than
        // simply developing the board, unless they are extremely high value.
        if (allyCreatureCount == 0)
        {
            score *= emptyBoardEffectMultiplier;
        }
        else if (allyCreatureCount <= 1)
        {
            score *= fewAlliesEffectMultiplier;
        }

        if (isGlobal)
        {
            // If this global is intended as a "catch-up" tool, de-prioritize it when we're not behind.
            if (card.aiPreferWhenBehindOnBoard && allyCreatureCount >= enemyCreatureCount)
            {
                score *= 0.4f;
            }

            // For ally-buff globals, require a minimum board presence before we consider them good.
            if (
                card.aiMinAlliesForBuffGlobals > 0
                && allyCreatureCount < card.aiMinAlliesForBuffGlobals
            )
            {
                score *= 0.2f;
            }
        }

        return score;
    }

    void DebugLogAIChoice(
        GameManager gm,
        List<AIAction> actions,
        AIAction best,
        int allyCreatureCount,
        int enemyCreatureCount
    )
    {
        if (!debugAI)
            return;

        var sb = new StringBuilder();
        int momentum = gm.GetMomentum(SlotOwner.Player2);

        sb.AppendLine("[AI] --- Decision ---");
        sb.AppendLine(
            $"Momentum: {momentum}, Hand: {hand.Count}, Deck: {RemainingDeckCount}, "
                + $"Allies: {allyCreatureCount}, Enemies: {enemyCreatureCount}"
        );

        // Hand summary
        sb.AppendLine("[AI] Hand:");
        foreach (var card in hand)
        {
            if (card is CreatureCard cc)
            {
                sb.AppendLine($"  Creature - {cc.cardName} (Type={cc.type}, Size={cc.size})");
            }
            else if (card is EffectCard ec)
            {
                sb.AppendLine(
                    $"  Effect   - {ec.effectName} (Cost={ec.momentumCost}, Global={ec.isGlobal}, "
                        + $"Cleanse={ec.aiCleanseSynergy}, AtkSync={ec.aiAttackSynergy}, "
                        + $"BodyBuff={ec.aiBodyBuffMultiplier}, Removal={ec.aiRemovalValue})"
                );
            }
        }

        // Action ranking
        sb.AppendLine("[AI] Candidate actions (top scored first):");
        var ordered = actions.OrderByDescending(a => a.score).ToList();
        int limit = Mathf.Clamp(debugMaxActionsToLog, 1, 20);
        for (int i = 0; i < ordered.Count && i < limit; i++)
        {
            var a = ordered[i];
            string label;
            switch (a.type)
            {
                case AIActionType.PlayCreature:
                    label =
                        $"PlayCreature - {a.creatureCard?.cardName ?? "null"} "
                        + $"to slot {a.creatureSlot?.name ?? "null"}";
                    break;
                case AIActionType.PlayEffect:
                    label =
                        $"PlayEffect  - {a.effectCard?.effectName ?? "null"} "
                        + $"(targets={a.effectTargets?.Count ?? 0})";
                    break;
                default:
                    label = "Pass";
                    break;
            }
            sb.AppendLine($"  [{i}] {label}, score={a.score:F2}");
        }

        // Highlight final choice.
        sb.AppendLine("[AI] Chosen action:");
        sb.AppendLine($"  Type={best.type}, Score={best.score:F2}");

        Debug.Log(sb.ToString());
    }

    bool ExecuteCreatureAction(GameManager gm, AIAction action)
    {
        var card = action.creatureCard;
        var slot = action.creatureSlot;
        if (card == null || slot == null)
            return false;

        string reason;
        if (!gm.CanPlayCreatureCard(card, SlotOwner.Player2, out reason))
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[AI] Cannot play {card.cardName}: {reason}");
            return false;
        }

        var creature =
            DeckManager.Instance != null ? DeckManager.Instance.SpawnCreature(card, slot) : null;

        if (creature == null)
            return false;
        hand.Remove(card);
        UpdateHandUI();
        return true;
    }

    bool ExecuteEffectAction(GameManager gm, AIAction action)
    {
        var card = action.effectCard;
        if (card == null)
            return false;

        var targets = action.effectTargets ?? new List<Creature>();

        string reason;
        bool ok = gm.TryPlayEffectCard(card, SlotOwner.Player2, targets, out reason);
        if (!ok)
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[AI] Cannot play effect {card.effectName}: {reason}");
            return false;
        }

        hand.Remove(card);
        UpdateHandUI();
        return true;
    }
}
