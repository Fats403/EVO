using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class CreatureHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	private Creature creature;
	[SerializeField] private readonly float hoverDelaySeconds = 0.5f;
	private Coroutine hoverRoutine;
	private bool pointerInside;

	void Awake()
	{
		creature = GetComponent<Creature>();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		StartHover();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		CancelHoverAndHide();
	}

	private void StartHover()
	{
		pointerInside = true;
		if (hoverRoutine != null) StopCoroutine(hoverRoutine);
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
		if (HoverPreviewManager.Instance != null)
		{
			HoverPreviewManager.Instance.Hide(creature);
		}
	}

	private System.Collections.IEnumerator ShowAfterDelay()
	{
		yield return new WaitForSeconds(Mathf.Max(0f, hoverDelaySeconds));
		hoverRoutine = null;
		if (pointerInside && HoverPreviewManager.Instance != null)
		{
			HoverPreviewManager.Instance.Show(creature);
		}
	}
}


