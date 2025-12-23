using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attachable to any TMP_Text to enable hover tooltips for <link> regions.
///
/// Usage:
/// - Add this component to a TextMeshProUGUI object (e.g., card description text).
/// - In the text, wrap keywords with TMP link markup:
///     "Gain <link=\"frenzy\"><color=#FFD35B><b>Frenzy</b></color></link> this round."
/// - Optionally, configure it to resolve status keywords via StatusDefinitionLibrary by
///   using link IDs that match StatusTag names, e.g. <link="Infected">Infected</link>.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class KeywordTooltipLinkHandler : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    private TMP_Text tmpText;
    private Canvas canvas;
    private Camera uiCamera;

    [Header("Keyword Sources")]
    [Tooltip(
        "Optional explicit status definition library; used when link IDs match StatusTag names."
    )]
    public StatusDefinitionLibrary statusDefinitionLibrary;

    // Track currently hovered link index so we only refresh when it changes.
    private int currentLinkIndex = -1;

    private void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
        canvas = GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }
        else
        {
            uiCamera = null;
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (tmpText == null || TooltipManager.Instance == null)
            return;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(
            tmpText,
            eventData.position,
            uiCamera
        );

        // No link under pointer: if we were previously hovering one, hide its tooltip.
        if (linkIndex == -1)
        {
            if (currentLinkIndex != -1)
            {
                currentLinkIndex = -1;
                TooltipManager.Instance.Hide(this);
            }
            return;
        }

        // Still hovering the same link: just update tooltip position to follow the mouse.
        if (linkIndex == currentLinkIndex)
        {
            TooltipManager.Instance.UpdatePosition(eventData.position);
            return;
        }

        // Hover changed: hide previous tooltip (if any), then show the new one.
        if (currentLinkIndex != -1)
        {
            TooltipManager.Instance.Hide(this);
        }

        currentLinkIndex = linkIndex;
        ShowTooltipForLink(linkIndex, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        currentLinkIndex = -1;
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.Hide(this);
        }
    }

    private void OnDisable()
    {
        currentLinkIndex = -1;
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.Hide(this);
        }
    }

    private void ShowTooltipForLink(int linkIndex, Vector2 screenPosition)
    {
        if (TooltipManager.Instance == null)
            return;

        TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
        string linkId = linkInfo.GetLinkID();

        // 1) Try generic keyword library first (if present).
        if (
            KeywordTooltipLibrary.Instance != null
            && KeywordTooltipLibrary.Instance.TryGet(linkId, out var def)
        )
        {
            TooltipData dataFromKeyword = new TooltipData
            {
                title = string.IsNullOrEmpty(def.title) ? def.id : def.title,
                body = def.body ?? string.Empty,
                icon = def.icon,
                hasStacks = def.useStacks && def.defaultStacks > 1,
                stacks = def.useStacks ? Mathf.Max(1, def.defaultStacks) : 0,
            };

            TooltipManager.Instance.Show(dataFromKeyword, screenPosition, this);
            return;
        }

        // 2) Fallback: treat the link ID as a StatusTag and use StatusDefinitionLibrary.
        if (
            statusDefinitionLibrary != null
            && Enum.TryParse<StatusTag>(linkId, ignoreCase: true, out var tag)
        )
        {
            string title = statusDefinitionLibrary.GetDisplayName(tag);
            string body = statusDefinitionLibrary.GetDescription(tag);
            Sprite icon = statusDefinitionLibrary.GetIcon(tag);

            TooltipData dataFromStatus = new TooltipData
            {
                title = title,
                body = body,
                icon = icon,
                // Card text status keywords generally don't have a dynamic stack count;
                // stacks are handled by StatusIconController when relevant.
                hasStacks = false,
                stacks = 0,
            };

            TooltipManager.Instance.Show(dataFromStatus, screenPosition, this);
        }
    }
}
