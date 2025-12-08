using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class CreatureInteractionHandler
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
{
    private Creature creature;

    [SerializeField]
    private readonly float hoverDelaySeconds = 0.5f;
    private Coroutine hoverRoutine;
    private bool pointerInside;

    void Awake()
    {
        creature = GetComponent<Creature>();
    }

    private void OnDisable()
    {
        // If this creature is going away (death, scene unload, etc.), make sure
        // any hover preview tied to it is cleared so the HUD doesn't show a
        // ghost card.
        pointerInside = false;
        if (hoverRoutine != null)
        {
            StopCoroutine(hoverRoutine);
            hoverRoutine = null;
        }

        if (creature != null && CardPreviewManager.Instance != null)
        {
            CardPreviewManager.Instance.HideHoverCreature(creature);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelHoverAndHide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (creature == null)
            return;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleManualEffectCreatureClicked(creature);
        }
    }

    private void StartHover()
    {
        pointerInside = true;
        if (hoverRoutine != null)
            StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(ShowAfterDelay());
    }

    private void CancelHoverAndHide()
    {
        pointerInside = false;
        if (hoverRoutine != null)
        {
            StopCoroutine(hoverRoutine);
            hoverRoutine = null;
        }
        if (CardPreviewManager.Instance != null)
        {
            CardPreviewManager.Instance.HideHoverCreature(creature);
        }
    }

    private System.Collections.IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, hoverDelaySeconds));
        hoverRoutine = null;
        if (pointerInside && CardPreviewManager.Instance != null)
        {
            CardPreviewManager.Instance.ShowHoverCreature(creature);
        }
    }
}
