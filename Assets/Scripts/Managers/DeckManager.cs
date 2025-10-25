using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    [Header("Deck Setup")]
    public List<CardData> allCards;
    public GameObject cardPrefab;
    public Transform handPanel;
    public GameObject creaturePrefab;
    public int startingHandSize = 3;
    
    [Header("Deck UI")]
    public Text deckCountText;

    private readonly List<CardData> currentDeck = new List<CardData>();
    private readonly List<CardData> drawPile = new List<CardData>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BuildDeck();
        DrawStartingHand();
    }

    void BuildDeck()
    {
        currentDeck.Clear();
        currentDeck.AddRange(allCards);
        drawPile.Clear();
        drawPile.AddRange(currentDeck);
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = (GameManager.Instance != null) ? GameManager.Instance.NextRandomInt(0, i + 1) : Random.Range(0, i + 1);
            var temp = drawPile[i];
            drawPile[i] = drawPile[j];
            drawPile[j] = temp;
        }
        UpdateDeckUI();
    }

    void DrawStartingHand()
    {
        for (int i = 0; i < startingHandSize; i++)
            DrawCard();
    }

    public void DrawCard()
    {
        CardData data = DrawCardData();
        if (data == null) return;
        CreateCardUI(data);
    }

    void CreateCardUI(CardData data)
    {
        GameObject cardObj = Instantiate(cardPrefab, handPanel);
        CardUI ui = cardObj.GetComponent<CardUI>();
        ui.Initialize(data);
    }

    public bool SpawnCreature(CardData data, BoardSlot slot)
    {
        if (creaturePrefab == null)
        {
            Debug.LogError("Creature prefab not assigned!");
            return false;
        }

        if (slot == null)
            return false;

        if (slot.occupied)
            return false;

        GameObject creatureObj = Instantiate(creaturePrefab, slot.transform.position, Quaternion.identity);
        Creature creature = creatureObj.GetComponent<Creature>();
        creature.Initialize(data);
        creature.owner = slot.owner;
        slot.Occupy(creature);
        return true;
    }

    public int CurrentHandCount()
    {
        if (handPanel == null) return 0;
        return handPanel.childCount;
    }

    public CardData DrawCardData()
    {
        if (drawPile.Count == 0) return null;
        int last = drawPile.Count - 1;
        CardData c = drawPile[last];
        drawPile.RemoveAt(last);
        UpdateDeckUI();
        return c;
    }

    void UpdateDeckUI()
    {
        if (deckCountText != null)
            deckCountText.text = $"Deck: {drawPile.Count}";
    }
}
