using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardData Data { get; private set; }

    [Header("UI References")]
    public Image artworkImage;
    public Image backgroundImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statsText;

    private Vector3 originalPosition;
    private Transform originalParent;
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    public void Initialize(CardData data)
    {
        Data = data;

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();

        if (nameText != null) nameText.text = data.cardName;
        if (statsText != null) statsText.text = $"Size: {data.size} | Speed: {data.speed} | Tier: {data.tier}";
        if (artworkImage != null) artworkImage.sprite = data.artwork;
        if (backgroundImage != null) backgroundImage.sprite = data.background;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalPosition = transform.localPosition;
        transform.SetParent(canvas.transform, true);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // Find closest board slot (world-space)
        BoardSlot closest = FindClosestSlot(eventData.position);
		bool placed = false;
		if (closest != null && Vector2.Distance(closest.ScreenPosition, eventData.position) < 100f)
		{
			if (closest.owner == SlotOwner.Player1)
			{
				if (GameManager.Instance != null && GameManager.Instance.currentPhase == GamePhase.Place)
				{
					placed = DeckManager.Instance.SpawnCreature(Data, closest);
				}
			}
		}

		if (placed)
		{
			Destroy(gameObject);
		}
		else
		{
			// Return card to hand
			transform.SetParent(originalParent, false);
			transform.localPosition = originalPosition;
		}
    }

    private BoardSlot FindClosestSlot(Vector2 screenPos)
    {
        BoardSlot[] slots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
        float best = float.MaxValue;
        BoardSlot nearest = null;

        foreach (var s in slots)
        {
            float d = Vector2.Distance(s.ScreenPosition, screenPos);
            if (d < best)
            {
                best = d;
                nearest = s;
            }
        }

        return nearest;
    }
}
