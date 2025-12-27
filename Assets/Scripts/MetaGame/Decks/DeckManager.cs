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

    private bool deckInitialized;

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

    // DeckManager no longer auto-builds or draws a starting hand on Start().
    // The deck is expected to be initialized explicitly via InitializeRandomDeck()
    // or InitializeFromDraft(), followed by InitializeAndDraw() when appropriate.

    void BuildDeck()
    {
        currentDeck.Clear();
        // Source of truth: allCards; build a unique deck of size deckSize
        var pool = new List<ScriptableObject>(allCards ?? new List<ScriptableObject>());
        // Shuffle pool
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = 0;
            if (GameManager.Instance == null)
            {
                Debug.LogWarning(
                    "DeckManager: GameManager.Instance is null during shuffle. Determinism may be compromised."
                );
                j = UnityEngine.Random.Range(0, i + 1);
            }
            else
            {
                j = GameManager.Instance.NextRandomInt(0, i + 1);
            }
            (pool[j], pool[i]) = (pool[i], pool[j]);
        }
        // Take up to deckSize unique
        var picked = new List<ScriptableObject>(deckSize);
        var seen = new System.Collections.Generic.HashSet<ScriptableObject>();
        for (int i = 0; i < pool.Count && picked.Count < deckSize; i++)
        {
            var card = pool[i];
            if (card == null)
                continue;
            if (seen.Add(card))
                picked.Add(card);
        }

        currentDeck.AddRange(picked);
        drawPile.Clear();
        drawPile.AddRange(currentDeck);

        // Shuffle draw order
        ShuffleDrawPile();
        UpdateDeckUI();
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
                ui.owner = SlotOwner.Player1;
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

    public Creature SpawnCreature(CreatureCard data, BoardSlot slot)
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
        creature.owner = slot.owner;
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
    /// Build a fresh random deck from allCards using the configured deckSize.
    /// Does not draw any cards; caller is responsible for triggering the starting hand draw.
    /// </summary>
    public void InitializeRandomDeck()
    {
        BuildDeck();
        deckInitialized = true;
    }

    /// <summary>
    /// Initialize the internal deck state from an externally provided list
    /// (e.g., a drafted deck or a deck loaded from a database).
    /// </summary>
    public void InitializeFromDraft(IReadOnlyList<ScriptableObject> draftedCards)
    {
        currentDeck.Clear();
        drawPile.Clear();

        if (draftedCards != null)
        {
            for (int i = 0; i < draftedCards.Count; i++)
            {
                var card = draftedCards[i];
                if (card == null)
                    continue;
                currentDeck.Add(card);
                drawPile.Add(card);
            }
        }

        ShuffleDrawPile();
        deckInitialized = true;
        UpdateDeckUI();
    }

    /// <summary>
    /// Called by game flow once the deck content has been provided externally
    /// (e.g., after draft completes) to shuffle and deal the starting hand.
    /// </summary>
    public void InitializeAndDraw()
    {
        if (!deckInitialized)
        {
            Debug.LogWarning("DeckManager.InitializeAndDraw called before deck was initialized.");
            return;
        }

        // Ensure draw pile is shuffled before drawing the starting hand.
        ShuffleDrawPile();
        StartCoroutine(DrawStartingHandRoutine());
    }

    private void ShuffleDrawPile()
    {
        if (drawPile == null || drawPile.Count <= 1)
            return;

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
