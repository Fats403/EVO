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

        // This effect needs to interact with the card choice UI.
        // In single-player vs AI, the non-local owner is the AI, so auto-pick.
        // In networked games, we always run the choice logic on both clients:
        // - The owner sees the interactive choice UI.
        // - The remote client shows a waiting overlay and applies the result via network.
        if (!NetworkSessionStore.IsNetworkedGame && !NetworkRoleHelper.IsLocalPlayer(owner))
        {
            HandleAIPick();
            remainingRounds = 0;
            return;
        }

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
        if (picked == null)
            picked = new List<ScriptableObject>();

        // In networked games on the *remote* client (where this effect owner is NOT local),
        // we should NOT modify the local DeckManager/hand, since this represents the opponent.
        // Instead, just update the opponent tracker so hand/deck counts stay in sync.
        if (NetworkSessionStore.IsNetworkedGame && !NetworkRoleHelper.IsLocalPlayer(owner))
        {
            if (OpponentDeckTracker.Instance != null && picked.Count > 0)
            {
                OpponentDeckTracker.Instance.OnOpponentDrew(picked.Count);
            }
            return;
        }

        if (dm == null)
            return;

        // Add picked cards to hand (local player path)
        foreach (var card in picked)
        {
            if (card == null)
                continue;

            // Remove from deck
            dm.RemoveFromDrawPile(card);

            // Check hand limit and add to hand
            if (dm.CurrentHandCount() < dm.maxHandSize)
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
            if (dm.CurrentHandCount() < dm.maxHandSize)
            {
                dm.DrawCard();
            }
        }
        dm.UpdateHandUI();
    }
}
