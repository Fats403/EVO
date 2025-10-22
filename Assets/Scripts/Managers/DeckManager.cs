using UnityEngine;
using System.Collections.Generic;

public class DeckManager : MonoBehaviour
{
    [Header("Deck Setup")]
    public List<CardData> allCards;     // Assign via Inspector
    public GameObject cardPrefab;       // Card_UI prefab
    public Transform handPanel;         // HandPanel reference
    public int startingHandSize = 3;

    private List<CardData> currentDeck = new List<CardData>();

    private void Start()
    {
        BuildDeck();
        DrawStartingHand();
    }

    void BuildDeck()
    {
        currentDeck.Clear();
        currentDeck.AddRange(allCards); // In a real version: shuffle / select subset
    }

    void DrawStartingHand()
    {
        for (int i = 0; i < startingHandSize; i++)
            DrawCard();
    }

    public void DrawCard()
    {
        if (currentDeck.Count == 0) return;

        CardData data = currentDeck[Random.Range(0, currentDeck.Count)];
        CreateCardUI(data);
    }

    void CreateCardUI(CardData data)
    {
        GameObject cardObj = Instantiate(cardPrefab, handPanel);
        CardUI ui = cardObj.GetComponent<CardUI>();
        ui.Initialize(data, OnCardClicked);
    }

    void OnCardClicked(CardUI clickedCard)
    {
        Debug.Log($"Clicked: {clickedCard.Data.cardName} | Type: {clickedCard.Data.type} | Size: {clickedCard.Data.size}");
        // Will trigger placement logic in Phase 3
    }
}
