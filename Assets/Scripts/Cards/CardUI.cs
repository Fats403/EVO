using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public CardData Data { get; private set; }

    [Header("UI References")]
    public Image artworkImage;
    public Image backgroundImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statsText;

	private Vector3 originalPosition;
	private Transform originalParent;
	private int originalSiblingIndex;
	private RectTransform rectTransform;
	private Canvas canvas;
	private CanvasGroup canvasGroup;
	private HandLayoutController handLayout;
	private BoardSlot hoverSlot;
	[SerializeField] private float highlightRadius = 100f;

	// Drag smoothing (canvas local space)
	private bool isDragging;
	private Vector2 dragTargetAnchoredPosition;
	private Vector2 dragVelocity; // for SmoothDamp (anchored)
	private const float dragSmoothTime = 0.06f;
	private Vector2 pointerGrabOffset; // keeps where you grabbed relative to card center in canvas space

    public void Initialize(CardData data)
    {
        Data = data;

		rectTransform = GetComponent<RectTransform>();
		canvasGroup = GetComponent<CanvasGroup>();
		canvas = GetComponentInParent<Canvas>();
		handLayout = transform.parent != null ? transform.parent.GetComponentInParent<HandLayoutController>() : null;

        if (nameText != null) nameText.text = data.cardName;
        if (statsText != null) statsText.text = $"Size: {data.size} | Speed: {data.speed} | Tier: {data.tier}";
        if (artworkImage != null) artworkImage.sprite = data.artwork;
        if (backgroundImage != null) backgroundImage.sprite = data.background;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalPosition = transform.localPosition;
		originalSiblingIndex = transform.GetSiblingIndex();
		if (handLayout != null) handLayout.NotifyDragStart(this);
		// move to top-level canvas to avoid being laid out by the hand while dragging
		transform.SetParent(canvas.transform, true); // preserve world position
		// rotate upright and set natural scale for dragging
		rectTransform.localRotation = Quaternion.identity;
		rectTransform.localScale = Vector3.one;
		// compute pointer offset so we keep the grab point consistent (in canvas local space)
		RectTransform canvasRT = canvas.transform as RectTransform;
		Vector2 pointerLocal;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, canvas.worldCamera, out pointerLocal);
		Vector2 cardPivotLocal;
		{
			Vector2 cardScreen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, cardScreen, canvas.worldCamera, out cardPivotLocal);
		}
		pointerGrabOffset = cardPivotLocal - pointerLocal;
		// start drag smoothing from the exact grab point to avoid 1-frame snap
		dragTargetAnchoredPosition = pointerLocal + pointerGrabOffset;
		rectTransform.anchoredPosition = dragTargetAnchoredPosition;
		dragVelocity = Vector2.zero;
		isDragging = true;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
		// set target under the mouse with the original grab offset; Update() will ease towards it
		RectTransform canvasRT = canvas.transform as RectTransform;
		Vector2 pointerLocal;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, canvas.worldCamera, out pointerLocal);
		dragTargetAnchoredPosition = pointerLocal + pointerGrabOffset;

		// update slot highlight
		UpdateHoverSlot(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
		isDragging = false;

        // Find closest board slot (world-space)
        BoardSlot closest = FindClosestSlot(eventData.position);
		bool placed = false;
		if (closest != null && Vector2.Distance(closest.ScreenPosition, eventData.position) < highlightRadius)
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
			if (handLayout != null) handLayout.NotifyDragEnd(this, false);
			if (hoverSlot != null)
			{
				hoverSlot.HideHoverIndicator();
				hoverSlot = null;
			}
			Destroy(gameObject);
		}
		else
		{
			// Return card to hand, preserving world position, then layout will animate back
			transform.SetParent(originalParent, true);
			transform.SetSiblingIndex(originalSiblingIndex); // go back to original slot
			if (handLayout == null && originalParent != null)
				handLayout = originalParent.GetComponentInParent<HandLayoutController>();
			if (handLayout == null && originalParent != null)
				handLayout = originalParent.GetComponentInParent<HandLayoutController>();
			if (handLayout != null) handLayout.NotifyDragEnd(this, true);
			if (hoverSlot != null)
			{
				hoverSlot.HideHoverIndicator();
				hoverSlot = null;
			}
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

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (handLayout == null)
		{
			handLayout = transform.parent != null ? transform.parent.GetComponentInParent<HandLayoutController>() : null;
		}
		if (handLayout != null)
		{
			handLayout.NotifyHoverEnter(this);
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (handLayout != null)
		{
			handLayout.NotifyHoverExit(this);
		}
	}

	void Update()
	{
		if (isDragging)
		{
			// Smoothly move towards target anchored position while dragging
			rectTransform.anchoredPosition = Vector2.SmoothDamp(rectTransform.anchoredPosition, dragTargetAnchoredPosition, ref dragVelocity, dragSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
		}
	}

	private void UpdateHoverSlot(Vector2 screenPointer)
	{
		bool canPlaceNow = GameManager.Instance != null && GameManager.Instance.currentPhase == GamePhase.Place;
		if (!canPlaceNow)
		{
			if (hoverSlot != null) { hoverSlot.HideHoverIndicator(); hoverSlot = null; }
			return;
		}

		BoardSlot closest = FindClosestSlot(screenPointer);
		if (closest == null)
		{
			if (hoverSlot != null) { hoverSlot.HideHoverIndicator(); hoverSlot = null; }
			return;
		}

		float dist = Vector2.Distance(closest.ScreenPosition, screenPointer);
		if (dist > highlightRadius || closest.owner != SlotOwner.Player1 || closest.occupied)
		{
			if (hoverSlot != null) { hoverSlot.HideHoverIndicator(); hoverSlot = null; }
			return;
		}

		if (hoverSlot != null && hoverSlot != closest)
		{
			hoverSlot.HideHoverIndicator();
		}

		if (closest != hoverSlot)
		{
			var prefab = DeckManager.Instance != null ? DeckManager.Instance.hoverIndicatorPrefab : null;
			closest.ShowHoverIndicator(prefab);
			hoverSlot = closest;
		}
	}
}
