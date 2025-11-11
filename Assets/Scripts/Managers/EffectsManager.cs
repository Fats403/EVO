using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    public ResolutionManager resolutionManager;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool CanPayCosts(EffectCard card)
    {
        if (card == null) return false;
        if (!card.costOneFood) return true;
        return resolutionManager != null && resolutionManager.foodPile != null && resolutionManager.foodPile.count > 0;
    }

    public void PayCosts(EffectCard card)
    {
        if (card == null || resolutionManager == null || resolutionManager.foodPile == null) return;
        if (card.costOneFood)
        {
            resolutionManager.foodPile.Take(1);
        }
    }

    public bool IsValidTarget(EffectCard card, Creature c, SlotOwner player)
    {
        return card != null && c != null && card.IsValidTarget(c, player);
    }

    public void PlayOnTargets(EffectCard card, IEnumerable<Creature> targets, SlotOwner player)
    {
        if (card == null || targets == null) return;
        if (!CanPayCosts(card)) return;
        PayCosts(card);

        // If card is global but also attaches traits to a side/type, derive targets now
        if (card.isGlobal && (card.traitsToAttachToTargets != null && card.traitsToAttachToTargets.Length > 0) && resolutionManager != null)
        {
            var all = resolutionManager.AllCreatures();
            var list = new List<Creature>();
            foreach (var c in all)
            {
                if (c == null || c.data == null) continue;
                // For global cards, we still want to apply to all creatures matching side/type filters.
                bool ok = true;
                switch (card.targetSide)
                {
                    case EffectTargetSide.Ally: ok = c.owner == player; break;
                    case EffectTargetSide.Enemy: ok = c.owner != player; break;
                    case EffectTargetSide.Any: ok = true; break;
                }
                if (!ok) continue;
                switch (card.targetType)
                {
                    case EffectTargetType.Herbivore: ok = c.data.type == CardType.Herbivore; break;
                    case EffectTargetType.Carnivore: ok = c.data.type == CardType.Carnivore; break;
                    case EffectTargetType.Avian: ok = c.data.type == CardType.Avian; break;
                    case EffectTargetType.Any: ok = true; break;
                }
                if (!ok) continue;
                list.Add(c);
            }
            targets = list;
        }

        foreach (var c in targets.Where(t => t != null))
        {
            // Permanent deltas (e.g., Mutation Boost)
            if (card.applyPermanentStatDelta)
            {
                c.body += card.bodyDelta;
                c.speed += card.speedDelta;
                c.RefreshStatsUI();
            }

            // Attach traits
            if (card.traitsToAttachToTargets != null)
            {
                foreach (var tr in card.traitsToAttachToTargets)
                {
                    if (tr == null) continue;
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


