using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class FeedbackManager : MonoBehaviour
{
	public static FeedbackManager Instance;

	[Header("Floating Text")]
	public GameObject floatingTextPrefab; // prefab with TMP Text
    public float floatUpDistance = 1.2f;
	public float floatDuration = 1.75f; // fade-out duration
	[Tooltip("Time to hold at full alpha before fading starts")]
	public float alphaHold = 0.75f;
	[Tooltip("Vertical spacing between stacked messages at same position")]
	public float stackOffset = 0.3f;

	[Header("Log UI")]
	public TextMeshProUGUI logText;
	public GameObject logPanel;
	public int maxLines = 20;
	public bool logPanelVisible = false;

	private readonly System.Text.StringBuilder sb = new(1024);
	
	// Per-position queue system
	private readonly Dictionary<Vector3, Queue<FeedbackRequest>> positionQueues = new();
	private readonly Dictionary<Vector3, Coroutine> activeAnimations = new();
	private readonly Dictionary<Vector3, int> activeStackCounts = new(); // track stacking offset

	private class FeedbackRequest
	{
		public string text;
		public Vector3 worldPos;
		public Color color;
	}

	void Awake()
	{
		Instance = this;
        // Set initial visibility
        logPanel?.SetActive(logPanelVisible);
	}

	public void Log(string message)
	{
		if (string.IsNullOrEmpty(message)) return;
		// Append and clamp lines
		sb.AppendLine(message);
		var str = sb.ToString();
		if (logText != null)
		{
			var lines = str.Split('\n');
			if (lines.Length > maxLines)
			{
				str = string.Join("\n", lines.Skip(Mathf.Max(0, lines.Length - maxLines)));
			}
			logText.text = str;
		}
		Debug.Log(message);
	}

	public void ToggleLogPanel()
	{
		logPanelVisible = !logPanelVisible;
		if (logPanel != null)
		{
			logPanel.SetActive(logPanelVisible);
		}
	}

	public static string TagOwner(SlotOwner owner)
	{
		return owner == SlotOwner.Player1 ? "[P1]" : "[P2]";
	}

	public void ShowFloatingText(string text, Vector3 worldPos, Color color)
	{
		if (floatingTextPrefab == null || string.IsNullOrEmpty(text)) return;
		
		// Round position to avoid near-duplicates creating separate queues
		Vector3 key = new Vector3(
			Mathf.Round(worldPos.x * 10f) / 10f,
			Mathf.Round(worldPos.y * 10f) / 10f,
			Mathf.Round(worldPos.z * 10f) / 10f
		);

		var req = new FeedbackRequest { text = text, worldPos = worldPos, color = color };
		
		if (!positionQueues.ContainsKey(key))
			positionQueues[key] = new Queue<FeedbackRequest>();
		
		positionQueues[key].Enqueue(req);
		
		// Start processing if not already running for this position
		if (!activeAnimations.ContainsKey(key))
		{
			activeStackCounts[key] = 0;
			activeAnimations[key] = StartCoroutine(ProcessQueue(key));
		}
	}

	IEnumerator ProcessQueue(Vector3 key)
	{
		while (positionQueues.ContainsKey(key) && positionQueues[key].Count > 0)
		{
			var req = positionQueues[key].Dequeue();
			int stackIndex = activeStackCounts[key];
			activeStackCounts[key]++;
			
			yield return StartCoroutine(FloatAndFade(req, stackIndex));
			
			activeStackCounts[key]--;
		}
		
		// Cleanup when queue is empty
		activeAnimations.Remove(key);
		positionQueues.Remove(key);
		activeStackCounts.Remove(key);
	}

	IEnumerator FloatAndFade(FeedbackRequest req, int stackIndex)
	{
		if (floatingTextPrefab == null) yield break;
		
		// Apply vertical stack offset
		Vector3 spawnPos = req.worldPos + Vector3.up * (stackIndex * stackOffset);
		var go = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
		var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
		if (tmp != null)
		{
			tmp.text = req.text;
			tmp.color = req.color;
		}

		var start = go.transform.position;
		var end = start + Vector3.up * floatUpDistance;
		var t = 0f;
		var canvasGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
		float total = Mathf.Max(0.01f, alphaHold + Mathf.Max(0.01f, floatDuration));
		
		while (t < total)
		{
			t += Time.deltaTime;
			float uMove = Mathf.Clamp01(t / total);
			go.transform.position = Vector3.Lerp(start, end, uMove);
			if (t <= alphaHold)
			{
				canvasGroup.alpha = 1f;
			}
			else
			{
				float uFade = Mathf.Clamp01((t - alphaHold) / Mathf.Max(0.01f, floatDuration));
				canvasGroup.alpha = 1f - uFade;
			}
			yield return null;
		}
		Destroy(go);
	}

}



