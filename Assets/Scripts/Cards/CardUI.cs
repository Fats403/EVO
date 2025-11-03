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
    public TMP_Text nameText;
    public TMP_Text tierText;
    public TMP_Text speedText;
    public TMP_Text bodyText;
    public TMP_Text healthText;
    public TMP_Text traitDescText;

	private Vector3 originalPosition;
	private Transform originalParent;
	private int originalSiblingIndex;
	private RectTransform rectTransform;
	private Canvas canvas;
	private CanvasGroup canvasGroup;
	private HandLayoutController handLayout;
	private BoardSlot hoverSlot;
	[SerializeField] private readonly float highlightRadius = 100f;

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
		handLayout = transform.parent?.GetComponentInParent<HandLayoutController>();

        if (nameText != null) nameText.text = data.cardName;
        if (tierText != null) tierText.text = $"Tier: {data.tier}";
        if (speedText != null) speedText.text = $"Speed: {data.speed}";
        if (bodyText != null) bodyText.text = $"Size: {data.size}";
        if (healthText != null) healthText.text = $"{data.maxHealth}";
        if (artworkImage != null) artworkImage.sprite = data.artwork;
        if (backgroundImage != null) backgroundImage.sprite = data.background;

        if (traitDescText != null)
        {
            string traitLine = "";
            if (data.baseTraits != null && data.baseTraits.Length > 0 && data.baseTraits[0] != null)
            {
                var t = data.baseTraits[0];
                string tName = string.IsNullOrEmpty(t.traitName) ? t.name : t.traitName;
                if (!string.IsNullOrEmpty(t.description)) traitLine = $"{tName}: {t.description}";
                else traitLine = tName;
            }
            traitDescText.text = traitLine;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalPosition = transform.localPosition;
		originalSiblingIndex = transform.GetSiblingIndex();
		handLayout?.NotifyDragStart(this);
		// move to top-level canvas to avoid being laid out by the hand while dragging
		transform.SetParent(canvas.transform, true); // preserve world position
		// rotate upright and set natural scale for dragging
		rectTransform.localRotation = Quaternion.identity;
		rectTransform.localScale = Vector3.one;
		// compute pointer offset so we keep the grab point consistent (in canvas local space)
		RectTransform canvasRT = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, canvas.worldCamera, out Vector2 pointerLocal);
        Vector2 cardScreen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, cardScreen, canvas.worldCamera, out Vector2 cardPivotLocal);
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
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, canvas.worldCamera, out Vector2 pointerLocal);
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
			handLayout?.NotifyDragEnd(this, false);
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

			handLayout?.NotifyDragEnd(this, true);
			
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
			handLayout = transform.parent?.GetComponentInParent<HandLayoutController>();
		}
		
		handLayout?.NotifyHoverEnter(this);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		handLayout?.NotifyHoverExit(this);
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
			var prefab = DeckManager.Instance?.hoverIndicatorPrefab;
			closest.ShowHoverIndicator(prefab);
			hoverSlot = closest;
		}
	}
}
