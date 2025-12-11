using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance;

    [Header("Floating Text")]
    public GameObject floatingTextPrefab; // prefab with TMP Text
    public float floatUpDistance = 1.2f;
    public float floatDuration = 2.25f; // fade-out duration

    [Tooltip("Time to hold at full alpha before fading starts")]
    public float alphaHold = 1.25f;

    [Tooltip("Vertical spacing between stacked messages at same position")]
    public float stackOffset = 0.5f;

    [Header("Log UI")]
    public TextMeshProUGUI logText;
    public GameObject logPanel;
    public int maxLines = 20;
    public bool logPanelVisible = false;

    [Header("Global Alert")]
    [Tooltip("Centered text used for big global alerts (weather, era changes, errors, etc.)")]
    public TextMeshProUGUI globalAlertText;

    [Tooltip("Seconds to hold the global alert at full alpha before fading")]
    public float globalAlertHold = 2f;

    [Tooltip("Seconds to fade the global alert out")]
    public float globalAlertFade = 0.6f;

    private readonly System.Text.StringBuilder sb = new(1024);

    // Per-position active text tracking (for stacking)
    private readonly Dictionary<Vector3, List<GameObject>> activeTexts = new();

    private Coroutine globalAlertRoutine;
    private readonly Queue<(string message, Color color)> globalAlertQueue = new();

    void Awake()
    {
        Instance = this;
        // Set initial visibility
        logPanel?.SetActive(logPanelVisible);
        // Ensure global alert starts hidden
        if (globalAlertText != null)
        {
            var cg =
                globalAlertText.GetComponent<CanvasGroup>()
                ?? globalAlertText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }
    }

    public void Log(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;
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

    // --- Global Screen-Center Alerts ---

    public void ShowGlobalAlert(string message, Color color)
    {
        if (globalAlertText == null || string.IsNullOrEmpty(message))
            return;

        // Enqueue the alert; if nothing is currently processing, start the processor
        globalAlertQueue.Enqueue((message, color));
        if (globalAlertRoutine == null)
        {
            globalAlertRoutine = StartCoroutine(ProcessGlobalAlerts());
        }
    }

    private IEnumerator ProcessGlobalAlerts()
    {
        while (globalAlertQueue.Count > 0)
        {
            var (message, color) = globalAlertQueue.Dequeue();
            yield return StartCoroutine(GlobalAlertRoutine(message, color));
        }
        globalAlertRoutine = null;
    }

    private IEnumerator GlobalAlertRoutine(string message, Color color)
    {
        var cg =
            globalAlertText.GetComponent<CanvasGroup>()
            ?? globalAlertText.gameObject.AddComponent<CanvasGroup>();

        globalAlertText.text = message;
        Color c = color;
        c.a = 1f;
        globalAlertText.color = c;
        cg.alpha = 1f;

        float hold = Mathf.Max(0.01f, globalAlertHold);
        float fade = Mathf.Max(0.01f, globalAlertFade);

        yield return new WaitForSeconds(hold);

        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fade);
            cg.alpha = 1f - u;
            yield return null;
        }

        cg.alpha = 0f;
        globalAlertRoutine = null;
    }

    public static string TagOwner(SlotOwner owner)
    {
        return owner == SlotOwner.Player1 ? "[P1]" : "[P2]";
    }

    public void ShowFloatingText(string text, Vector3 worldPos, Color color)
    {
        if (floatingTextPrefab == null || string.IsNullOrEmpty(text))
            return;

        // Round position to group nearby texts
        Vector3 key = new Vector3(
            Mathf.Round(worldPos.x * 10f) / 10f,
            Mathf.Round(worldPos.y * 10f) / 10f,
            Mathf.Round(worldPos.z * 10f) / 10f
        );

        // Prune nulls from the list for this key
        if (!activeTexts.ContainsKey(key))
            activeTexts[key] = new List<GameObject>();

        activeTexts[key].RemoveAll(x => x == null);

        // Push existing texts up
        foreach (var obj in activeTexts[key])
        {
            if (obj != null)
            {
                obj.transform.position += Vector3.up * stackOffset;
            }
        }

        // Instantiate new text
        var go = Instantiate(floatingTextPrefab, worldPos, Quaternion.identity);
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
        }

        // Track it
        activeTexts[key].Add(go);

        // Animate
        StartCoroutine(FloatAndFade(go));
    }

    IEnumerator FloatAndFade(GameObject go)
    {
        if (go == null)
            yield break;
        _ = go.transform.position; // Note: this is just the spawn point, but we will read current pos in loop
        // We want to drift UP relative to wherever we are, but since other logic might move us (the stack push),
        // we should perhaps just add a continuous drift velocity or simply fade out.
        // The stack push handles the "make room" part.
        // Let's just handle the Fade and maybe a slow drift, but be careful not to fight the stack push.
        // Actually, if we just let the stack push handle the big moves, we can add a small drift here or just fade.

        // To keep it simple and consistent with WoW style:
        // The text appears, maybe pops a bit, then stays or drifts slowly, then fades.
        // The "stack push" moves it up significantly.

        var canvasGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        float totalDuration = alphaHold + floatDuration;
        float t = 0f;

        // We'll add a small constant drift just so it's not static if no other events happen
        float driftSpeed = 0.2f;

        while (t < totalDuration)
        {
            if (go == null)
                yield break;

            t += Time.deltaTime;

            // Small continuous drift
            go.transform.position += Vector3.up * (driftSpeed * Time.deltaTime);

            // Alpha
            if (t > alphaHold)
            {
                float fadeProgress = (t - alphaHold) / floatDuration;
                canvasGroup.alpha = 1f - Mathf.Clamp01(fadeProgress);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }

            yield return null;
        }

        if (go != null)
            Destroy(go);
    }
}
