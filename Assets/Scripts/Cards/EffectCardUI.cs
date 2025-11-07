using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class EffectCardUI : BaseCardUI
{
    public EffectCard effectData;
    public SlotOwner owner = SlotOwner.Player1;
    public float targetRadiusPx = 100f;

    private readonly HashSet<TargetHighlightController> highlighted = new();

    [Header("UI References")]
    public Image artworkImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;

    public void Initialize(EffectCard data)
    {
        effectData = data;
        if (nameText != null) nameText.text = effectData != null ? effectData.effectName : "";
        if (artworkImage != null) artworkImage.sprite = effectData != null ? effectData.icon : null;
        if (descriptionText != null) descriptionText.text = effectData != null ? (effectData.description ?? "").Trim() : "";
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        // Use base smoothing pipeline
        RectTransform canvasRT = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, canvas.worldCamera, out Vector2 pointerLocal);
        dragTargetAnchoredPosition = pointerLocal + pointerGrabOffset;

        UpdateHighlights(eventData.position);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        isDragging = false;

        // Determine target or global drop
        var target = FindNearestValidTarget(eventData.position, out float distPx);
        bool played = false;

        if (effectData != null && effectData.isGlobal)
        {
            var dz = GlobalEffectDropZone.Instance;
            if (dz != null && dz.IsPointerInside(eventData.position)
                && EffectsManager.Instance != null
                && EffectsManager.Instance.CanPayCosts(effectData))
            {
                EffectsManager.Instance?.PlayOnTargets(effectData, Enumerable.Empty<Creature>(), owner);
                played = true;
            }
        }
        else if (target != null && distPx <= targetRadiusPx
                 && EffectsManager.Instance != null
                 && EffectsManager.Instance.CanPayCosts(effectData))
        {
            EffectsManager.Instance?.PlayOnTargets(effectData, new[] { target }, owner);
            played = true;
        }

        // Clear highlights
        foreach (var h in highlighted) if (h != null) h.SetHighlighted(false);
        highlighted.Clear();

        // If played, remove the card from hand; otherwise return it
        if (played)
        {
            handLayout?.NotifyDragEnd(this, false);
            Destroy(gameObject);
            return;
        }
        ReturnToHand();
    }

    private void UpdateHighlights(Vector2 pointerScreen)
    {
        // Turn off all first
        foreach (var h in highlighted) if (h != null) h.SetHighlighted(false);
        highlighted.Clear();

        if (effectData == null) return;
        if (effectData.isGlobal)
        {
            var dz = GlobalEffectDropZone.Instance;
            bool inside = dz != null && dz.IsPointerInside(pointerScreen);
            if (inside)
            {
                foreach (var c in FindObjectsByType<Creature>(FindObjectsSortMode.None))
                {
                    if (c == null) continue;
                    var th = c.GetComponent<TargetHighlightController>();
                    if (th == null) continue;
                    th.SetHighlighted(true);
                    highlighted.Add(th);
                }
            }
            return;
        }

        // Creature targets
        var nearest = FindNearestValidTarget(pointerScreen, out float distPx);
        if (nearest != null && distPx <= targetRadiusPx)
        {
            var th = nearest.GetComponent<TargetHighlightController>();
            if (th != null) { th.SetHighlighted(true); highlighted.Add(th); }
        }
    }

    private Creature FindNearestValidTarget(Vector2 pointerScreen, out float bestDist)
    {
        bestDist = float.MaxValue;
        Creature best = null;
        var cam = (canvas != null && canvas.worldCamera != null) ? canvas.worldCamera : Camera.main;
        foreach (var c in FindObjectsByType<Creature>(FindObjectsSortMode.None))
        {
            if (c == null) continue;
            if (!EffectsManager.Instance.IsValidTarget(effectData, c, owner)) continue;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, c.transform.position);
            float d = Vector2.Distance(screen, pointerScreen);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    // Description is authored on EffectCard (effectData.description)
}


