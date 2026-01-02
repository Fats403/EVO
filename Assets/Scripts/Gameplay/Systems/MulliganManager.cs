using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the optional mulligan phase at the start of the game.
/// Players can choose cards from their starting hand to replace.
///
/// Integrates with GameManager and DeckManager to:
/// 1. Pause the game after initial draw
/// 2. Show card choice UI for mulligan selection
/// 3. Replace selected cards and shuffle them back
/// 4. Resume normal game flow
/// </summary>
public class MulliganManager : MonoBehaviour
{
    public static MulliganManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Whether mulligan is enabled for this game mode.")]
    public bool mulliganEnabled = true;

    [Tooltip("Maximum number of cards a player can mulligan. -1 = entire hand.")]
    public int maxMulliganCount = -1;

    [Tooltip("Seconds to wait for mulligan decision. 0 = no timeout.")]
    public float mulliganTimeout = 30f;

    [Header("UI Text")]
    public string mulliganTitle = "Mulligan";
    public string mulliganSubtitle = "Select cards to replace, then confirm";
    public string keepHandText = "Keep Hand";

    // State
    private bool isMulliganPhase;
    private bool localPlayerDone;
    private bool remotePlayerDone;
    private System.Action onMulliganComplete;

    public bool IsMulliganPhase => isMulliganPhase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Start the mulligan phase. Call this after initial hands are drawn.
    /// </summary>
    /// <param name="onComplete">Callback when all players have finished mulligan.</param>
    public void StartMulligan(System.Action onComplete)
    {
        if (!mulliganEnabled)
        {
            onComplete?.Invoke();
            return;
        }

        onMulliganComplete = onComplete;
        isMulliganPhase = true;
        localPlayerDone = false;
        remotePlayerDone = !NetworkSessionStore.IsNetworkedGame; // AI/local doesn't need to wait

        // Show mulligan UI for local player
        ShowLocalPlayerMulligan();

        // In networked games, wait for both players
        // In local games, AI doesn't mulligan (or could have AI mulligan logic)
        if (!NetworkSessionStore.IsNetworkedGame)
        {
            // Optionally handle AI mulligan here
            HandleAIMulligan();
        }
    }

    /// <summary>
    /// Skip mulligan entirely and proceed to game.
    /// </summary>
    public void SkipMulligan()
    {
        isMulliganPhase = false;
        localPlayerDone = true;
        remotePlayerDone = true;
        onMulliganComplete?.Invoke();
        onMulliganComplete = null;
    }

    private void ShowLocalPlayerMulligan()
    {
        var dm = DeckManager.Instance;
        if (dm == null)
        {
            Debug.LogError("MulliganManager: DeckManager not found.");
            OnLocalPlayerMulliganComplete(new List<ScriptableObject>());
            return;
        }

        // Get cards currently in hand
        var handCards = GetHandCards(dm);
        if (handCards.Count == 0)
        {
            OnLocalPlayerMulliganComplete(new List<ScriptableObject>());
            return;
        }

        if (CardChoiceManager.Instance == null)
        {
            Debug.LogError("MulliganManager: CardChoiceManager not found in scene.");
            OnLocalPlayerMulliganComplete(new List<ScriptableObject>());
            return;
        }

        int maxReplace =
            maxMulliganCount > 0 ? Mathf.Min(maxMulliganCount, handCards.Count) : handCards.Count;

        var request = new CardChoiceRequest
        {
            title = mulliganTitle,
            subtitle =
                maxReplace == handCards.Count
                    ? mulliganSubtitle
                    : $"Select up to {maxReplace} cards to replace",
            cards = handCards,
            minPicks = 0,
            maxPicks = maxReplace,
            allowEmpty = true,
            confirmButtonText = keepHandText,
            allowCancel = false,
            timeoutSeconds = mulliganTimeout,
            timeoutBehavior = CardChoiceTimeoutBehavior.ConfirmCurrent,
            onConfirm = OnLocalPlayerMulliganComplete,
        };

        CardChoiceManager.Instance.ShowChoice(request);
    }

    private List<ScriptableObject> GetHandCards(DeckManager dm)
    {
        // Use DeckManager's built-in helper method
        return dm.GetHandCards();
    }

    private void OnLocalPlayerMulliganComplete(List<ScriptableObject> cardsToReplace)
    {
        var dm = DeckManager.Instance;
        if (dm == null)
        {
            localPlayerDone = true;
            CheckAllPlayersComplete();
            return;
        }

        if (cardsToReplace != null && cardsToReplace.Count > 0)
        {
            StartCoroutine(PerformMulligan(dm, cardsToReplace));
        }
        else
        {
            // Player kept their hand
            FeedbackManager.Instance?.Log("Kept starting hand");
            localPlayerDone = true;
            CheckAllPlayersComplete();
        }
    }

    private IEnumerator PerformMulligan(DeckManager dm, List<ScriptableObject> cardsToReplace)
    {
        int replaceCount = cardsToReplace.Count;

        // 1. Remove selected cards from hand UI
        foreach (var cardData in cardsToReplace)
        {
            RemoveCardFromHand(dm, cardData);
            yield return new WaitForSeconds(0.1f);
        }

        // 2. Shuffle replaced cards back into deck
        ShuffleCardsIntoDeck(dm, cardsToReplace);

        // 3. Draw replacement cards
        yield return new WaitForSeconds(0.2f);

        for (int i = 0; i < replaceCount; i++)
        {
            dm.DrawCard();
            yield return new WaitForSeconds(0.15f);
        }

        dm.UpdateHandUI();

        FeedbackManager.Instance?.Log(
            $"Mulliganed {replaceCount} card{(replaceCount > 1 ? "s" : "")}"
        );

        localPlayerDone = true;
        CheckAllPlayersComplete();
    }

    private void RemoveCardFromHand(DeckManager dm, ScriptableObject cardData)
    {
        // Use DeckManager's built-in helper method
        dm.RemoveCardFromHand(cardData);
    }

    private void ShuffleCardsIntoDeck(DeckManager dm, List<ScriptableObject> cards)
    {
        // Use DeckManager's built-in helper method
        dm.ShuffleIntoDeck(cards);
    }

    private void HandleAIMulligan()
    {
        // Simple AI mulligan logic: keep high-value cards, replace low-value ones
        // For now, AI doesn't mulligan (keeps hand as-is)
        // This could be expanded with smarter AI logic

        remotePlayerDone = true;

        // Could add AI mulligan logic here:
        // - Analyze hand for curve
        // - Replace cards that don't fit current era momentum
        // - Keep synergy pieces together
    }

    /// <summary>
    /// Called by networking layer when remote player completes mulligan.
    /// </summary>
    public void OnRemotePlayerMulliganComplete()
    {
        remotePlayerDone = true;
        CheckAllPlayersComplete();
    }

    private void CheckAllPlayersComplete()
    {
        if (!localPlayerDone || !remotePlayerDone)
            return;

        isMulliganPhase = false;

        // Small delay before continuing to let UI settle
        StartCoroutine(CompleteMulliganPhase());
    }

    private IEnumerator CompleteMulliganPhase()
    {
        yield return new WaitForSeconds(0.3f);

        onMulliganComplete?.Invoke();
        onMulliganComplete = null;
    }
}
