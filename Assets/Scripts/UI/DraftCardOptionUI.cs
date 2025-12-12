using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simple wrapper for a single draft option slot.
/// It hosts either a CreatureCardUI or EffectCardUI preview and
/// relays click events back to the DraftManager.
/// </summary>
public class DraftCardOptionUI : MonoBehaviour, IPointerClickHandler
{
    [Tooltip(
        "Parent transform where the card preview (CreatureCardUI / EffectCardUI) will be instantiated."
    )]
    public Transform previewRoot;

    [Header("Selection Visuals")]
    [Tooltip(
        "Transform to scale when this option is selected. Defaults to this.transform if not set."
    )]
    public Transform selectionScaleTarget;

    [Tooltip("Scale applied when not selected.")]
    public float normalScale = 1.0f;

    [Tooltip("Scale applied when selected.")]
    public float selectedScale = 1.5f;

    [Tooltip("Seconds to animate between normal/selected scale.")]
    public float scaleAnimDuration = 0.12f;

    [Header("Outline (Selected)")]
    [Tooltip(
        "If set, this Graphic gets an Outline when selected. If null, we auto-pick the first Graphic under previewRoot."
    )]
    public Graphic outlineTarget;

    [Tooltip("Outline color when selected.")]
    public Color outlineColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Tooltip("Outline thickness (UI Outline uses pixel offset).")]
    public Vector2 outlineDistance = new Vector2(6f, -6f);

    [Header("Prefabs")]
    [Tooltip("Prefab for creature card previews.")]
    public GameObject creatureCardPrefab;

    [Tooltip("Prefab for effect card previews.")]
    public GameObject effectCardPrefab;

    private ScriptableObject cardData;
    private System.Action<DraftCardOptionUI> onClicked;
    private Graphic rootRaycastGraphic;
    private Coroutine scaleRoutine;
    private Outline outline;

    private void Awake()
    {
        if (selectionScaleTarget == null)
            selectionScaleTarget = transform;

        // Ensure there is a root-level Graphic so this object can receive raycasts
        // even if child previews change. We keep it fully transparent.
        rootRaycastGraphic = GetComponent<Graphic>();
        if (rootRaycastGraphic == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            rootRaycastGraphic = img;
        }
        rootRaycastGraphic.raycastTarget = true;

        // Initialize visuals to unselected.
        ApplySelectionVisuals(false, instant: true);
    }

    public void SetCard(ScriptableObject data, System.Action<DraftCardOptionUI> clickedCallback)
    {
        cardData = data;
        onClicked = clickedCallback;

        // Clear any existing preview children
        if (previewRoot != null)
        {
            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                var child = previewRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        if (data == null || previewRoot == null)
        {
            // Also reset visuals to unselected when slot is cleared.
            ApplySelectionVisuals(false, instant: true);
            return;
        }

        GameObject previewObj = null;

        if (data is CreatureCard creatureData)
        {
            if (creatureCardPrefab == null)
            {
                Debug.LogError("DraftCardOptionUI: Creature card prefab not assigned.");
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
                Debug.LogError("DraftCardOptionUI: Effect card prefab not assigned.");
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
                $"DraftCardOptionUI: Unsupported card data type {data.GetType().Name}"
            );
        }

        // Draft previews should NOT handle drag / hover behaviour or steal
        // raycasts from the root; the option itself owns interaction. Disable
        // BaseCardUI and child Graphic raycasts so clicks hit the root.
        if (previewObj != null)
        {
            // Disable BaseCardUI if present so it won't respond to drag/hover.
            var baseCard = previewObj.GetComponent<BaseCardUI>();
            if (baseCard != null)
            {
                baseCard.enabled = false;
            }

            // Turn off raycast targets on all graphics under the preview so
            // pointer events reach the root DraftCardOptionUI instead.
            var graphics = previewObj.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var g in graphics)
            {
                if (g != null)
                    g.raycastTarget = false;
            }
        }

        // Reset selection visuals when a new card is assigned.
        ApplySelectionVisuals(false, instant: true);
    }

    public ScriptableObject GetCardData()
    {
        return cardData;
    }

    public void SetSelected(bool selected)
    {
        ApplySelectionVisuals(selected, instant: false);
    }

    private void ApplySelectionVisuals(bool selected, bool instant)
    {
        ApplyOutline(selected);

        if (selectionScaleTarget != null)
        {
            float targetScale = selected ? selectedScale : normalScale;
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
    }

    private void ApplyOutline(bool selected)
    {
        if (outline == null)
        {
            // Pick a target graphic for the outline.
            Graphic g = outlineTarget;
            if (g == null && previewRoot != null)
            {
                // Prefer a visible graphic under the preview rather than the invisible raycast Image.
                var graphics = previewRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
                g = graphics != null && graphics.Length > 0 ? graphics[0] : null;
            }

            if (g != null)
            {
                outline = g.GetComponent<Outline>();
                if (outline == null)
                    outline = g.gameObject.AddComponent<Outline>();

                outline.effectColor = outlineColor;
                outline.effectDistance = outlineDistance;
                outline.useGraphicAlpha = true;
            }
        }

        if (outline != null)
            outline.enabled = selected;
    }

    private IEnumerator AnimateScale(float targetScale)
    {
        if (selectionScaleTarget == null)
            yield break;

        float start = selectionScaleTarget.localScale.x;
        float end = targetScale;
        float dur = Mathf.Max(0.01f, scaleAnimDuration);

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            // Smooth ease-in-out.
            u = u * u * (3f - 2f * u);
            float s = Mathf.Lerp(start, end, u);
            selectionScaleTarget.localScale = Vector3.one * s;
            yield return null;
        }
        selectionScaleTarget.localScale = Vector3.one * end;
        scaleRoutine = null;
    }

    private void HandleClicked()
    {
        onClicked?.Invoke(this);
    }

    /// <summary>
    /// Handle pointer clicks directly on this option. As long as the root
    /// has a raycast-target Graphic (ensured in Awake), this will fire.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClicked();
    }
}
