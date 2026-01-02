using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    [Header("Deck Setup")]
    [Tooltip(
        "Global card database. If assigned, allCards will be auto-populated from this at runtime."
    )]
    public CardDatabase cardDatabase;

    [Tooltip(
        "Legacy card pool. At runtime this is auto-populated from cardDatabase when available, "
            + "and used as the source for random decks / draft pools."
    )]
    public List<ScriptableObject> allCards;
    public Transform handPanel;
    public GameObject creaturePrefab;

    [Tooltip("Prefab to show as a hover/highlight indicator on a BoardSlot while dragging a card")]
    public GameObject hoverIndicatorPrefab;

    public int startingHandSize = 3;

    [Tooltip("Maximum number of cards allowed in hand")]
    public int maxHandSize = 6;

    [Tooltip("Number of cards drawn automatically at the start of each round")]
    public int cardsPerRound = 2;

    [Tooltip("Size of the deck (used when auto-building a random deck).")]
    public int deckSize = 20;

    [Tooltip("UI prefab for creature cards (fallbacks to cardPrefab if null)")]
    public GameObject creatureCardPrefab;

    [Tooltip("UI prefab for effect cards")]
    public GameObject effectCardPrefab;

    [Header("Draft / External Deck")]
    [Tooltip(
        "If true, DeckManager will auto-build a random deck on Start; set false when using external decks (e.g., draft or database-loaded)."
    )]
    public bool autoBuildOnStart = true;

    [Header("Deck UI")]
    public TMP_Text deckCountText;
    public TMP_Text handCountText;

    [Header("Animation")]
    [Tooltip("Optional animator used to play draw-from-deck animations for the local player.")]
    public CardDrawAnimator cardDrawAnimator;

    [Tooltip("Delay before dealing the player's starting hand (seconds).")]
    public float initialDrawDelay = 1.0f;

    [Tooltip("Extra delay between sequential draw animations (seconds).")]
    public float drawSpacingDelay = 0.15f;

    private readonly List<ScriptableObject> currentDeck = new();
    private readonly List<ScriptableObject> drawPile = new();

    private void Awake()
    {
        Instance = this;

        // Single source of truth for cards: CardDatabase. If it's assigned and allCards
        // is empty, mirror its contents into allCards so existing systems that depend
        // on DeckManager.allCards (draft, debug tools, etc.) continue to work.
        if (cardDatabase != null && (allCards == null || allCards.Count == 0))
        {
            allCards = new List<ScriptableObject>();
            if (cardDatabase.allCards != null)
            {
                foreach (var def in cardDatabase.allCards)
                {
                    if (def != null)
                        allCards.Add(def);
                }
            }
        }
    }

    IEnumerator DrawStartingHandRoutine()
    {
        int canDraw = Mathf.Max(0, maxHandSize - CurrentHandCount());
        int drawNow = Mathf.Min(startingHandSize, canDraw);
        if (drawNow <= 0)
        {
            UpdateHandUI();
            yield break;
        }

        if (initialDrawDelay > 0f)
            yield return new WaitForSeconds(initialDrawDelay);

        // Use the animated multi-draw path when available so the starting hand feels cinematic.
        if (cardDrawAnimator != null)
        {
            yield return StartCoroutine(DrawCardsAnimated(drawNow));
        }
        else
        {
            for (int i = 0; i < drawNow; i++)
            {
                DrawCard();
                if (drawSpacingDelay > 0f && i < drawNow - 1)
                    yield return new WaitForSeconds(drawSpacingDelay);
            }
        }

        UpdateHandUI();
    }

    public void DrawCard()
    {
        if (CurrentHandCount() >= maxHandSize)
            return;
        ScriptableObject data = DrawCardData();
        if (data == null)
            return;

        // If we have a card draw animator, play the full animation for the local player.
        if (cardDrawAnimator != null)
        {
            StartCoroutine(cardDrawAnimator.DrawCardAnimated(data));
        }
        else
        {
            // Fallback: immediate spawn with no animation.
            CreateCardUI(data, triggerLayoutAndUI: true);
        }
    }

    /// <summary>
    /// Creates a card UI GameObject for the given data under the hand panel.
    /// Optionally triggers hand layout and hand-count UI.
    /// </summary>
    public GameObject CreateCardUI(ScriptableObject data, bool triggerLayoutAndUI = true)
    {
        if (data == null || handPanel == null)
            return null;

        GameObject cardObj = null;

        if (data is CreatureCard creatureData)
        {
            if (creatureCardPrefab == null)
            {
                Debug.LogError("Creature card prefab not assigned!");
                return null;
            }
            cardObj = Object.Instantiate(creatureCardPrefab, handPanel);
            CreatureCardUI ui = cardObj.GetComponent<CreatureCardUI>();
            ui?.Initialize(creatureData);
        }
        else if (data is EffectCard effectData)
        {
            if (effectCardPrefab == null)
            {
                Debug.LogError("Effect card prefab not assigned!");
                return null;
            }
            cardObj = Object.Instantiate(effectCardPrefab, handPanel);
            EffectCardUI ui = cardObj.GetComponent<EffectCardUI>();
            if (ui != null)
            {
                ui.Initialize(effectData);
                // Use local player's role for card ownership
                ui.owner = NetworkRoleHelper.LocalRole;
            }
        }

        if (triggerLayoutAndUI)
        {
            var layout = handPanel.GetComponentInParent<HandLayoutController>();
            layout?.RequestLayout();
            UpdateHandUI();
        }

        return cardObj;
    }

    /// <summary>
    /// Spawns a creature on the given board slot.
    /// </summary>
    /// <param name="data">The creature card data to spawn.</param>
    /// <param name="slot">The target board slot.</param>
    /// <param name="explicitOwner">
    /// Optional explicit owner. If provided, overrides the slot's owner.
    /// This is needed for networked games where the guest places on slots with
    /// owner=Player1 but the creature should be owned by Player2.
    /// </param>
    public Creature SpawnCreature(
        CreatureCard data,
        BoardSlot slot,
        SlotOwner? explicitOwner = null
    )
    {
        if (creaturePrefab == null)
        {
            Debug.LogError("Creature prefab not assigned!");
            return null;
        }

        if (slot == null)
            return null;

        if (slot.occupied)
            return null;

        GameObject creatureObj = Instantiate(
            creaturePrefab,
            slot.transform.position,
            Quaternion.identity
        );
        Creature creature = creatureObj.GetComponent<Creature>();
        creature.Initialize(data);
        creature.owner = explicitOwner ?? slot.owner;
        slot.Occupy(creature);
        GameManager.Instance?.OnCreaturePlayedDuringPlacement(creature);
        return creature;
    }

    public int CurrentHandCount()
    {
        if (handPanel == null)
            return 0;
        // Prefer using the hand layout controller's logical count so that cards
        // currently being dragged from the hand still count as "in hand" for
        // max-hand and draw-limit calculations.
        var layout = handPanel.GetComponentInParent<HandLayoutController>();
        if (layout != null)
        {
            return layout.GetLogicalCardCount();
        }

        // Fallback to raw child count if no layout controller is present.
        return handPanel.childCount;
    }

    public ScriptableObject DrawCardData()
    {
        if (drawPile.Count == 0)
            return null;
        int last = drawPile.Count - 1;
        ScriptableObject c = drawPile[last];
        drawPile.RemoveAt(last);
        UpdateDeckUI();
        return c;
    }

    void UpdateDeckUI()
    {
        if (deckCountText != null)
            deckCountText.text = drawPile.Count.ToString();
    }

    public void UpdateHandUI()
    {
        if (handCountText != null)
            handCountText.text = CurrentHandCount().ToString();
    }

    // Public helper for round-based draws (caller: round system)
    public void DrawCardsForRoundStart()
    {
        int canDraw = Mathf.Max(0, maxHandSize - CurrentHandCount());
        int drawNow = Mathf.Min(cardsPerRound, canDraw);
        if (drawNow <= 0)
            return;

        // If we have an animator, run the animated multi-draw sequence; otherwise fall back to instant draws.
        if (cardDrawAnimator != null)
        {
            StartCoroutine(DrawCardsAnimated(drawNow));
        }
        else
        {
            for (int i = 0; i < drawNow; i++)
            {
                DrawCard();
            }
        }
    }

    /// <summary>
    /// Coroutine helper to draw multiple cards in sequence with animation (for player 1).
    /// Falls back to immediate draws if no animator is assigned.
    /// </summary>
    public IEnumerator DrawCardsAnimated(int count)
    {
        if (count <= 0)
            yield break;

        int canDrawTotal = Mathf.Max(0, maxHandSize - CurrentHandCount());
        int toDraw = Mathf.Min(count, canDrawTotal);
        for (int i = 0; i < toDraw; i++)
        {
            ScriptableObject data = DrawCardData();
            if (data == null)
                yield break;

            if (cardDrawAnimator != null)
            {
                yield return StartCoroutine(cardDrawAnimator.DrawCardAnimated(data));
            }
            else
            {
                CreateCardUI(data, triggerLayoutAndUI: true);
                // Give a frame so multiple instant draws don't all apply on the same frame.
                yield return null;
            }

            if (drawSpacingDelay > 0f && i < toDraw - 1)
                yield return new WaitForSeconds(drawSpacingDelay);
        }
    }

    /// <summary>
    /// Called by game flow once the deck content has been provided externally
    /// </summary>
    public void InitializeAndDraw(IReadOnlyList<ScriptableObject> cards)
    {
        currentDeck.Clear();
        drawPile.Clear();

        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;
                currentDeck.Add(card);
                drawPile.Add(card);
            }
        }

        ShuffleDrawPile();

        UpdateDeckUI();

        StartCoroutine(DrawStartingHandRoutine());
    }

    private void ShuffleDrawPile()
    {
        if (drawPile == null || drawPile.Count <= 1)
            return;

        // In networked games, use a per-player sub-stream derived from the
        // shared deterministic seed so that even identical decks produce
        // different orders for each player while remaining deterministic.
        if (NetworkSessionStore.IsNetworkedGame && DeterministicRng.IsInitialized)
        {
            int salt = NetworkRoleHelper.LocalRole == SlotOwner.Player1 ? 1 : 2;
            var rnd = DeterministicRng.CreateSubRandom(salt);

            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(0, i + 1);
                (drawPile[j], drawPile[i]) = (drawPile[i], drawPile[j]);
            }
        }
        else
        {
            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = 0;
                if (GameManager.Instance == null)
                {
                    Debug.LogWarning(
                        "DeckManager: GameManager.Instance is null during ShuffleDrawPile. Determinism may be compromised."
                    );
                    j = UnityEngine.Random.Range(0, i + 1);
                }
                else
                {
                    j = GameManager.Instance.NextRandomInt(0, i + 1);
                }
                (drawPile[j], drawPile[i]) = (drawPile[i], drawPile[j]);
            }
        }
    }

    // ----- Card Choice System Helpers -----

    /// <summary>
    /// Returns the number of cards remaining in the draw pile.
    /// </summary>
    public int DrawPileCount => drawPile.Count;

    /// <summary>
    /// Peek at the top N cards of the draw pile without removing them.
    /// Returns cards in draw order (first element = next to draw).
    /// </summary>
    public List<ScriptableObject> PeekTopCards(int count)
    {
        var result = new List<ScriptableObject>();
        if (drawPile == null || count <= 0)
            return result;

        int toPeek = Mathf.Min(count, drawPile.Count);
        // Top of deck is at the end of the list
        for (int i = 0; i < toPeek; i++)
        {
            int idx = drawPile.Count - 1 - i;
            if (idx >= 0)
                result.Add(drawPile[idx]);
        }
        return result;
    }

    /// <summary>
    /// Remove a specific card from the draw pile (used by scry/discover effects).
    /// Returns true if the card was found and removed.
    /// </summary>
    public bool RemoveFromDrawPile(ScriptableObject card)
    {
        if (card == null || drawPile == null)
            return false;

        bool removed = drawPile.Remove(card);
        if (removed)
            UpdateDeckUI();
        return removed;
    }

    /// <summary>
    /// Add cards to the draw pile and shuffle.
    /// Used when returning cards to deck (mulligan, effects).
    /// </summary>
    public void ShuffleIntoDeck(IEnumerable<ScriptableObject> cards)
    {
        if (cards == null)
            return;

        foreach (var card in cards)
        {
            if (card != null)
                drawPile.Add(card);
        }

        ShuffleDrawPile();
        UpdateDeckUI();
    }

    /// <summary>
    /// Add a card to the bottom of the draw pile (no shuffle).
    /// </summary>
    public void AddToBottomOfDeck(ScriptableObject card)
    {
        if (card == null)
            return;

        drawPile.Insert(0, card);
        UpdateDeckUI();
    }

    /// <summary>
    /// Add a card to the top of the draw pile (next to be drawn).
    /// </summary>
    public void AddToTopOfDeck(ScriptableObject card)
    {
        if (card == null)
            return;

        drawPile.Add(card);
        UpdateDeckUI();
    }

    /// <summary>
    /// Get a read-only view of cards currently in the player's hand.
    /// </summary>
    public List<ScriptableObject> GetHandCards()
    {
        var result = new List<ScriptableObject>();
        if (handPanel == null)
            return result;

        var cardUIs = handPanel.GetComponentsInChildren<BaseCardUI>(includeInactive: false);
        foreach (var cardUI in cardUIs)
        {
            if (cardUI == null)
                continue;

            ScriptableObject data = null;

            if (cardUI is CreatureCardUI creatureUI && creatureUI.Data != null)
            {
                data = creatureUI.Data;
            }
            else if (cardUI is EffectCardUI effectUI && effectUI.Data != null)
            {
                data = effectUI.Data;
            }

            if (data != null)
                result.Add(data);
        }

        return result;
    }

    /// <summary>
    /// Remove a card from the hand UI (used by discard effects).
    /// Returns true if the card was found and removed.
    /// </summary>
    public bool RemoveCardFromHand(ScriptableObject cardData)
    {
        if (cardData == null || handPanel == null)
            return false;

        var cardUIs = handPanel.GetComponentsInChildren<BaseCardUI>(includeInactive: false);
        foreach (var cardUI in cardUIs)
        {
            if (cardUI == null)
                continue;

            bool matches = false;

            if (cardUI is CreatureCardUI creatureUI && creatureUI.Data == cardData)
            {
                matches = true;
            }
            else if (cardUI is EffectCardUI effectUI && effectUI.Data == cardData)
            {
                matches = true;
            }

            if (matches)
            {
                Destroy(cardUI.gameObject);
                UpdateHandUI();

                var layout = handPanel.GetComponentInParent<HandLayoutController>();
                layout?.RequestLayout();

                return true;
            }
        }

        return false;
    }
}
