using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public abstract class BaseCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Base UI")]
    protected RectTransform rectTransform;
    protected Canvas canvas;
    protected CanvasGroup canvasGroup;
    protected HandLayoutController handLayout;

    protected Vector3 originalPosition;
    protected Transform originalParent;
    protected int originalSiblingIndex;

    [SerializeField] protected readonly float highlightRadius = 100f;

    // Drag smoothing (canvas local space)
    protected bool isDragging;
    protected Vector2 dragTargetAnchoredPosition;
    protected Vector2 dragVelocity; // for SmoothDamp (anchored)
    protected const float dragSmoothTime = 0.06f;
    protected Vector2 pointerGrabOffset; // keeps where you grabbed relative to card center in canvas space

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        handLayout = transform.parent?.GetComponentInParent<HandLayoutController>();
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
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

    public virtual void OnDrag(PointerEventData eventData)
    {
        // set target under the mouse with the original grab offset; Update() will ease towards it
        RectTransform canvasRT = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, canvas.worldCamera, out Vector2 pointerLocal);
        dragTargetAnchoredPosition = pointerLocal + pointerGrabOffset;
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        isDragging = false;
        // default: return to hand (subclasses can override to consume or place)
        ReturnToHand();
    }

    protected void ReturnToHand()
    {
        // Return card to hand, preserving world position, then layout will animate back
        transform.SetParent(originalParent, true);
        transform.SetSiblingIndex(originalSiblingIndex); // go back to original slot

        if (handLayout == null && originalParent != null)
            handLayout = originalParent.GetComponentInParent<HandLayoutController>();

        handLayout?.NotifyDragEnd(this, true);
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (handLayout == null)
        {
            handLayout = transform.parent?.GetComponentInParent<HandLayoutController>();
        }
        handLayout?.NotifyHoverEnter(this);
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        handLayout?.NotifyHoverExit(this);
    }

    protected virtual void Update()
    {
        if (isDragging)
        {
            // Smoothly move towards target anchored position while dragging
            rectTransform.anchoredPosition = Vector2.SmoothDamp(rectTransform.anchoredPosition, dragTargetAnchoredPosition, ref dragVelocity, dragSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}


