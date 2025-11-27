using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    [Header("Deck Setup")]
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

    [Tooltip("Size of the deck")]
    public int deckSize = 20;

    [Tooltip("UI prefab for creature cards (fallbacks to cardPrefab if null)")]
    public GameObject creatureCardPrefab;

    [Tooltip("UI prefab for effect cards")]
    public GameObject effectCardPrefab;

    [Header("Deck UI")]
    public TMP_Text deckCountText;
    public TMP_Text handCountText;

    private readonly List<ScriptableObject> currentDeck = new();
    private readonly List<ScriptableObject> drawPile = new();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BuildDeck();
        DrawStartingHand();
        UpdateHandUI();
    }

    void BuildDeck()
    {
        currentDeck.Clear();
        // Source of truth: allCards; build a unique deck of size deckSize
        var pool = new List<ScriptableObject>(allCards ?? new List<ScriptableObject>());
        // Shuffle pool
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j =
                (GameManager.Instance != null)
                    ? GameManager.Instance.NextRandomInt(0, i + 1)
                    : Random.Range(0, i + 1);
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
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j =
                (GameManager.Instance != null)
                    ? GameManager.Instance.NextRandomInt(0, i + 1)
                    : Random.Range(0, i + 1);
            (drawPile[j], drawPile[i]) = (drawPile[i], drawPile[j]);
        }
        UpdateDeckUI();
    }

    void DrawStartingHand()
    {
        int canDraw = Mathf.Max(0, maxHandSize - CurrentHandCount());
        int drawNow = Mathf.Min(startingHandSize, canDraw);
        for (int i = 0; i < drawNow; i++)
        {
            DrawCard();
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
        CreateCardUI(data);
        UpdateHandUI();
    }

    void CreateCardUI(ScriptableObject data)
    {
        if (data is CreatureCard creatureData)
        {
            if (creatureCardPrefab == null)
            {
                Debug.LogError("Creature card prefab not assigned!");
                return;
            }
            GameObject cardObj = Instantiate(creatureCardPrefab, handPanel);
            CreatureCardUI ui = cardObj.GetComponent<CreatureCardUI>();
            ui?.Initialize(creatureData);
        }
        else if (data is EffectCard effectData)
        {
            if (effectCardPrefab == null)
            {
                Debug.LogError("Effect card prefab not assigned!");
                return;
            }
            GameObject cardObj = Instantiate(effectCardPrefab, handPanel);
            EffectCardUI ui = cardObj.GetComponent<EffectCardUI>();
            if (ui != null)
            {
                ui.Initialize(effectData);
                ui.owner = SlotOwner.Player1;
            }
        }

        var layout = handPanel?.GetComponentInParent<HandLayoutController>();
        layout?.RequestLayout();
        UpdateHandUI();
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
        for (int i = 0; i < drawNow; i++)
        {
            DrawCard();
        }
    }
}
