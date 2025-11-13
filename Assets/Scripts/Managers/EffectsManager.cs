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
        var all =
            resolutionManager != null
                ? resolutionManager.AllCreatures()
                : FindObjectsByType<Creature>(FindObjectsSortMode.None);
        return all.Where(c => c != null && c.owner == owner)
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - origin))
            .Where(c => Vector3.SqrMagnitude(c.transform.position - origin) <= radius * radius)
            .Take(Mathf.Max(0, maxCount))
            .ToList();
    }

    public IEnumerable<Creature> PreviewAutoTargets(
        EffectCard card,
        SlotOwner owner,
        Vector3 origin
    )
    {
        if (card == null)
            return System.Array.Empty<Creature>();
        if (!card.multiSelect)
            return System.Array.Empty<Creature>();
        int count = Mathf.Max(0, card.maxTargets);
        float r = Mathf.Max(0f, card.multiSelectRadius);
        var all =
            resolutionManager != null
                ? resolutionManager.AllCreatures()
                : FindObjectsByType<Creature>(FindObjectsSortMode.None).AsEnumerable();
        return all.Where(c => c != null && IsValidTarget(card, c, owner))
            .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - origin))
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

    public void PlayOnTargets(EffectCard card, IEnumerable<Creature> targets, SlotOwner player)
    {
        if (card == null || targets == null)
            return;

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
            targets = list;
        }

        foreach (var c in targets.Where(t => t != null))
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
        }

        // Register global effect if any
        if (card.globalEffect != null && resolutionManager != null)
        {
            var ge = ScriptableObject.Instantiate(card.globalEffect);
            resolutionManager.RegisterGlobalEffect(ge);
        }

        // Feedback
        if (FeedbackManager.Instance != null)
        {
            string who = player == SlotOwner.Player1 ? "P1" : "P2";
            FeedbackManager.Instance.Log($"[{who}] played {card.effectName}");
        }
    }
}
