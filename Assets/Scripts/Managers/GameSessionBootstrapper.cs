using UnityEngine;

/// <summary>
/// Optional helper MonoBehaviour that owns the startup flow for constructed
/// games. It reads the SelectedDeckStore (via a ConstructedDeckProvider),
/// initialises the DeckManager, and then hands off to GameManager once the
/// player's deck is ready.
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

    private void Awake()
    {
        // Signal to GameManager that an external bootstrapper is responsible
        // for constructed deck initialisation this session.
        GameManager.MarkExternallyBootstrapped();
    }

    private void Start()
    {
        // For now we only bootstrap constructed games. Draft mode continues to be
        // handled by GameManager directly until the draft flow is moved into the
        // DeckHubScene.
        if (SelectedDeckStore.Mode != GameStartMode.Constructed)
            return;

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (constructiveRefsInvalid())
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

        deckManager.InitializeFromDraft(cards);
        deckManager.InitializeAndDraw();

        // Clear the selection so future sessions don't accidentally reuse it.
        SelectedDeckStore.Clear();

        // Hand off control to the normal game flow.
        gameManager.BeginGameWithReadyDeck();
    }

    private bool constructiveRefsInvalid()
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
