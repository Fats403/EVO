using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// "Ancient Instinct" effect: Look at the top 3 cards of your deck.
/// Add 1 to your hand. Shuffle the rest back.
///
/// This demonstrates how to use CardChoiceManager with effect cards.
/// The effect shows during the Place phase when played, pausing for player input.
/// </summary>
[CreateAssetMenu(menuName = "Effects/Global/Ancient Instinct")]
public class AncientInstinctEffect : GlobalEffectBase
{
    [Tooltip("Number of cards to reveal from top of deck.")]
    public int revealCount = 3;

    [Tooltip("Number of cards the player picks to add to hand.")]
    public int pickCount = 1;

    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        // For AI in single-player, use simple auto-pick
        if (!NetworkSessionStore.IsNetworkedGame && !NetworkRoleHelper.IsLocalPlayer(owner))
        {
            HandleAIPick();
            remainingRounds = 0;
            return;
        }

        // For networked games where this is the REMOTE player's effect:
        // We don't show any UI - just track that opponent drew cards.
        // The opponent's hand/deck contents are hidden from us anyway.
        if (NetworkSessionStore.IsNetworkedGame && !NetworkRoleHelper.IsLocalPlayer(owner))
        {
            if (OpponentDeckTracker.Instance != null)
            {
                OpponentDeckTracker.Instance.OnOpponentDrew(pickCount);
            }
            remainingRounds = 0;
            return;
        }

        // LOCAL player path - show the choice UI
        var dm = DeckManager.Instance;
        if (dm == null)
        {
            Debug.LogWarning("AncientInstinctEffect: DeckManager not found.");
            remainingRounds = 0;
            return;
        }

        // Peek at top N cards
        var topCards = dm.PeekTopCards(revealCount);

        if (topCards.Count == 0)
        {
            FeedbackManager.Instance?.ShowGlobalAlert(
                "No cards in deck!",
                GameColorPalette.TextWarning
            );
            remainingRounds = 0;
            return;
        }

        // Adjust pick count if fewer cards available
        int actualPick = Mathf.Min(pickCount, topCards.Count);

        // Check if CardChoiceManager is available
        if (CardChoiceManager.Instance == null)
        {
            Debug.LogWarning("AncientInstinctEffect: CardChoiceManager not found. Using fallback.");
            FallbackDraw(dm, topCards, actualPick);
            remainingRounds = 0;
            return;
        }

        // Show the choice UI
        // Note: The global effect runs synchronously, but the choice UI is async.
        // The effect completes immediately, and the choice callback handles the actual card movement.
        // Pass the owner for proper networking synchronization.
        CardChoiceManager.Instance.ShowChoice(
            new CardChoiceRequest
            {
                title = "Ancient Instinct",
                subtitle =
                    actualPick == 1
                        ? "Choose 1 card to add to your hand"
                        : $"Choose {actualPick} cards to add to your hand",
                cards = topCards,
                minPicks = actualPick,
                maxPicks = actualPick,
                confirmButtonText = "Add to Hand",
                allowCancel = false,
                timeoutSeconds = 30f,
                timeoutBehavior = CardChoiceTimeoutBehavior.RandomFill,
                // Don't pause game loop - this only affects local hidden state (our deck/hand)
                // The opponent doesn't need to wait for our choice
                pauseGameLoop = false,
                onConfirm = (pickedCards) => OnCardsChosen(dm, topCards, pickedCards),
            },
            owner // Pass owner for networking
        );

        remainingRounds = 0;
    }

    private void OnCardsChosen(
        DeckManager dm,
        List<ScriptableObject> revealed,
        List<ScriptableObject> picked
    )
    {
        // This callback only runs for the LOCAL player (remote path exits early in OnPlay)
        if (dm == null)
            return;

        if (picked == null)
            picked = new List<ScriptableObject>();

        // Add picked cards to hand (local player path)
        foreach (var card in picked)
        {
            if (card == null)
                continue;

            // Remove from deck
            dm.RemoveFromDrawPile(card);

            // Check hand limit and add to hand
            if (dm.CurrentHandCount() < GameRules.MaxHandSize)
            {
                dm.CreateCardUI(card, triggerLayoutAndUI: true);

                FeedbackManager.Instance?.ShowFloatingText(
                    "+1 Card",
                    Camera.main?.transform.position ?? Vector3.zero,
                    GameColorPalette.TextPositive
                );
            }
        }

        // The non-picked revealed cards stay on top of deck (they weren't removed)
        // Optionally shuffle them back in:
        var notPicked = revealed.Where(c => !picked.Contains(c)).ToList();
        if (notPicked.Count > 0)
        {
            // Remove them from their current position and shuffle back
            foreach (var card in notPicked)
            {
                dm.RemoveFromDrawPile(card);
            }
            dm.ShuffleIntoDeck(notPicked);
        }

        dm.UpdateHandUI();

        FeedbackManager.Instance?.Log($"Ancient Instinct: drew {picked.Count} card(s)");
    }

    private void HandleAIPick()
    {
        // AI picks the highest-value card based on simple heuristics
        // For now, just draw the top card normally
        if (!NetworkSessionStore.IsNetworkedGame && AIManager.Instance != null)
        {
            AIManager.Instance.TryDrawOneCard();
        }
    }

    private void FallbackDraw(DeckManager dm, List<ScriptableObject> topCards, int count)
    {
        // Simple fallback: just draw from deck normally
        for (int i = 0; i < count && i < topCards.Count; i++)
        {
            if (dm.CurrentHandCount() < GameRules.MaxHandSize)
            {
                dm.DrawCard();
            }
        }
        dm.UpdateHandUI();
    }
}
