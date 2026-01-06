using UnityEngine;

/// <summary>
/// Tribal Unity:
/// If you control 3 or more creatures of the same type (Herbivore, Carnivore, or Avian),
/// draw 2 cards; otherwise draw 1.
/// </summary>
[CreateAssetMenu(menuName = "Effects/Global/Tribal Unity")]
public class TribalUnityGlobalEffect : GlobalEffectBase
{
    [Tooltip("Cards drawn when the tribal condition is NOT met.")]
    public int fallbackDraw = 1;

    [Tooltip("Cards drawn when you control 3+ creatures of the same type.")]
    public int bonusDraw = 2;

    public override void OnPlay(ResolutionManager rm)
    {
        if (rm == null)
            return;

        // Count allied creatures by type
        int herbivores = 0;
        int carnivores = 0;
        int avians = 0;

        var allies = DeterministicHelpers.GetCreaturesSorted(c => c.owner == owner);
        foreach (var c in allies)
        {
            if (c == null || c.data == null)
                continue;

            switch (c.data.type)
            {
                case CardType.Herbivore:
                    herbivores++;
                    break;
                case CardType.Carnivore:
                    carnivores++;
                    break;
                case CardType.Avian:
                    avians++;
                    break;
            }
        }

        bool hasTribalUnity = herbivores >= 3 || carnivores >= 3 || avians >= 3;

        int drawCount = hasTribalUnity ? bonusDraw : fallbackDraw;
        if (drawCount <= 0)
        {
            remainingRounds = 0;
            return;
        }

        // Local player draws cards using DeckManager (respecting hand size).
        // Use the animated draw sequence so cards appear one at a time.
        if (NetworkRoleHelper.IsLocalPlayer(owner))
        {
            var dm = DeckManager.Instance;
            if (dm != null)
            {
                // Use the animated multi-draw coroutine for sequential card reveals
                dm.StartCoroutine(dm.DrawCardsAnimated(drawCount));
            }
        }
        // Networked remote opponent: only update the opponent tracker so the HUD stays in sync.
        else if (NetworkSessionStore.IsNetworkedGame && OpponentDeckTracker.Instance != null)
        {
            OpponentDeckTracker.Instance.OnOpponentDrew(drawCount);
        }
        // Offline AI: mirror draw behavior using AIManager.
        else if (!NetworkSessionStore.IsNetworkedGame && AIManager.Instance != null)
        {
            for (int i = 0; i < drawCount; i++)
            {
                AIManager.Instance.TryDrawOneCard();
            }
        }

        FeedbackManager.Instance?.Log(
            hasTribalUnity
                ? $"Tribal Unity: drew {drawCount} cards (tribal bonus)"
                : $"Tribal Unity: drew {drawCount} card"
        );

        remainingRounds = 0;
    }
}
