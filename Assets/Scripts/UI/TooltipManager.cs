using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct TooltipData
{
    public string title;
    public string body;
    public Sprite icon;

    // Optional dedicated stacks display (e.g., for status effects).
    public bool hasStacks;
    public int stacks;
}

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Wiring")]
    public Canvas canvas;
    public RectTransform root;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public TextMeshProUGUI stacksText;
    public Image iconImage;
    public GameObject iconContainer;

    [Header("Behavior")]
    [Tooltip("Offset from the mouse in screen pixels (positive X = right, positive Y = up).")]
    public Vector2 screenOffset = new Vector2(300f, 100f);

    [Tooltip("If true, flips the tooltip offset when near screen edges to help keep it on-screen.")]
    public bool flipToStayOnScreen = true;

    [Tooltip("Sprite used when no specific icon is supplied.")]
    public Sprite defaultIcon;

    private object currentOwner;
    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    [Header("Fade")]
    [Tooltip("Seconds to fade the tooltip in when it appears.")]
    public float fadeInDuration = 0.12f;

    [Tooltip("Seconds to fade the tooltip out when it hides.")]
    public float fadeOutDuration = 0.08f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Persist across scene loads so tooltips are available in all game scenes.
        DontDestroyOnLoad(gameObject);
        // Expect explicit wiring via inspector; keep this class simple.

        if (root != null)
        {
            canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = root.gameObject.AddComponent<CanvasGroup>();

            // Tooltips should not block other UI.
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }

        HideImmediate();
    }

    public void Show(TooltipData data, Vector2 screenPosition, object owner = null)
    {
        if (canvas == null || root == null)
            return;

        currentOwner = owner;

        root.gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = data.title ?? string.Empty;

        if (bodyText != null)
            bodyText.text = data.body ?? string.Empty;

        // Optional stacks readout (only used by some tooltips, e.g., statuses).
        if (stacksText != null)
        {
            if (data.hasStacks)
            {
                stacksText.gameObject.SetActive(true);
                stacksText.text = $"x{data.stacks}";
            }
            else
            {
                stacksText.gameObject.SetActive(false);
                stacksText.text = string.Empty;
            }
        }

        Sprite iconToUse = data.icon != null ? data.icon : defaultIcon;

        if (iconContainer != null)
            iconContainer.SetActive(iconToUse != null);

        if (iconImage != null)
            iconImage.sprite = iconToUse;

        UpdatePosition(screenPosition);

        // Fade in
        if (canvasGroup != null)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha: 1f, fadeInDuration));
        }
    }

    public void UpdatePosition(Vector2 screenPosition)
    {
        if (canvas == null || root == null)
            return;

        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        // Start with the configured offset, then optionally flip it based on which half
        // of the screen the cursor is in so the tooltip tends to stay on-screen.
        Vector2 offset = screenOffset;
        if (flipToStayOnScreen)
        {
            float halfW = Screen.width * 0.5f;
            float halfH = Screen.height * 0.5f;
            offset.x =
                (screenPosition.x > halfW) ? -Mathf.Abs(screenOffset.x) : Mathf.Abs(screenOffset.x);
            offset.y =
                (screenPosition.y > halfH) ? -Mathf.Abs(screenOffset.y) : Mathf.Abs(screenOffset.y);
        }

        // Convert cursor position to local canvas space and apply offset.
        Vector2 localPoint;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition + offset,
            cam,
            out localPoint
        );

        // First place the tooltip at the desired point.
        root.anchoredPosition = localPoint;

        // Make sure layout is up-to-date so bounds are correct.
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        // Now compute the tooltip's bounds relative to the canvas and nudge it fully on-screen.
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, root);
        Rect canvasBounds = canvasRect.rect;

        Vector2 correction = Vector2.zero;

        // If the left side is outside, move right.
        if (bounds.min.x < canvasBounds.xMin)
            correction.x += canvasBounds.xMin - bounds.min.x;
        // If the right side is outside, move left.
        if (bounds.max.x > canvasBounds.xMax)
            correction.x += canvasBounds.xMax - bounds.max.x;

        // If the bottom is outside, move up.
        if (bounds.min.y < canvasBounds.yMin)
            correction.y += canvasBounds.yMin - bounds.min.y;
        // If the top is outside, move down.
        if (bounds.max.y > canvasBounds.yMax)
            correction.y += canvasBounds.yMax - bounds.max.y;

        root.anchoredPosition += correction;
    }

    public void Hide(object owner = null)
    {
        if (owner != null && currentOwner != null && !ReferenceEquals(owner, currentOwner))
            return;

        // Fade out; if we have no CanvasGroup, hide immediately.
        if (canvasGroup != null && fadeOutDuration > 0f)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha: 0f, fadeOutDuration));
        }
        else
        {
            HideImmediate();
        }
    }

    private void HideImmediate()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        currentOwner = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (root != null)
            root.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            HideImmediate();
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, duration);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, u);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;

        if (Mathf.Approximately(targetAlpha, 0f))
        {
            // Fully hidden at the end of a fade-out.
            currentOwner = null;
            if (root != null)
                root.gameObject.SetActive(false);
        }
    }
}
