using UnityEngine;
using System.Collections;

public class HoverPreviewManager : MonoBehaviour
{
	public static HoverPreviewManager Instance { get; private set; }

	[Header("Preview UI")]
	[Tooltip("Assign a CreatureCardUI in the scene anchored to the left side.")]
	public CreatureCardUI previewUI;

	private Creature currentCreature;
	[Header("Animation")]
	[SerializeField] private readonly float fadeDuration = 0.2f;
	[SerializeField] private readonly float targetScale = 1.25f;
	private CanvasGroup previewGroup;
	private RectTransform previewRect;
	private Coroutine animRoutine;

	void Awake()
	{
		Instance = this;
		if (previewUI != null)
		{
			previewRect = previewUI.transform as RectTransform;
			previewGroup = previewUI.GetComponent<CanvasGroup>();
			if (previewGroup == null) previewGroup = previewUI.gameObject.AddComponent<CanvasGroup>();
			previewGroup.alpha = 0f;
			if (previewRect != null) previewRect.localScale = Vector3.one * targetScale;
			previewUI.gameObject.SetActive(false);
		}
	}

	public void Show(Creature creature)
	{
		if (creature == null || creature.data == null || previewUI == null) return;
		currentCreature = creature;
		previewUI.Initialize(creature.data);
		if (animRoutine != null) StopCoroutine(animRoutine);
		animRoutine = StartCoroutine(FadeTo(visible: true));
	}

	public void Hide(Creature creature)
	{
		if (previewUI == null) return;
		if (animRoutine != null) StopCoroutine(animRoutine);
		animRoutine = StartCoroutine(FadeTo(visible: false));
	}

	public void HideAll()
	{
		currentCreature = null;
		if (previewUI == null) return;
		if (animRoutine != null) StopCoroutine(animRoutine);
		animRoutine = StartCoroutine(FadeTo(visible: false));
	}

	private IEnumerator FadeTo(bool visible)
	{
		if (previewUI == null) yield break;
		if (previewRect == null) previewRect = previewUI.transform as RectTransform;
		if (previewGroup == null)
		{
			previewGroup = previewUI.GetComponent<CanvasGroup>();
			if (previewGroup == null) previewGroup = previewUI.gameObject.AddComponent<CanvasGroup>();
		}
		if (previewRect != null) previewRect.localScale = Vector3.one * targetScale;

		if (visible)
		{
			if (!previewUI.gameObject.activeSelf) previewUI.gameObject.SetActive(true);
		}
		float start = Mathf.Clamp01(previewGroup.alpha);
		float end = visible ? 1f : 0f;
		if (Mathf.Approximately(start, end))
		{
			previewGroup.alpha = end;
			if (!visible) previewUI.gameObject.SetActive(false);
			yield break;
		}
		float t = 0f;
		float dur = Mathf.Max(0.01f, fadeDuration);
		while (t < dur)
		{
			t += Time.deltaTime;
			float u = Mathf.Clamp01(t / dur);
			previewGroup.alpha = Mathf.Lerp(start, end, u);
			yield return null;
		}
		previewGroup.alpha = end;
		if (!visible) previewUI.gameObject.SetActive(false);
	}
}


