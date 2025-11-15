using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandLayoutController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField]
    private float maxTotalArcDegrees = 40f;

    [SerializeField]
    private float degreesPerCard = 8f;

    [SerializeField]
    private float overlapFactor = 0.25f; // 0 = no overlap, 0.5 = half width overlap

    [SerializeField]
    private float curveDepth = 40f; // how far center dips down

    [SerializeField]
    private float handYOffset = 0f; // positive moves cards down

    [Header("Hover")]
    [SerializeField]
    private float hoverScale = 1.15f;

    [SerializeField]
    private float hoverLift = 60f;

    [SerializeField]
    private int hoverSortingOrder = 1000;

    [Header("Animation")]
    [SerializeField]
    private float animationDuration = 0.18f;

    [SerializeField]
    private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private RectTransform handRect;
    private BaseCardUI hoveredCard;
    private readonly Dictionary<RectTransform, Coroutine> activeAnimations =
        new Dictionary<RectTransform, Coroutine>();
    private readonly Dictionary<RectTransform, Canvas> tempHoverCanvasByCard =
        new Dictionary<RectTransform, Canvas>();
    private RectTransform draggedCard;
    private readonly Dictionary<RectTransform, Coroutine> activeScaleAnimations =
        new Dictionary<RectTransform, Coroutine>();

    void Awake()
    {
        handRect = GetComponent<RectTransform>();
        var hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
            hlg.enabled = false;
        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
            vlg.enabled = false;
        var grid = GetComponent<GridLayoutGroup>();
        if (grid != null)
            grid.enabled = false;
    }

    void OnEnable()
    {
        RequestLayout();
    }

    void OnTransformChildrenChanged()
    {
        RequestLayout();
    }

    public void RequestLayout()
    {
        LayoutCards();
    }

    public void NotifyHoverEnter(BaseCardUI card)
    {
        hoveredCard = card;
        ApplyHoverCanvas(hoveredCard, true);
        LayoutCards();
    }

    public void NotifyHoverExit(BaseCardUI card)
    {
        if (hoveredCard == card)
        {
            hoveredCard = null;
            ApplyHoverCanvas(card, false);
            LayoutCards();
        }
    }

    public void NotifyDragStart(BaseCardUI card)
    {
        // If the hovered card is being dragged out, clear hover state
        if (hoveredCard == card)
        {
            ApplyHoverCanvas(card, false);
            hoveredCard = null;
        }
        // stop any running animation on this card so it doesn't snap/lerp
        var rt = card.transform as RectTransform;
        draggedCard = rt;
        StopAnimating(rt);
        // animate scale down while dragging (position is controlled by drag logic)
        AnimateScaleOnly(rt, 0.65f);
        LayoutCards();
    }

    public void NotifyDragEnd(BaseCardUI card, bool returnedToHand)
    {
        // When drag ends, just relayout current children
        if (draggedCard == (card != null ? card.transform as RectTransform : null))
        {
            // stop scale-only animation; LayoutCards will restore scale via standard animation
            StopScaleAnimating(draggedCard);
            draggedCard = null;
        }
        LayoutCards();
    }

    private void LayoutCards()
    {
        int count = handRect.childCount;
        if (count == 0)
            return;

        // Determine arc span
        float desiredTotalArc = Mathf.Min(
            maxTotalArcDegrees,
            degreesPerCard * Mathf.Max(0, count - 1)
        );
        float perCardAngle = (count > 1) ? desiredTotalArc / (count - 1) : 0f;

        // Reference card width from first child
        RectTransform first = handRect.GetChild(0) as RectTransform;
        float cardWidth = (first != null) ? first.rect.width : 100f;
        float baseSpacing = cardWidth * (1f - overlapFactor);
        float totalWidth = baseSpacing * Mathf.Max(0, count - 1);
        float halfChord = totalWidth * 0.5f;
        float sagitta = Mathf.Max(0.0001f, curveDepth);
        // radius from sagitta formula so edges ~ y=0 and center dips by sagitta
        float radius = ((halfChord * halfChord) + (sagitta * sagitta)) / (2f * sagitta);

        for (int i = 0; i < count; i++)
        {
            RectTransform rt = handRect.GetChild(i) as RectTransform;
            if (rt == null)
                continue;
            if (draggedCard != null && rt == draggedCard)
                continue; // skip card being dragged

            // normalized position across [0..1]
            float normalized = (count > 1) ? (i / (count - 1f)) : 0.5f;
            float centered = normalized - 0.5f;
            // angle away from center (invert sign from previous)
            float angle = -((i - (count - 1) * 0.5f) * perCardAngle);

            // X along chord
            float x = -halfChord + (baseSpacing * i);
            // Circular baseline: center of circle at (0, -radius); take upper arc and offset down by sagitta
            float underRoot = Mathf.Max(0f, radius * radius - x * x);
            float yOnCircle = -radius + Mathf.Sqrt(underRoot);
            float y = yOnCircle - sagitta - handYOffset;

            Vector2 baselinePos = new Vector2(x, y);
            float targetRot = angle;
            float targetScale = 1f;

            if (hoveredCard != null && rt == hoveredCard.transform as RectTransform)
            {
                baselinePos.y += hoverLift;
                targetScale = hoverScale;
            }

            Vector2 targetPos = BaselineToPivotAnchoredPosition(
                rt,
                baselinePos,
                targetRot,
                targetScale
            );
            AnimateTo(rt, targetPos, targetRot, targetScale);
        }
    }

    private Vector2 BaselineToPivotAnchoredPosition(
        RectTransform rt,
        Vector2 baselinePos,
        float rotationDegrees,
        float scale
    )
    {
        float w = rt.rect.width * scale;
        float h = rt.rect.height * scale;
        Vector2 pivot = rt.pivot; // 0..1
        // offset from pivot to bottom-center in local space (before rotation)
        Vector2 offsetLocal = new Vector2((0.5f - pivot.x) * w, (0f - pivot.y) * h);
        float rad = rotationDegrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        Vector2 offsetRotated = new Vector2(
            c * offsetLocal.x - s * offsetLocal.y,
            s * offsetLocal.x + c * offsetLocal.y
        );
        return baselinePos - offsetRotated;
    }

    private void ApplyHoverCanvas(BaseCardUI card, bool enable)
    {
        if (card == null)
            return;
        RectTransform rt = card.transform as RectTransform;
        if (rt == null)
            return;
        Canvas c;
        if (!tempHoverCanvasByCard.TryGetValue(rt, out c) || c == null)
        {
            c = rt.GetComponent<Canvas>();
            if (c == null)
            {
                c = rt.gameObject.AddComponent<Canvas>();
            }
            tempHoverCanvasByCard[rt] = c;
        }
        c.overrideSorting = enable;
        if (enable)
        {
            c.sortingOrder = hoverSortingOrder;
            // ensure this nested canvas is raycastable while hovered
            var ray = rt.GetComponent<GraphicRaycaster>();
            if (ray == null)
            {
                ray = rt.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }

    private void AnimateTo(
        RectTransform rt,
        Vector2 targetAnchoredPos,
        float targetZRotation,
        float targetScale
    )
    {
        StopAnimating(rt);
        activeAnimations[rt] = StartCoroutine(
            AnimateRoutine(rt, targetAnchoredPos, targetZRotation, targetScale)
        );
    }

    private void StopAnimating(RectTransform rt)
    {
        if (rt == null)
            return;
        if (activeAnimations.TryGetValue(rt, out var running) && running != null)
        {
            StopCoroutine(running);
        }
        activeAnimations[rt] = null;
    }

    private void AnimateScaleOnly(RectTransform rt, float targetScale)
    {
        StopScaleAnimating(rt);
        activeScaleAnimations[rt] = StartCoroutine(AnimateScaleOnlyRoutine(rt, targetScale));
    }

    private void StopScaleAnimating(RectTransform rt)
    {
        if (rt == null)
            return;
        if (activeScaleAnimations.TryGetValue(rt, out var running) && running != null)
        {
            StopCoroutine(running);
        }
        activeScaleAnimations[rt] = null;
    }

    private IEnumerator AnimateScaleOnlyRoutine(RectTransform rt, float targetScale)
    {
        float startScale = rt.localScale.x;
        float t = 0f;
        while (t < animationDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / animationDuration);
            u = easing.Evaluate(u);
            float s = Mathf.LerpUnclamped(startScale, targetScale, u);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(targetScale, targetScale, 1f);
        activeScaleAnimations[rt] = null;
    }

    private IEnumerator AnimateRoutine(
        RectTransform rt,
        Vector2 targetAnchoredPos,
        float targetZRotation,
        float targetScale
    )
    {
        Vector2 startPos = rt.anchoredPosition;
        float startRot = rt.localEulerAngles.z;
        if (startRot > 180f)
            startRot -= 360f; // prefer shortest path
        float startScale = rt.localScale.x;

        float t = 0f;
        while (t < animationDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / animationDuration);
            u = easing.Evaluate(u);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetAnchoredPos, u);
            float z = Mathf.LerpUnclamped(startRot, targetZRotation, u);
            rt.localRotation = Quaternion.Euler(0f, 0f, z);
            float s = Mathf.LerpUnclamped(startScale, targetScale, u);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        rt.anchoredPosition = targetAnchoredPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, targetZRotation);
        rt.localScale = new Vector3(targetScale, targetScale, 1f);
        activeAnimations[rt] = null;
    }
}
