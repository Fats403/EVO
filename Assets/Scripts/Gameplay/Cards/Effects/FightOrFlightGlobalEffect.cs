using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Fight or Flight - A global effect where the player chooses between:
/// - Fight: All allies gain +N Bulk (temporary body boost)
/// - Flight: All allies gain +N Haste (temporary speed boost)
///
/// The choice is made BEFORE the card is played via the pre-play choice system.
/// The choicePayload contains the optionId ("fight" or "flight").
/// </summary>
[CreateAssetMenu(menuName = "Effects/Global/Fight or Flight")]
public class FightOrFlightGlobalEffect : GlobalEffectBase
{
    [Header("Effect Values")]
    [Tooltip("Bulk stacks granted when choosing Fight.")]
    public int bulkAmount = 1;

    [Tooltip("Haste stacks granted when choosing Flight.")]
    public int hasteAmount = 1;

    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        // For AI, use the smart pick logic
        if (!NetworkSessionStore.IsNetworkedGame && !NetworkRoleHelper.IsLocalPlayer(owner))
        {
            HandleAIPick(rm);
            remainingRounds = 0;
            return;
        }

        // The choice was made before playing - read it from choicePayload
        if (string.IsNullOrEmpty(choicePayload))
        {
            Debug.LogWarning(
                "FightOrFlightGlobalEffect: No choice payload received. Defaulting to Fight."
            );
            ApplyFight(rm);
            remainingRounds = 0;
            return;
        }

        // Apply the chosen effect
        if (choicePayload == "fight")
        {
            ApplyFight(rm);
            FeedbackManager.Instance?.Log("Fight or Flight: chose Fight");
        }
        else if (choicePayload == "flight")
        {
            ApplyFlight(rm);
            FeedbackManager.Instance?.Log("Fight or Flight: chose Flight");
        }
        else
        {
            Debug.LogWarning(
                $"FightOrFlightGlobalEffect: Unknown choice '{choicePayload}'. Defaulting to Fight."
            );
            ApplyFight(rm);
        }

        remainingRounds = 0;
    }

    private void ApplyFight(ResolutionManager rm)
    {
        var allies = GetAllies(rm);
        if (allies.Count == 0)
            return;

        foreach (var c in allies)
        {
            c.AddStatus(StatusTag.Bulk, bulkAmount);
        }

        ShowFeedback(allies, $"+{bulkAmount} Bulk");
        PlayVisualFeedback(allies);
    }

    private void ApplyFlight(ResolutionManager rm)
    {
        var allies = GetAllies(rm);
        if (allies.Count == 0)
            return;

        foreach (var c in allies)
        {
            c.AddStatus(StatusTag.Haste, hasteAmount);
        }

        ShowFeedback(allies, $"+{hasteAmount} Haste");
        PlayVisualFeedback(allies);
    }

    private List<Creature> GetAllies(ResolutionManager rm)
    {
        return rm.AllCreatures()
            .Where(c => c != null && c.owner == owner && c.currentHealth > 0 && !c.isDying)
            .ToList();
    }

    private void ShowFeedback(List<Creature> creatures, string text)
    {
        if (FeedbackManager.Instance == null)
            return;

        foreach (var c in creatures)
        {
            if (c != null)
            {
                FeedbackManager.Instance.ShowFloatingText(
                    text,
                    c.transform.position,
                    GameColorPalette.TextPositive
                );
            }
        }
    }

    private void PlayVisualFeedback(List<Creature> allies)
    {
        // Always play our own visual feedback on allies only.
        // We ignore suppressHitBounceFromSource here because this effect
        // handles its own targeting - we only want to bounce the creatures
        // we actually affected (allies), not whatever EffectsManager would target.
        if (EffectsManager.Instance != null)
        {
            EffectsManager.Instance.PlayHitBounceOnCreatures(allies);
        }
    }

    private void HandleAIPick(ResolutionManager rm)
    {
        // AI picks based on board state:
        // - If creatures are slower than opponent average, pick Flight (Haste)
        // - Otherwise pick Fight (Bulk)
        var allies = GetAllies(rm);
        var enemies = rm.AllCreatures()
            .Where(c => c != null && c.owner != owner && c.currentHealth > 0 && !c.isDying)
            .ToList();

        if (allies.Count == 0)
            return;

        double allyAvgSpeed = allies.Average(c => c.speed);
        double enemyAvgSpeed = enemies.Count > 0 ? enemies.Average(c => c.speed) : 0;

        if (allyAvgSpeed < enemyAvgSpeed)
        {
            ApplyFlight(rm);
            FeedbackManager.Instance?.Log("Fight or Flight (AI): chose Flight");
        }
        else
        {
            ApplyFight(rm);
            FeedbackManager.Instance?.Log("Fight or Flight (AI): chose Fight");
        }
    }
}
