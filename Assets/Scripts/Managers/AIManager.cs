using System.Collections.Generic;
using System.Linq;
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

    [Header("Heuristic Weights (Normal AI)")]
    [Tooltip("Value per creature tier when considering plays.")]
    public float creatureTierWeight = 3f;

    [Tooltip("Value per creature size/body when considering plays.")]
    public float creatureSizeWeight = 1.5f;

    [Tooltip("Base value for playing any creature at all.")]
    public float creatureBaseValue = 2f;

    [Tooltip("Extra value for carnivores, since they threaten herbivores.")]
    public float carnivoreBonus = 2f;

    [Tooltip("Extra value for avians, reflecting mobility/harass potential.")]
    public float avianBonus = 1f;

    [Tooltip("Penalty per point of momentum cost when evaluating any action.")]
    public float momentumCostWeight = 1.0f;

    [Tooltip("Additional penalty factor when remaining momentum is low.")]
    public float lowMomentumPenalty = 1.5f;

    [Tooltip("Base value per allied target affected by a positive effect.")]
    public float allyEffectPerTargetValue = 1.5f;

    [Tooltip("Base value per enemy target affected by a negative effect.")]
    public float enemyEffectPerTargetValue = 1.75f;

    [Tooltip("Threshold below which the AI prefers to pass instead of playing.")]
    public float passThreshold = 0.2f;

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

    // Mirror of DeckManager.BuildDeck, but building an AI-owned logical deck.
    void BuildDeck()
    {
        drawPile.Clear();
        hand.Clear();

        var dm = DeckManager.Instance;
        if (dm == null)
            return;

        var src = dm.allCards ?? new List<ScriptableObject>();
        var pool = new List<ScriptableObject>(src);

        // Shuffle pool using the same RNG source as the rest of the game.
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

        // Shuffle draw order
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j =
                (GameManager.Instance != null)
                    ? GameManager.Instance.NextRandomInt(0, i + 1)
                    : Random.Range(0, i + 1);
            (drawPile[j], drawPile[i]) = (drawPile[i], drawPile[j]);
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

        var actions = new List<AIAction>();

        // Enumerate candidate creature plays.
        BuildCreatureActions(gm, actions);

        // Enumerate candidate effect plays.
        BuildEffectActions(gm, actions);

        // Always include an explicit pass option.
        actions.Add(new AIAction { type = AIActionType.Pass, score = 0f });

        if (actions.Count == 0)
            return false;

        var best = actions.OrderByDescending(a => a.score).First();

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

    void BuildCreatureActions(GameManager gm, List<AIAction> actions)
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
                baseValue += creatureTierWeight * card.tier;
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

    void BuildEffectActions(GameManager gm, List<AIAction> actions)
    {
        var effectCards = hand.OfType<EffectCard>().ToList();
        if (effectCards.Count == 0)
            return;

        int currentMomentum = gm.GetMomentum(SlotOwner.Player2);

        var allCreatures = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && c.data != null && c.currentHealth > 0 && !c.isDying)
            .ToList();

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

            // Global effects: no direct targets; evaluate based on how many creatures match filters.
            if (card.isGlobal)
            {
                var potentialTargets = allCreatures.Where(c =>
                    EffectsManager.Instance != null
                    && EffectsManager.Instance.IsValidTarget(card, c, SlotOwner.Player2)
                );

                int allyCount = potentialTargets.Count(c => c.owner == SlotOwner.Player2);
                int enemyCount = potentialTargets.Count(c => c.owner == SlotOwner.Player1);

                float baseValue =
                    allyCount * allyEffectPerTargetValue + enemyCount * enemyEffectPerTargetValue;

                float score = baseValue - momentumPenalty;
                if (score <= 0f)
                    continue;

                actions.Add(
                    new AIAction
                    {
                        type = AIActionType.PlayEffect,
                        effectCard = card,
                        effectTargets = new List<Creature>(), // handled as global by EffectsManager
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

            // Helper to score a list of targets.
            float ScoreTargetList(List<Creature> list)
            {
                if (list == null || list.Count == 0)
                    return 0f;

                float total = 0f;
                foreach (var c in list)
                {
                    if (c == null || c.data == null)
                        continue;

                    float threat =
                        c.data.tier * creatureTierWeight
                        + c.data.size * creatureSizeWeight
                        + c.maxHealth * 0.25f;

                    if (c.data.type == CardType.Carnivore)
                        threat += carnivoreBonus;
                    else if (c.data.type == CardType.Avian)
                        threat += avianBonus;

                    // Slight bonus if wounded for ally-targeted buffs (heals/protection),
                    // and for low-HP enemies for enemy-targeted debuffs.
                    if (c.owner == SlotOwner.Player2 && c.IsWounded)
                        threat *= 1.1f;
                    if (c.owner == SlotOwner.Player1 && c.IsWounded)
                        threat *= 1.15f;

                    total += threat;
                }

                bool targetsEnemies = list.Any(c => c.owner == SlotOwner.Player1);
                float perTarget = targetsEnemies
                    ? enemyEffectPerTargetValue
                    : allyEffectPerTargetValue;

                return total + list.Count * perTarget;
            }

            // Single-target effects.
            if (card.targetCount == EffectTargetCount.One)
            {
                foreach (var tgt in validTargets)
                {
                    var list = new List<Creature> { tgt };
                    float baseValue = ScoreTargetList(list);
                    if (baseValue <= 0f)
                        continue;
                    float score = baseValue - momentumPenalty;
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
                            ScoreTargetList(new List<Creature> { c })
                        )
                        : allyTargets.OrderByDescending(c =>
                            ScoreTargetList(new List<Creature> { c })
                        );

                var picks = ordered.Take(maxTargets).Where(c => c != null).ToList();
                if (picks.Count == 0)
                    continue;

                float baseValue = ScoreTargetList(picks);
                if (baseValue <= 0f)
                    continue;

                float score = baseValue - momentumPenalty;
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

                float baseValue = ScoreTargetList(picks);
                if (baseValue <= 0f)
                    continue;

                float score = baseValue - momentumPenalty;
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
