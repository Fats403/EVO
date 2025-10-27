using System.Collections;
using System.Linq;
using UnityEngine;
using TMPro;

public class FeedbackManager : MonoBehaviour
{
	public static FeedbackManager Instance;

	[Header("Floating Text")]
	public GameObject floatingTextPrefab; // prefab with TMP Text
	public float floatUpDistance = 1f;
	public float floatDuration = 0.7f;

	[Header("Log UI")]
	public TextMeshProUGUI logText;
	public int maxLines = 20;

	private System.Text.StringBuilder sb = new System.Text.StringBuilder();

	void Awake()
	{
		Instance = this;
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

	public static string TagOwner(SlotOwner owner)
	{
		return owner == SlotOwner.Player1 ? "[P1]" : "[P2]";
	}

	public void ShowFloatingText(string text, Vector3 worldPos, Color color)
	{
		if (floatingTextPrefab == null) return;
		var go = Instantiate(floatingTextPrefab, worldPos, Quaternion.identity);
		var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
		if (tmp != null)
		{
			tmp.text = text;
			tmp.color = color;
		}
		StartCoroutine(FloatAndFade(go));
	}

	IEnumerator FloatAndFade(GameObject go)
	{
		var start = go.transform.position;
		var end = start + Vector3.up * floatUpDistance;
		var t = 0f;
		var canvasGroup = go.GetComponent<CanvasGroup>();
		if (canvasGroup == null) canvasGroup = go.AddComponent<CanvasGroup>();
		while (t < floatDuration)
		{
			t += Time.deltaTime;
			float u = Mathf.Clamp01(t / floatDuration);
			go.transform.position = Vector3.Lerp(start, end, u);
			canvasGroup.alpha = 1f - u;
			yield return null;
		}
		Destroy(go);
	}

}



