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
        UpdateDeckUI();
    }

    void DrawStartingHand()
    {
        for (int i = 0; i < startingHandSize; i++)
            DrawCard();
    }

    public void DrawCard()
    {
        if (currentDeck.Count == 0)
            return;
        int idx = Random.Range(0, currentDeck.Count);
        CardData data = currentDeck[idx];
        currentDeck.RemoveAt(idx);
        UpdateDeckUI();
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
        slot.Occupy(creature);
        return true;
    }

    void UpdateDeckUI()
    {
        if (deckCountText != null)
            deckCountText.text = $"Deck: {currentDeck.Count}";
    }
}
