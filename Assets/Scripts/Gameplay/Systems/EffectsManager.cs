using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    public ResolutionManager resolutionManager;

    public IEnumerable<Creature> GetClosestAlliesWithinRadius(
        SlotOwner owner,
        Vector3 origin,
        float radius,
        int maxCount
    )
    {
        var baseCreatures =
            resolutionManager != null
                ? resolutionManager.AllCreatures()
                : DeterministicHelpers.GetAllCreaturesSorted();

        var allies = baseCreatures.Where(c => c != null && c.owner == owner);
        var ordered = DeterministicHelpers.OrderByDistanceWithTieBreaker(allies, origin);

        return ordered
            .Where(c => Vector3.SqrMagnitude(c.transform.position - origin) <= radius * radius)
            .Take(Mathf.Max(0, maxCount))
            .ToList();
    }

    public IEnumerable<Creature> PreviewAutoTargets(
        EffectCard card,
        SlotOwner owner,
        Vector3 origin,
        float radiusWorld
    )
    {
        if (card == null)
            return System.Array.Empty<Creature>();
        if (!card.multiSelect)
            return System.Array.Empty<Creature>();
        int count = Mathf.Max(0, card.maxTargets);
        float r = Mathf.Max(0f, radiusWorld);
        var baseCreatures =
            resolutionManager != null
                ? resolutionManager.AllCreatures()
                : DeterministicHelpers.GetAllCreaturesSorted();

        var candidates = baseCreatures.Where(c => c != null && IsValidTarget(card, c, owner));
        var ordered = DeterministicHelpers.OrderByDistanceWithTieBreaker(candidates, origin);

        return ordered
            .Where(c => Vector3.SqrMagnitude(c.transform.position - origin) <= r * r)
            .Take(count)
            .ToList();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool IsValidTarget(EffectCard card, Creature c, SlotOwner player)
    {
        return card != null && c != null && card.IsValidTarget(c, player);
    }

    public void PlayHitBounceOnCreatures(IEnumerable<Creature> creatures)
    {
        if (creatures == null)
            return;

        foreach (var c in creatures)
        {
            if (c == null || !c.gameObject.activeInHierarchy)
                continue;

            c.StartCoroutine(c.PlayEffectHitBounce(1.08f, 0.2f, 0.45f));
        }
    }

    public void PlayOnTargets(EffectCard card, IEnumerable<Creature> targets, SlotOwner player)
    {
        if (card == null || targets == null)
            return;

        // Materialize a list early so we can reuse it for trait attachment,
        // runtime effects, and feedback.
        var effectiveTargets = targets;

        // If card is global but also attaches traits to a side/type, derive targets now
        if (
            card.isGlobal
            && (card.traitsToAttachToTargets != null && card.traitsToAttachToTargets.Length > 0)
            && resolutionManager != null
        )
        {
            var all = resolutionManager.AllCreatures();
            var list = new List<Creature>();
            foreach (var c in all)
            {
                if (c == null || c.data == null)
                    continue;
                // For global cards, we still want to apply to all creatures matching side/type filters.
                bool ok = true;
                switch (card.targetSide)
                {
                    case EffectTargetSide.Ally:
                        ok = c.owner == player;
                        break;
                    case EffectTargetSide.Enemy:
                        ok = c.owner != player;
                        break;
                    case EffectTargetSide.Any:
                        ok = true;
                        break;
                }
                if (!ok)
                    continue;
                switch (card.targetType)
                {
                    case EffectTargetType.Herbivore:
                        ok = c.data.type == CardType.Herbivore;
                        break;
                    case EffectTargetType.Carnivore:
                        ok = c.data.type == CardType.Carnivore;
                        break;
                    case EffectTargetType.Avian:
                        ok = c.data.type == CardType.Avian;
                        break;
                    case EffectTargetType.Any:
                        ok = true;
                        break;
                }
                if (!ok)
                    continue;
                list.Add(c);
            }
            effectiveTargets = list;
        }

        var targetList = effectiveTargets.Where(t => t != null).ToList();

        foreach (var c in targetList)
        {
            // Attach traits
            if (card.traitsToAttachToTargets != null)
            {
                foreach (var tr in card.traitsToAttachToTargets)
                {
                    if (tr == null)
                        continue;
                    var inst = ScriptableObject.Instantiate(tr);
                    c.traits.Add(inst);
                    // Immediate application hook for status-driven effects
                    if (inst is EffectTraitBase etb)
                    {
                        etb.OnApply(c);
                        // If effect consumed itself immediately, remove it
                        if (etb.remainingRounds == 0 && c.traits.Contains(etb))
                        {
                            c.traits.Remove(etb);
                        }
                    }
                    c.RefreshStatsUI();
                }
            }

            // Visual feedback: a smooth scale/bob when this effect actually hits the creature,
            // unless this card has explicitly disabled the default hit-bounce.
            if (!card.suppressHitBounce && c.gameObject.activeInHierarchy)
            {
                c.StartCoroutine(c.PlayEffectHitBounce(1.08f, 0.2f, 0.45f));
            }
        }

        // Register global effect if any
        if (card.globalEffect != null)
        {
            if (resolutionManager == null)
            {
                Debug.LogWarning(
                    $"[EffectsManager] Cannot register global effect '{card.globalEffect.name}' - resolutionManager is null!"
                );
            }
            else
            {
                var ge = ScriptableObject.Instantiate(card.globalEffect);
                ge.owner = player;
                ge.suppressHitBounceFromSource = card.suppressHitBounce;
                Debug.Log($"[EffectsManager] Registering global effect: {ge.name} for {player}");
                resolutionManager.RegisterGlobalEffect(ge);
            }
        }

        // Custom runtime effect hook for bespoke logic over the final target set
        if (card.runtimeEffect != null && resolutionManager != null)
        {
            var re = ScriptableObject.Instantiate(card.runtimeEffect);
            re.Apply(targetList, player, resolutionManager);
        }

        // Feedback
        if (FeedbackManager.Instance != null)
        {
            string who = NetworkRoleHelper.IsLocalPlayer(player) ? "You" : "Opponent";
            FeedbackManager.Instance.Log($"[{who}] played {card.effectName}");
        }
    }
}
