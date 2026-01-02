using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton manager for displaying card choice prompts.
/// Can be used for mulligans, card draw effects, discard effects, etc.
///
/// Usage:
///   CardChoiceManager.Instance.ShowChoice(new CardChoiceRequest { ... });
///
/// Or use the static factory methods:
///   CardChoiceManager.Instance.ShowChoice(CardChoiceRequest.Mulligan(hand, OnMulliganComplete));
/// </summary>
public class CardChoiceManager : MonoBehaviour
{
    public static CardChoiceManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root canvas group for the entire choice panel.")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("Title text at the top of the panel.")]
    public TextMeshProUGUI titleLabel;

    [Tooltip("Subtitle/instruction text.")]
    public TextMeshProUGUI subtitleLabel;

    [Tooltip("Container where card options are spawned (HorizontalLayoutGroup recommended).")]
    public Transform cardContainer;

    [Tooltip("Prefab for each card option slot.")]
    public GameObject cardOptionPrefab;

    [Header("Buttons")]
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonText;
    public Button cancelButton;
    public TextMeshProUGUI cancelButtonText;

    [Header("Selection Feedback")]
    [Tooltip("Label showing current selection count (e.g., '2 / 3 selected').")]
    public TextMeshProUGUI selectionCountLabel;

    [Tooltip("Optional: progress bar or fill image for selection count.")]
    public Image selectionProgressFill;

    [Header("Timer")]
    [Tooltip("Optional: timer display for timed choices.")]
    public TextMeshProUGUI timerLabel;

    [Tooltip("Optional: timer fill image.")]
    public Image timerFill;

    [Header("Animation")]
    [Tooltip("Duration of panel fade in/out.")]
    public float fadeDuration = 0.2f;

    [Tooltip("Delay between spawning each card option for staggered effect.")]
    public float cardSpawnDelay = 0.05f;

    [Header("Audio")]
    [Tooltip("Sound when selecting a card.")]
    public AudioClip selectSound;

    [Tooltip("Sound when deselecting a card.")]
    public AudioClip deselectSound;

    [Tooltip("Sound when confirming.")]
    public AudioClip confirmSound;

    [Header("Waiting Overlay")]
    [Tooltip("CanvasGroup for the 'waiting for opponent' overlay.")]
    public CanvasGroup waitingOverlayCanvasGroup;

    [Tooltip("Label text for the waiting overlay.")]
    public TextMeshProUGUI waitingLabel;

    [Tooltip("Default message when waiting for opponent's choice.")]
    public string waitingMessage = "Waiting for opponent...";

    // State
    private CardChoiceRequest currentRequest;
    private readonly List<CardChoiceOptionUI> spawnedOptions = new();
    private readonly List<ScriptableObject> selectedCards = new();
    private readonly List<int> selectionOrder = new(); // indices into selectedCards
    private Coroutine timeoutCoroutine;
    private bool isShowing;
    private SlotOwner choiceOwner = SlotOwner.Player1;
    private string currentChoiceContextId;

    // Pending remote choice state (for when we're waiting for remote player's decision)
    private CardChoiceRequest pendingRemoteRequest;
    private string pendingRemoteContextId;
    private bool isWaitingForRemote;

    /// <summary>True if a choice prompt is currently displayed.</summary>
    public bool IsShowing => isShowing;

    /// <summary>True if waiting for a remote player's choice.</summary>
    public bool IsWaitingForRemote => isWaitingForRemote;

    /// <summary>The current request being displayed, or null if none.</summary>
    public CardChoiceRequest CurrentRequest => currentRequest;

    /// <summary>
    /// Event fired when a choice is confirmed. Useful for networking sync.
    /// Parameters: (owner, cardIds chosen)
    /// </summary>
    public event System.Action<SlotOwner, List<string>> OnChoiceConfirmed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Start hidden
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        // Hide waiting overlay
        if (waitingOverlayCanvasGroup != null)
        {
            waitingOverlayCanvasGroup.alpha = 0f;
            waitingOverlayCanvasGroup.interactable = false;
            waitingOverlayCanvasGroup.blocksRaycasts = false;
        }

        // Wire up buttons
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Display a card choice prompt with the given configuration.
    /// </summary>
    /// <param name="request">The choice configuration.</param>
    /// <param name="owner">Optional: which player is making this choice (for networking).</param>
    public void ShowChoice(CardChoiceRequest request, SlotOwner owner = SlotOwner.Player1)
    {
        if (request == null)
        {
            Debug.LogError("CardChoiceManager.ShowChoice: Request is null.");
            return;
        }

        // Generate a unique context ID for this choice (used for network sync)
        currentChoiceContextId = GenerateContextId(request.title);

        // In networked games, check if this choice is for local player or remote
        bool isNetworkedGame = NetworkSessionStore.IsNetworkedGame;
        bool isLocalPlayerChoice = !isNetworkedGame || NetworkRoleHelper.IsLocalPlayer(owner);

        if (!isLocalPlayerChoice)
        {
            // This choice is for the remote player - show waiting overlay instead
            Debug.Log(
                $"[CardChoiceManager] Remote player ({owner}) is making a choice. Showing waiting overlay."
            );
            ShowWaitingForRemote(request, owner);
            return;
        }

        if (isShowing)
        {
            Debug.LogWarning("CardChoiceManager: A choice is already showing. Hiding previous.");
            HideImmediate();
        }

        currentRequest = request;
        choiceOwner = owner;
        isShowing = true;
        selectedCards.Clear();
        selectionOrder.Clear();

        // Pause game flow while waiting for choice
        SetGamePaused(true);

        // Setup UI text
        if (titleLabel != null)
            titleLabel.text = request.title ?? "";

        if (subtitleLabel != null)
        {
            subtitleLabel.text = request.subtitle ?? "";
            subtitleLabel.gameObject.SetActive(!string.IsNullOrEmpty(request.subtitle));
        }

        // Setup buttons
        SetupButtons();

        // Spawn card options
        StartCoroutine(SpawnCardOptions());

        // Show timer if applicable
        SetupTimer();

        // Fade in
        StartCoroutine(FadePanel(true));
    }

    /// <summary>
    /// Generates a unique context ID for tracking choices across network.
    /// </summary>
    private string GenerateContextId(string title)
    {
        // Combine title, owner, and timestamp for uniqueness
        int round = GameManager.Instance?.CurrentRound ?? 0;
        return $"{title ?? "choice"}_{round}_{System.DateTime.UtcNow.Ticks}";
    }

    /// <summary>
    /// Shows the "waiting for opponent" overlay while remote player makes a choice.
    /// </summary>
    private void ShowWaitingForRemote(CardChoiceRequest request, SlotOwner owner)
    {
        pendingRemoteRequest = request;
        pendingRemoteContextId = currentChoiceContextId;
        isWaitingForRemote = true;

        // Pause game flow
        SetGamePaused(true);

        // Show waiting overlay
        if (waitingOverlayCanvasGroup != null)
        {
            StartCoroutine(FadeWaitingOverlay(true));
        }

        if (waitingLabel != null)
        {
            waitingLabel.text = waitingMessage;
        }

        Debug.Log($"[CardChoiceManager] Waiting for remote player choice: {request.title}");
    }

    /// <summary>
    /// Hides the waiting overlay.
    /// </summary>
    private void HideWaitingOverlay()
    {
        if (waitingOverlayCanvasGroup != null)
        {
            StartCoroutine(FadeWaitingOverlay(false));
        }

        isWaitingForRemote = false;
        pendingRemoteRequest = null;
        pendingRemoteContextId = null;
    }

    private IEnumerator FadeWaitingOverlay(bool fadeIn)
    {
        if (waitingOverlayCanvasGroup == null)
            yield break;

        float start = waitingOverlayCanvasGroup.alpha;
        float end = fadeIn ? 1f : 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        if (fadeIn)
        {
            waitingOverlayCanvasGroup.interactable = true;
            waitingOverlayCanvasGroup.blocksRaycasts = true;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            waitingOverlayCanvasGroup.alpha = Mathf.Lerp(start, end, u);
            yield return null;
        }

        waitingOverlayCanvasGroup.alpha = end;

        if (!fadeIn)
        {
            waitingOverlayCanvasGroup.interactable = false;
            waitingOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Pause or resume game flow. Called when choice UI opens/closes.
    /// </summary>
    private void SetGamePaused(bool paused)
    {
        Debug.Log(
            $"[CardChoiceManager] SetGamePaused({paused}) - GameManager.Instance is {(GameManager.Instance != null ? "valid" : "NULL")}"
        );

        // Block/unblock player from ending their turn while choice is pending
        if (GameManager.Instance != null)
        {
            // Use the awaitingExternalInput flag if available
            GameManager.Instance.SetAwaitingExternalInput(paused);
        }
        else
        {
            Debug.LogWarning(
                "[CardChoiceManager] GameManager.Instance is null! Cannot pause game."
            );
        }
    }

    /// <summary>
    /// Hide the choice panel immediately without callbacks.
    /// </summary>
    public void HideImmediate()
    {
        StopAllCoroutines();
        ClearSpawnedOptions();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        // Resume game flow
        SetGamePaused(false);

        currentRequest = null;
        isShowing = false;
        selectedCards.Clear();
        selectionOrder.Clear();
    }

    /// <summary>
    /// Hide the choice panel with fade animation. Does not trigger callbacks.
    /// </summary>
    public void Hide()
    {
        if (!isShowing)
            return;

        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        StartCoroutine(HideCoroutine());
    }

    private IEnumerator HideCoroutine()
    {
        yield return StartCoroutine(FadePanel(false));
        ClearSpawnedOptions();
        currentRequest = null;
        isShowing = false;
        selectedCards.Clear();
        selectionOrder.Clear();
    }

    private void SetupButtons()
    {
        // Confirm button
        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(true);
            if (confirmButtonText != null)
                confirmButtonText.text = currentRequest.confirmButtonText ?? "Confirm";
        }

        // Cancel button
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(currentRequest.allowCancel);
            if (cancelButtonText != null)
                cancelButtonText.text = currentRequest.cancelButtonText ?? "Cancel";
        }

        UpdateConfirmButtonState();
    }

    private void SetupTimer()
    {
        bool hasTimer = currentRequest.timeoutSeconds > 0f;

        if (timerLabel != null)
            timerLabel.gameObject.SetActive(hasTimer);
        if (timerFill != null)
            timerFill.gameObject.SetActive(hasTimer);

        if (hasTimer)
        {
            timeoutCoroutine = StartCoroutine(TimerCoroutine());
        }
    }

    private IEnumerator TimerCoroutine()
    {
        float remaining = currentRequest.timeoutSeconds;
        float total = remaining;

        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(remaining / total);

            if (timerLabel != null)
                timerLabel.text = Mathf.CeilToInt(remaining).ToString();

            if (timerFill != null)
                timerFill.fillAmount = t;

            yield return null;
        }

        // Timeout reached
        HandleTimeout();
    }

    private void HandleTimeout()
    {
        switch (currentRequest.timeoutBehavior)
        {
            case CardChoiceTimeoutBehavior.ConfirmCurrent:
                ConfirmSelection();
                break;

            case CardChoiceTimeoutBehavior.Cancel:
                if (currentRequest.allowCancel)
                    CancelSelection();
                else
                    ConfirmSelection(); // fallback
                break;

            case CardChoiceTimeoutBehavior.RandomFill:
                RandomFillToMinimum();
                ConfirmSelection();
                break;
        }
    }

    private void RandomFillToMinimum()
    {
        if (currentRequest == null)
            return;

        int needed = currentRequest.minPicks - selectedCards.Count;
        if (needed <= 0)
            return;

        // Get unselected cards
        var unselected = currentRequest
            .cards.Where(c => c != null && !selectedCards.Contains(c))
            .ToList();

        // Shuffle and pick
        for (int i = unselected.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (unselected[j], unselected[i]) = (unselected[i], unselected[j]);
        }

        for (int i = 0; i < Mathf.Min(needed, unselected.Count); i++)
        {
            selectedCards.Add(unselected[i]);
            selectionOrder.Add(selectedCards.Count - 1);
        }
    }

    private IEnumerator SpawnCardOptions()
    {
        ClearSpawnedOptions();

        if (cardContainer == null || cardOptionPrefab == null)
        {
            Debug.LogError("CardChoiceManager: cardContainer or cardOptionPrefab not assigned.");
            yield break;
        }

        if (currentRequest.cards == null || currentRequest.cards.Count == 0)
        {
            Debug.LogWarning("CardChoiceManager: No cards to display.");
            yield break;
        }

        foreach (var card in currentRequest.cards)
        {
            if (card == null)
                continue;

            var optionObj = Instantiate(cardOptionPrefab, cardContainer);
            var option = optionObj.GetComponent<CardChoiceOptionUI>();

            if (option != null)
            {
                option.SetCard(card, OnCardClicked, currentRequest.showFaceDown);
                spawnedOptions.Add(option);
            }
            else
            {
                Debug.LogError(
                    "CardChoiceManager: cardOptionPrefab missing CardChoiceOptionUI component."
                );
                Destroy(optionObj);
            }

            if (cardSpawnDelay > 0f)
                yield return new WaitForSecondsRealtime(cardSpawnDelay);
        }

        UpdateSelectionUI();
    }

    private void ClearSpawnedOptions()
    {
        foreach (var option in spawnedOptions)
        {
            if (option != null)
                Destroy(option.gameObject);
        }
        spawnedOptions.Clear();
    }

    private void OnCardClicked(CardChoiceOptionUI option)
    {
        if (option == null || option.CardData == null)
            return;

        var card = option.CardData;

        if (selectedCards.Contains(card))
        {
            // Deselect
            int idx = selectedCards.IndexOf(card);
            selectedCards.Remove(card);
            selectionOrder.Remove(idx);

            // Reindex remaining orders
            for (int i = 0; i < selectionOrder.Count; i++)
            {
                if (selectionOrder[i] > idx)
                    selectionOrder[i]--;
            }

            // Deselect all UIs that show this card (important if the same ScriptableObject
            // appears multiple times in the list, e.g., duplicate top-deck entries).
            foreach (var opt in spawnedOptions)
            {
                if (opt != null && opt.CardData == card)
                {
                    opt.SetSelected(false);
                }
            }

            PlaySound(deselectSound);
        }
        else
        {
            // For single-selection (maxPicks == 1), auto-replace the current selection
            if (currentRequest.maxPicks == 1 && selectedCards.Count >= 1)
            {
                Debug.Log(
                    $"[CardChoiceManager] Single-select mode: replacing previous selection. maxPicks={currentRequest.maxPicks}, selectedCount={selectedCards.Count}"
                );

                // Deselect all currently selected cards (should only be one)
                foreach (var opt in spawnedOptions)
                {
                    if (opt != null && opt.IsSelected)
                    {
                        opt.SetSelected(false);
                    }
                }
                selectedCards.Clear();
                selectionOrder.Clear();

                // Now select the new one
                selectedCards.Add(card);
                int order = currentRequest.orderMatters ? 0 : -1;
                option.SetSelected(true, order);
                PlaySound(selectSound);
            }
            // Check if we can select more (for multi-select)
            else if (selectedCards.Count >= currentRequest.maxPicks)
            {
                // At max for multi-select - do nothing
                Debug.Log(
                    $"[CardChoiceManager] At max selections: {selectedCards.Count}/{currentRequest.maxPicks}"
                );
                return;
            }
            else
            {
                // Select
                selectedCards.Add(card);
                int order = currentRequest.orderMatters ? selectedCards.Count - 1 : -1;
                option.SetSelected(true, order);
                PlaySound(selectSound);
            }
        }

        // Update all options' order badges if order matters
        if (currentRequest.orderMatters)
        {
            UpdateOrderBadges();
        }

        // Dim cards that can't be selected if at max
        UpdateInteractableStates();

        // Update confirm button state
        UpdateConfirmButtonState();

        // Update selection count UI
        UpdateSelectionUI();

        // Notify callback
        currentRequest.onSelectionChanged?.Invoke(new List<ScriptableObject>(selectedCards));
    }

    private void UpdateOrderBadges()
    {
        foreach (var option in spawnedOptions)
        {
            if (option == null)
                continue;

            if (option.IsSelected && currentRequest.orderMatters)
            {
                int idx = selectedCards.IndexOf(option.CardData);
                option.SetSelected(true, idx);
                option.ShowOrderBadge(true);
            }
            else
            {
                option.ShowOrderBadge(false);
            }
        }
    }

    private void UpdateInteractableStates()
    {
        bool atMax = selectedCards.Count >= currentRequest.maxPicks;

        foreach (var option in spawnedOptions)
        {
            if (option == null)
                continue;

            bool canInteract;

            if (currentRequest != null && currentRequest.maxPicks == 1)
            {
                // Single-select: always allow clicking any option so we can replace the selection
                canInteract = true;
            }
            else
            {
                // Multi-select: if at max, only selected cards are interactable (to deselect).
                // Otherwise, all cards are interactable.
                canInteract = option.IsSelected || !atMax;
            }

            option.SetInteractable(canInteract);
        }
    }

    private void UpdateConfirmButtonState()
    {
        if (confirmButton == null)
            return;

        bool canConfirm = false;

        if (currentRequest.allowEmpty && selectedCards.Count == 0)
        {
            canConfirm = true;
        }
        else
        {
            canConfirm = selectedCards.Count >= currentRequest.minPicks;
        }

        confirmButton.interactable = canConfirm;
    }

    private void UpdateSelectionUI()
    {
        if (selectionCountLabel != null)
        {
            string text;
            if (currentRequest.minPicks == currentRequest.maxPicks)
            {
                text = $"{selectedCards.Count} / {currentRequest.maxPicks}";
            }
            else if (currentRequest.minPicks == 0)
            {
                text = $"{selectedCards.Count} / {currentRequest.maxPicks} (optional)";
            }
            else
            {
                text =
                    $"{selectedCards.Count} selected (min: {currentRequest.minPicks}, max: {currentRequest.maxPicks})";
            }
            selectionCountLabel.text = text;
        }

        if (selectionProgressFill != null)
        {
            float progress =
                currentRequest.maxPicks > 0
                    ? (float)selectedCards.Count / currentRequest.maxPicks
                    : 0f;
            selectionProgressFill.fillAmount = progress;
        }
    }

    private void OnConfirmClicked()
    {
        if (!CanConfirm())
            return;

        PlaySound(confirmSound);
        ConfirmSelection();
    }

    private void OnCancelClicked()
    {
        if (!currentRequest.allowCancel)
            return;

        CancelSelection();
    }

    private bool CanConfirm()
    {
        if (currentRequest == null)
            return false;

        if (currentRequest.allowEmpty && selectedCards.Count == 0)
            return true;

        return selectedCards.Count >= currentRequest.minPicks;
    }

    private void ConfirmSelection()
    {
        if (currentRequest == null)
            return;

        // Build result list
        List<ScriptableObject> result;

        if (currentRequest.orderMatters)
        {
            // Return in selection order
            result = new List<ScriptableObject>(selectedCards);
        }
        else
        {
            // Return in original card order, but never exceed maxPicks.
            result = new List<ScriptableObject>();
            int max = Mathf.Max(1, currentRequest.maxPicks);

            foreach (var c in currentRequest.cards)
            {
                if (c != null && selectedCards.Contains(c))
                {
                    result.Add(c);
                    if (result.Count >= max)
                        break;
                }
            }
        }

        var callback = currentRequest.onConfirm;

        // Hide first, then callback (so callback can show another choice if needed)
        StartCoroutine(ConfirmCoroutine(callback, result));
    }

    private IEnumerator ConfirmCoroutine(
        System.Action<List<ScriptableObject>> callback,
        List<ScriptableObject> result
    )
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        yield return StartCoroutine(FadePanel(false));

        // Build card ID list for network sync
        var cardIds = new List<string>();
        foreach (var card in result)
        {
            string cardId = GetCardId(card);
            if (!string.IsNullOrEmpty(cardId))
                cardIds.Add(cardId);
        }

        // Fire local event
        OnChoiceConfirmed?.Invoke(choiceOwner, cardIds);

        // Send choice over network if in networked game
        if (NetworkSessionStore.IsNetworkedGame)
        {
            SendChoiceToNetwork(cardIds, wasCancelled: false);
        }

        ClearSpawnedOptions();

        // Resume game flow
        SetGamePaused(false);

        currentRequest = null;
        isShowing = false;
        selectedCards.Clear();
        selectionOrder.Clear();

        // Invoke callback after hide complete
        callback?.Invoke(result);
    }

    /// <summary>
    /// Gets the unique card ID from a ScriptableObject card.
    /// </summary>
    private string GetCardId(ScriptableObject card)
    {
        if (card == null)
            return null;

        // All card types (CreatureCard, EffectCard) extend CardDefinition
        if (card is CardDefinition cardDef)
            return cardDef.cardId;

        return null;
    }

    /// <summary>
    /// Sends the choice result to the remote player.
    /// </summary>
    private void SendChoiceToNetwork(List<string> cardIds, bool wasCancelled)
    {
        var payload = new CardChoicePayload
        {
            owner = choiceOwner,
            choiceContextId = currentChoiceContextId,
            selectedCardIds = cardIds.ToArray(),
            wasCancelled = wasCancelled,
        };

        NetworkMatchManager.Instance?.SendCardChoice(payload);
        Debug.Log(
            $"[CardChoiceManager] Sent choice to network: {cardIds.Count} cards, context={currentChoiceContextId}"
        );
    }

    private void CancelSelection()
    {
        if (currentRequest == null)
            return;

        var callback = currentRequest.onCancel;

        StartCoroutine(CancelCoroutine(callback));
    }

    private IEnumerator CancelCoroutine(System.Action callback)
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        yield return StartCoroutine(FadePanel(false));

        // Send cancel over network if in networked game
        if (NetworkSessionStore.IsNetworkedGame)
        {
            SendChoiceToNetwork(new List<string>(), wasCancelled: true);
        }

        ClearSpawnedOptions();

        // Resume game flow
        SetGamePaused(false);

        currentRequest = null;
        isShowing = false;
        selectedCards.Clear();
        selectionOrder.Clear();

        callback?.Invoke();
    }

    private IEnumerator FadePanel(bool fadeIn)
    {
        if (panelCanvasGroup == null)
            yield break;

        float start = panelCanvasGroup.alpha;
        float end = fadeIn ? 1f : 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        // Enable interaction immediately when fading in
        if (fadeIn)
        {
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            panelCanvasGroup.alpha = Mathf.Lerp(start, end, u);
            yield return null;
        }

        panelCanvasGroup.alpha = end;

        // Disable interaction when fading out
        if (!fadeIn)
        {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        // Use a simple AudioSource.PlayClipAtPoint or your audio manager
        AudioSource.PlayClipAtPoint(clip, Camera.main?.transform.position ?? Vector3.zero);
    }

    // ----- Public Utility Methods -----

    /// <summary>
    /// Quick helper to show a mulligan prompt.
    /// </summary>
    public void ShowMulligan(
        List<ScriptableObject> handCards,
        System.Action<List<ScriptableObject>> onCardsToReplace,
        int maxReplace = -1
    )
    {
        ShowChoice(CardChoiceRequest.Mulligan(handCards, onCardsToReplace, maxReplace));
    }

    /// <summary>
    /// Quick helper to show a "pick one" prompt.
    /// </summary>
    public void ShowPickOne(
        string title,
        List<ScriptableObject> options,
        System.Action<ScriptableObject> onPicked,
        bool canCancel = false
    )
    {
        ShowChoice(CardChoiceRequest.PickOne(title, options, onPicked, canCancel));
    }

    /// <summary>
    /// Quick helper to show a "look at top N, pick M" prompt.
    /// </summary>
    public void ShowLookAndPick(
        string title,
        List<ScriptableObject> topCards,
        int pickCount,
        System.Action<List<ScriptableObject>> onPicked
    )
    {
        ShowChoice(CardChoiceRequest.LookAndPick(title, topCards, pickCount, onPicked));
    }

    // -------------------------------------------------------------------------
    // Network Integration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by NetworkMatchManager when a remote player's choice is received.
    /// Applies the choice locally and triggers the appropriate callbacks.
    /// </summary>
    public void ApplyRemoteChoice(CardChoicePayload payload)
    {
        Debug.Log(
            $"[CardChoiceManager] Applying remote choice: owner={payload.owner} context={payload.choiceContextId} cards={payload.selectedCardIds?.Length ?? 0} cancelled={payload.wasCancelled}"
        );

        // Hide the waiting overlay if showing
        if (isWaitingForRemote)
        {
            HideWaitingOverlay();

            // Resume game flow
            SetGamePaused(false);

            // If we have a pending request, resolve it with the remote choice
            if (pendingRemoteRequest != null)
            {
                if (payload.wasCancelled)
                {
                    // Remote player cancelled
                    pendingRemoteRequest.onCancel?.Invoke();
                }
                else
                {
                    // Convert card IDs back to ScriptableObjects
                    var chosenCards = ResolveCardIds(payload.selectedCardIds);
                    pendingRemoteRequest.onConfirm?.Invoke(chosenCards);
                }

                pendingRemoteRequest = null;
            }
        }
        else
        {
            // Choice received but we weren't waiting - might be a reconnection scenario
            Debug.LogWarning(
                $"[CardChoiceManager] Received remote choice but not waiting for one. Context: {payload.choiceContextId}"
            );
        }

        // Fire the event for any other listeners
        OnChoiceConfirmed?.Invoke(
            payload.owner,
            payload.selectedCardIds?.ToList() ?? new List<string>()
        );
    }

    /// <summary>
    /// Resolves card IDs to ScriptableObjects using the CardDatabase.
    /// </summary>
    private List<ScriptableObject> ResolveCardIds(string[] cardIds)
    {
        var result = new List<ScriptableObject>();

        if (cardIds == null || cardIds.Length == 0)
            return result;

        // Access CardDatabase through DeckManager
        var deckManager = DeckManager.Instance;
        var cardDatabase = deckManager?.cardDatabase;

        if (cardDatabase == null)
        {
            Debug.LogWarning(
                "[CardChoiceManager] CardDatabase not found, cannot resolve card IDs."
            );
            return result;
        }

        foreach (var cardId in cardIds)
        {
            if (string.IsNullOrEmpty(cardId))
                continue;

            // Try to find the card in the database
            var card = cardDatabase.GetById(cardId);
            if (card != null)
            {
                result.Add(card);
            }
            else
            {
                Debug.LogWarning($"[CardChoiceManager] Could not resolve card ID: {cardId}");
            }
        }

        return result;
    }

    /// <summary>
    /// Called when a card effect triggers a choice for a specific player.
    /// If it's the local player, shows the UI. If it's remote, shows the waiting overlay.
    /// </summary>
    public void TriggerEffectChoice(
        CardChoiceRequest request,
        SlotOwner owner,
        string contextId = null
    )
    {
        // Use provided context ID or generate one
        currentChoiceContextId = contextId ?? GenerateContextId(request.title);

        // ShowChoice already handles local vs remote
        ShowChoice(request, owner);
    }
}
