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
    public float selectedScale = 1.08f;

    [Tooltip(
        "Graphics whose color will be tinted when selected (e.g., card frame, background). If empty, all child Graphics will be used."
    )]
    public Graphic[] tintTargets;

    [Tooltip("Tint color when not selected.")]
    public Color normalTint = Color.white;

    [Tooltip("Tint color when selected (acts like a subtle outline/emphasis).")]
    public Color selectedTint = new Color(0.9f, 0.95f, 1.1f, 1f);

    [Header("Prefabs")]
    [Tooltip("Prefab for creature card previews.")]
    public GameObject creatureCardPrefab;

    [Tooltip("Prefab for effect card previews.")]
    public GameObject effectCardPrefab;

    private ScriptableObject cardData;
    private System.Action<DraftCardOptionUI> onClicked;
    private Graphic rootRaycastGraphic;

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

        // If no explicit tint targets were assigned, default to all child Graphics.
        if (tintTargets == null || tintTargets.Length == 0)
        {
            tintTargets = GetComponentsInChildren<Graphic>(includeInactive: true);
        }

        // Initialize visuals to unselected.
        ApplySelectionVisuals(false);
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
            ApplySelectionVisuals(false);
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
        ApplySelectionVisuals(false);
    }

    public ScriptableObject GetCardData()
    {
        return cardData;
    }

    public void SetSelected(bool selected)
    {
        ApplySelectionVisuals(selected);
    }

    private void ApplySelectionVisuals(bool selected)
    {
        if (selectionScaleTarget != null)
        {
            float targetScale = selected ? selectedScale : normalScale;
            selectionScaleTarget.localScale = Vector3.one * targetScale;
        }

        if (tintTargets != null)
        {
            var color = selected ? selectedTint : normalTint;
            foreach (var g in tintTargets)
            {
                if (g != null)
                    g.color = color;
            }
        }
    }

    private void HandleClicked()
    {
        Debug.Log("DraftCardOptionUI: HandleClicked");
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
