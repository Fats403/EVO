using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    [Header("Deck Setup")]
    public List<ScriptableObject> allCards;
    public GameObject cardPrefab;
    public Transform handPanel;
    public GameObject creaturePrefab;
	[Tooltip("Prefab to show as a hover/highlight indicator on a BoardSlot while dragging a card")]
	public GameObject hoverIndicatorPrefab;
    public int startingHandSize = 3;
    [Tooltip("UI prefab for creature cards (fallbacks to cardPrefab if null)")]
    public GameObject creatureCardPrefab;
    [Tooltip("UI prefab for effect cards")]
    public GameObject effectCardPrefab;
    
    [Header("Deck UI")]
    public Text deckCountText;

    private readonly List<ScriptableObject> currentDeck = new List<ScriptableObject>();
    private readonly List<ScriptableObject> drawPile = new List<ScriptableObject>();

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
        ScriptableObject data = DrawCardData();
        if (data == null) return;
        CreateCardUI(data);
    }

    void CreateCardUI(ScriptableObject data)
    {
        if (data is CreatureCard creatureData)
        {
            GameObject prefab = creatureCardPrefab != null ? creatureCardPrefab : cardPrefab;
            if (prefab == null) { Debug.LogError("Creature card prefab not assigned!"); return; }
            GameObject cardObj = Instantiate(prefab, handPanel);
            CreatureCardUI ui = cardObj.GetComponent<CreatureCardUI>();
            if (ui != null) ui.Initialize(creatureData);
        }
        else if (data is EffectCard effectData)
        {
            if (effectCardPrefab == null) { Debug.LogError("Effect card prefab not assigned!"); return; }
            GameObject cardObj = Instantiate(effectCardPrefab, handPanel);
            EffectCardUI ui = cardObj.GetComponent<EffectCardUI>();
            if (ui != null)
            {
                ui.Initialize(effectData);
                ui.owner = SlotOwner.Player1;
            }
        }

		var layout = handPanel != null ? handPanel.GetComponentInParent<HandLayoutController>() : null;
		if (layout != null) layout.RequestLayout();
    }

    public bool SpawnCreature(CreatureCard data, BoardSlot slot)
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

    public ScriptableObject DrawCardData()
    {
        if (drawPile.Count == 0) return null;
        int last = drawPile.Count - 1;
        ScriptableObject c = drawPile[last];
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
