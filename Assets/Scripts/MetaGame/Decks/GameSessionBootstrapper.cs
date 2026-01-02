using UnityEngine;

/// <summary>
/// Optional helper MonoBehaviour that owns the startup flow for constructed
/// and networked games. It reads deck data from SelectedDeckStore or
/// NetworkSessionStore, initialises the DeckManager, and then hands off to
/// GameManager once the player's deck is ready.
///
/// If this component is not present in a scene, GameManager falls back to
/// its legacy startup behaviour so existing scenes continue to work.
/// </summary>
public class GameSessionBootstrapper : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField]
    private GameManager gameManager;

    [SerializeField]
    private DeckManager deckManager;

    [SerializeField]
    private CardDatabase cardDatabase;

    [Header("Deck Providers")]
    [SerializeField]
    private ConstructedDeckProvider constructedProvider;

    private void Start()
    {
        // Network mode takes priority - deck data comes from session header
        if (NetworkSessionStore.IsNetworkedGame)
        {
            InitializeNetworkMode();
            return;
        }

        // Constructed mode (single player vs AI with a pre-built deck)
        if (SelectedDeckStore.Mode == GameStartMode.Constructed)
        {
            InitializeConstructedMode();
            return;
        }

        // No valid mode detected - let GameManager handle it
        Debug.Log("GameSessionBootstrapper: No deck mode detected, deferring to GameManager.");
    }

    /// <summary>
    /// Initializes decks for a networked game using data from the session header.
    /// </summary>
    private void InitializeNetworkMode()
    {
        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (deckManager == null)
            deckManager = DeckManager.Instance;

        if (cardDatabase == null)
            cardDatabase = gameManager?.cardDatabase;

        if (deckManager == null || cardDatabase == null || gameManager == null)
        {
            Debug.LogError("GameSessionBootstrapper: Missing references for network mode.");
            return;
        }

        var header = NetworkSessionStore.CurrentHeader.Value;

        // CRITICAL: Both host and guest must re-initialize the RNG from the header
        // seed when the game scene loads. This ensures both start at RNG call #0
        // with the same seed, regardless of how many RNG calls were made during
        // draft/lobby phases.
        Debug.Log(
            $"GameSessionBootstrapper: Initializing DeterministicRng with seed {header.rngSeed} (resetting call count to 0)"
        );
        DeterministicRng.Initialize(header.rngSeed);
        bool isHost = header.localRole == SlotOwner.Player1;

        // Determine which deck is ours vs opponent's
        var localDeckEntries = isHost ? header.hostDeck : header.guestDeck;

        // For the host, the guest deck comes from the ACK and may be stored separately.
        // For the guest, the host deck is already in the header.
        DeckCardEntry[] remoteDeckEntries;
        if (isHost)
        {
            // Host: Try to get guest deck from SteamLobbyManager (received via ACK)
            // If not available there, fall back to header (which may be empty initially)
            var lobbyManager = SteamLobbyManager.Instance;
            var pendingGuestDeck = lobbyManager?.GetPendingGuestDeck();
            remoteDeckEntries =
                (pendingGuestDeck != null && pendingGuestDeck.Length > 0)
                    ? pendingGuestDeck
                    : header.guestDeck;
        }
        else
        {
            // Guest: Host deck is always in the header
            remoteDeckEntries = header.hostDeck;
        }

        // Build local deck from entries
        var localDef = new DeckDefinition();
        if (localDeckEntries != null)
        {
            foreach (var entry in localDeckEntries)
                localDef.cards.Add(entry);
        }

        var localCards = localDef.ToCardAssets(cardDatabase);
        if (localCards.Count == 0)
        {
            Debug.LogError("GameSessionBootstrapper: Network deck contained no valid cards.");
            return;
        }

        // Initialize local player's deck
        deckManager.InitializeAndDraw(localCards);

        // Initialize opponent tracker (if present in scene)
        if (
            OpponentDeckTracker.Instance != null
            && remoteDeckEntries != null
            && remoteDeckEntries.Length > 0
        )
        {
            OpponentDeckTracker.Instance.Initialize(remoteDeckEntries);

            // Assume the opponent drew their starting hand using the same rules as DeckManager.
            // This keeps the opponent hand/deck counts in sync from turn 1.
            OpponentDeckTracker.Instance.OnOpponentDrew(deckManager.startingHandSize);
        }
        else if (OpponentDeckTracker.Instance != null)
        {
            Debug.LogWarning(
                "GameSessionBootstrapper: Could not initialize opponent tracker - remote deck data unavailable."
            );
        }

        Debug.Log(
            $"GameSessionBootstrapper: Network mode initialized. Local={localCards.Count} cards, isHost={isHost}"
        );

        // Initialize checkpoint manager for reconnection support
        InitializeCheckpointManager(header);

        // Hand off to game flow
        gameManager.BeginGameWithReadyDeck();
    }

    /// <summary>
    /// Initializes the MatchCheckpointManager for Firestore checkpoint storage.
    /// </summary>
    private void InitializeCheckpointManager(NetSessionHeader header)
    {
        if (MatchCheckpointManager.Instance == null)
        {
            Debug.LogWarning(
                "GameSessionBootstrapper: MatchCheckpointManager not found in scene. Checkpoints disabled."
            );
            return;
        }

        // Determine opponent name and if we're host
        bool isHost = header.localRole == SlotOwner.Player1;
        string hostName = SteamLobbyManager.Instance?.HostName ?? "Player 1";
        string guestName = SteamLobbyManager.Instance?.GuestName ?? "Player 2";
        string opponentName = isHost ? guestName : hostName;

        // Initialize with both player IDs and opponent info
        MatchCheckpointManager.Instance.InitializeForMatch(
            header.hostId,
            header.guestId,
            opponentName,
            isHost
        );

        // Update match metadata in Firestore (fire and forget - don't block game start)
        _ = MatchCheckpointManager.Instance.UpdateMatchMetadataAsync(
            header.hostId,
            header.guestId,
            hostName,
            guestName
        );

        Debug.Log(
            $"GameSessionBootstrapper: MatchCheckpointManager initialized for match with opponent '{opponentName}'."
        );
    }

    /// <summary>
    /// Initializes decks for a constructed (vs AI) game using SelectedDeckStore.
    /// </summary>
    private void InitializeConstructedMode()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (ConstructiveRefsInvalid())
            return;

        // Build the deck definition from the store, then convert to concrete assets
        // and feed into DeckManager.
        var deckDef = constructedProvider.BuildLocalDeckDefinition();
        if (deckDef == null)
        {
            Debug.LogError("GameSessionBootstrapper: Failed to build constructed deck definition.");
            return;
        }

        var cards = deckDef.ToCardAssets(cardDatabase);
        if (cards.Count == 0)
        {
            Debug.LogError("GameSessionBootstrapper: Constructed deck contained no valid cards.");
            return;
        }

        deckManager.InitializeAndDraw(cards);

        // Clear the selection so future sessions don't accidentally reuse it.
        SelectedDeckStore.Clear();

        // Hand off control to the normal game flow.
        gameManager.BeginGameWithReadyDeck();
    }

    private bool ConstructiveRefsInvalid()
    {
        bool valid = true;

        if (deckManager == null)
        {
            deckManager = DeckManager.Instance;
        }

        if (deckManager == null)
        {
            Debug.LogError("GameSessionBootstrapper: DeckManager reference is missing.");
            valid = false;
        }

        if (cardDatabase == null)
        {
            cardDatabase = gameManager != null ? gameManager.cardDatabase : null;
        }

        if (cardDatabase == null)
        {
            Debug.LogError("GameSessionBootstrapper: CardDatabase reference is missing.");
            valid = false;
        }

        if (constructedProvider == null)
        {
            constructedProvider = FindAnyObjectByType<ConstructedDeckProvider>();
        }

        if (constructedProvider == null)
        {
            Debug.LogError(
                "GameSessionBootstrapper: ConstructedDeckProvider reference is missing."
            );
            valid = false;
        }

        if (gameManager == null)
        {
            Debug.LogError("GameSessionBootstrapper: GameManager reference is missing.");
            valid = false;
        }

        return !valid;
    }
}
