using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class TooltipTriggerBase
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler
{
    [Header("Hover")]
    [SerializeField]
    protected float hoverDelaySeconds = 0.4f;

    private Coroutine hoverRoutine;
    private bool pointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (hoverRoutine != null)
            StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(ShowAfterDelay(eventData.position));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (hoverRoutine != null)
        {
            StopCoroutine(hoverRoutine);
            hoverRoutine = null;
        }

        OnHideTooltip();
    }

    private void OnDisable()
    {
        pointerInside = false;
        if (hoverRoutine != null)
        {
            StopCoroutine(hoverRoutine);
            hoverRoutine = null;
        }

        OnHideTooltip();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.UpdatePosition(eventData.position);
    }

    private IEnumerator ShowAfterDelay(Vector2 initialScreenPos)
    {
        float delay = Mathf.Max(0f, hoverDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        hoverRoutine = null;

        if (pointerInside)
            OnShowTooltip(initialScreenPos);
    }

    protected abstract void OnShowTooltip(Vector2 screenPosition);
    protected abstract void OnHideTooltip();
}
