using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI component for a single card option in the CardChoiceManager.
/// Handles display, selection state, and click events.
/// Similar to DraftCardOptionUI but designed for the more flexible choice system.
/// </summary>
public class CardChoiceOptionUI
    : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
{
    [Header("Card Display")]
    [Tooltip(
        "Parent transform where the card preview (CreatureCardUI / EffectCardUI) will be instantiated."
    )]
    public Transform previewRoot;

    [Tooltip("Prefab for creature card previews.")]
    public GameObject creatureCardPrefab;

    [Tooltip("Prefab for effect card previews.")]
    public GameObject effectCardPrefab;

    [Tooltip("Optional: GameObject to show when card is face-down.")]
    public GameObject faceDownOverlay;

    [Header("Selection Visuals")]
    [Tooltip("Transform to scale on selection. Defaults to this.transform if not set.")]
    public Transform selectionScaleTarget;

    [Tooltip("Scale when not selected.")]
    public float normalScale = 1.0f;

    [Tooltip("Scale when hovered (not selected).")]
    public float hoverScale = 1.1f;

    [Tooltip("Scale when selected.")]
    public float selectedScale = 1.15f;

    [Tooltip("Seconds to animate scale transitions.")]
    public float scaleAnimDuration = 0.1f;

    [Header("Selection Outline")]
    [Tooltip("Graphic to apply outline to. If null, auto-picks first Graphic under previewRoot.")]
    public Graphic outlineTarget;

    [Tooltip("Outline color when selected.")]
    public Color selectedOutlineColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Tooltip("Outline color when hovered but not selected.")]
    public Color hoverOutlineColor = new Color(1f, 1f, 1f, 0.5f);

    [Tooltip("Outline thickness.")]
    public Vector2 outlineDistance = new Vector2(4f, -4f);

    [Header("Selection Order Badge")]
    [Tooltip("Optional: Text to show selection order (1, 2, 3...) when orderMatters is true.")]
    public TMPro.TextMeshProUGUI orderBadgeText;

    [Tooltip("Parent of the order badge; hidden when not showing order.")]
    public GameObject orderBadgeRoot;

    [Header("Disabled State")]
    [Tooltip("CanvasGroup to dim when this option is disabled (e.g., max selections reached).")]
    public CanvasGroup dimGroup;

    [Tooltip("Alpha when disabled/dimmed.")]
    public float dimmedAlpha = 0.5f;

    // State
    private ScriptableObject cardData;
    private System.Action<CardChoiceOptionUI> onClicked;
    private bool isSelected;
    private bool isHovered;
    private bool isInteractable = true;
    private int selectionOrder = -1; // -1 = not selected
    private Graphic rootRaycastGraphic;
    private Coroutine scaleRoutine;
    private Outline outline;

    public ScriptableObject CardData => cardData;
    public bool IsSelected => isSelected;
    public int SelectionOrder => selectionOrder;

    private void Awake()
    {
        if (selectionScaleTarget == null)
            selectionScaleTarget = transform;

        // Ensure there is a root-level Graphic for raycasts.
        rootRaycastGraphic = GetComponent<Graphic>();
        if (rootRaycastGraphic == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            rootRaycastGraphic = img;
        }
        rootRaycastGraphic.raycastTarget = true;

        // Start in unselected, un-hovered state.
        ApplyVisualState(instant: true);

        if (orderBadgeRoot != null)
            orderBadgeRoot.SetActive(false);
    }

    /// <summary>
    /// Initialize this option with card data.
    /// </summary>
    public void SetCard(
        ScriptableObject data,
        System.Action<CardChoiceOptionUI> clickedCallback,
        bool showFaceDown = false
    )
    {
        cardData = data;
        onClicked = clickedCallback;
        isSelected = false;
        isHovered = false;
        selectionOrder = -1;

        // Clear existing preview
        if (previewRoot != null)
        {
            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                var child = previewRoot.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        // Handle face-down display
        if (faceDownOverlay != null)
            faceDownOverlay.SetActive(showFaceDown);

        if (data == null || previewRoot == null)
        {
            ApplyVisualState(instant: true);
            return;
        }

        // Don't create preview if showing face-down
        if (showFaceDown)
        {
            ApplyVisualState(instant: true);
            return;
        }

        GameObject previewObj = null;

        if (data is CreatureCard creatureData)
        {
            if (creatureCardPrefab == null)
            {
                Debug.LogError("CardChoiceOptionUI: Creature card prefab not assigned.");
                return;
            }
            previewObj = Instantiate(creatureCardPrefab, previewRoot);
            var ui = previewObj.GetComponent<CreatureCardUI>();
            ui?.Initialize(creatureData);
        }
        else if (data is EffectCard effectData)
        {
            if (effectCardPrefab == null)
            {
                Debug.LogError("CardChoiceOptionUI: Effect card prefab not assigned.");
                return;
            }
            previewObj = Instantiate(effectCardPrefab, previewRoot);
            var ui = previewObj.GetComponent<EffectCardUI>();
            if (ui != null)
            {
                ui.Initialize(effectData);
                ui.owner = SlotOwner.Player1;
            }
        }
        else
        {
            Debug.LogWarning(
                $"CardChoiceOptionUI: Unsupported card data type {data.GetType().Name}"
            );
        }

        // Disable interaction on the card preview so clicks hit this component
        if (previewObj != null)
        {
            var baseCard = previewObj.GetComponent<BaseCardUI>();
            if (baseCard != null)
                baseCard.enabled = false;

            var graphics = previewObj.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var g in graphics)
            {
                if (g != null)
                    g.raycastTarget = false;
            }
        }

        ApplyVisualState(instant: true);

        if (orderBadgeRoot != null)
            orderBadgeRoot.SetActive(false);
    }

    /// <summary>
    /// Set selection state.
    /// </summary>
    public void SetSelected(bool selected, int order = -1)
    {
        isSelected = selected;
        selectionOrder = selected ? order : -1;
        ApplyVisualState(instant: false);
        UpdateOrderBadge();
    }

    /// <summary>
    /// Set whether this option can be clicked.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        ApplyDimState();
    }

    /// <summary>
    /// Show or hide the selection order badge.
    /// </summary>
    public void ShowOrderBadge(bool show)
    {
        if (orderBadgeRoot != null)
            orderBadgeRoot.SetActive(show && isSelected && selectionOrder >= 0);
    }

    private void UpdateOrderBadge()
    {
        if (orderBadgeRoot == null)
            return;

        bool shouldShow = isSelected && selectionOrder >= 0;
        orderBadgeRoot.SetActive(shouldShow);

        if (shouldShow && orderBadgeText != null)
        {
            // Display 1-indexed order
            orderBadgeText.text = (selectionOrder + 1).ToString();
        }
    }

    private void ApplyVisualState(bool instant)
    {
        ApplyOutline();
        ApplyScale(instant);
        ApplyDimState();
    }

    private void ApplyScale(bool instant)
    {
        if (selectionScaleTarget == null)
            return;

        float targetScale;
        if (isSelected)
            targetScale = selectedScale;
        else if (isHovered && isInteractable)
            targetScale = hoverScale;
        else
            targetScale = normalScale;

        if (instant || scaleAnimDuration <= 0f)
        {
            selectionScaleTarget.localScale = Vector3.one * targetScale;
        }
        else
        {
            if (scaleRoutine != null)
                StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(AnimateScale(targetScale));
        }
    }

    private void ApplyOutline()
    {
        if (outline == null)
        {
            Graphic g = outlineTarget;
            if (g == null && previewRoot != null)
            {
                var graphics = previewRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
                g = graphics != null && graphics.Length > 0 ? graphics[0] : null;
            }

            if (g != null)
            {
                outline = g.GetComponent<Outline>();
                if (outline == null)
                    outline = g.gameObject.AddComponent<Outline>();

                outline.effectDistance = outlineDistance;
                outline.useGraphicAlpha = true;
            }
        }

        if (outline != null)
        {
            if (isSelected)
            {
                outline.enabled = true;
                outline.effectColor = selectedOutlineColor;
            }
            else if (isHovered && isInteractable)
            {
                outline.enabled = true;
                outline.effectColor = hoverOutlineColor;
            }
            else
            {
                outline.enabled = false;
            }
        }
    }

    private void ApplyDimState()
    {
        if (dimGroup != null)
        {
            dimGroup.alpha = isInteractable ? 1f : dimmedAlpha;
        }
    }

    private IEnumerator AnimateScale(float targetScale)
    {
        if (selectionScaleTarget == null)
            yield break;

        float start = selectionScaleTarget.localScale.x;
        float dur = Mathf.Max(0.01f, scaleAnimDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            u = u * u * (3f - 2f * u); // ease in-out
            float s = Mathf.Lerp(start, u, t / dur);
            s = Mathf.Lerp(start, targetScale, u);
            selectionScaleTarget.localScale = Vector3.one * s;
            yield return null;
        }

        selectionScaleTarget.localScale = Vector3.one * targetScale;
        scaleRoutine = null;
    }

    // ----- Pointer Events -----

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable)
            return;

        onClicked?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        ApplyVisualState(instant: false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        ApplyVisualState(instant: false);
    }
}
