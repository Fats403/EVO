using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Predator's Focus - A targeted effect where the player chooses between:
/// - Hunt: Target gains +N Fury (extra attack damage)
/// - Stalk: Target gains +N Stealth (cannot be targeted)
///
/// This effect demonstrates pre-play choice + manual target selection.
/// The choicePayload contains the optionId ("hunt" or "stalk").
/// </summary>
[CreateAssetMenu(menuName = "Effects/Runtime/Predator's Focus")]
public class PredatorsFocusEffect : RuntimeEffectBase
{
    [Header("Effect Values")]
    [Tooltip("Fury stacks granted when choosing Hunt.")]
    public int furyAmount = 2;

    [Tooltip("Stealth stacks granted when choosing Stalk.")]
    public int stealthAmount = 2;

    public override void Apply(List<Creature> targets, SlotOwner owner, ResolutionManager rm)
    {
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning("PredatorsFocusEffect: No targets provided.");
            return;
        }

        // Read the choice from the payload
        if (string.IsNullOrEmpty(choicePayload))
        {
            Debug.LogWarning("PredatorsFocusEffect: No choice payload. Defaulting to Hunt.");
            ApplyHunt(targets);
            return;
        }

        if (choicePayload == "hunt")
        {
            ApplyHunt(targets);
            FeedbackManager.Instance?.Log("Predator's Focus: chose Hunt");
        }
        else if (choicePayload == "stalk")
        {
            ApplyStalk(targets);
            FeedbackManager.Instance?.Log("Predator's Focus: chose Stalk");
        }
        else
        {
            Debug.LogWarning(
                $"PredatorsFocusEffect: Unknown choice '{choicePayload}'. Defaulting to Hunt."
            );
            ApplyHunt(targets);
        }
    }

    private void ApplyHunt(List<Creature> targets)
    {
        foreach (var c in targets)
        {
            if (c == null || c.isDying)
                continue;

            c.AddStatus(StatusTag.Fury, furyAmount);
            ShowFeedback(c, $"+{furyAmount} Fury");
        }
    }

    private void ApplyStalk(List<Creature> targets)
    {
        foreach (var c in targets)
        {
            if (c == null || c.isDying)
                continue;

            c.AddStatus(StatusTag.Stealth, stealthAmount);
            ShowFeedback(c, $"+{stealthAmount} Stealth");
        }
    }

    private void ShowFeedback(Creature c, string text)
    {
        if (FeedbackManager.Instance == null || c == null)
            return;

        FeedbackManager.Instance.ShowFloatingText(
            text,
            c.transform.position,
            GameColorPalette.TextPositive
        );
    }
}
