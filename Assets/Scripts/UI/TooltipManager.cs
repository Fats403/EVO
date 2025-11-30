using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct TooltipData
{
    public string title;
    public string body;
    public Sprite icon;
}

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Wiring")]
    public Canvas canvas;
    public RectTransform root;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Image iconImage;
    public GameObject iconContainer;

    [Header("Behavior")]
    [Tooltip("Offset from the mouse in screen pixels (positive X = right, positive Y = up).")]
    public Vector2 screenOffset = new Vector2(16f, 100f);

    [Tooltip("Sprite used when no specific icon is supplied.")]
    public Sprite defaultIcon;

    private object currentOwner;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

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

        Sprite iconToUse = data.icon != null ? data.icon : defaultIcon;

        if (iconContainer != null)
            iconContainer.SetActive(iconToUse != null);

        if (iconImage != null)
            iconImage.sprite = iconToUse;

        UpdatePosition(screenPosition);
    }

    public void UpdatePosition(Vector2 screenPosition)
    {
        if (canvas == null || root == null)
            return;

        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 localPoint;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition + screenOffset,
            cam,
            out localPoint
        );

        root.anchoredPosition = localPoint;
    }

    public void Hide(object owner = null)
    {
        if (owner != null && currentOwner != null && !ReferenceEquals(owner, currentOwner))
            return;

        HideImmediate();
    }

    private void HideImmediate()
    {
        currentOwner = null;
        if (root != null)
            root.gameObject.SetActive(false);
    }
}
