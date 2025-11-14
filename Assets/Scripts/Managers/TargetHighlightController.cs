using UnityEngine;
using UnityEngine.UI;

public class TargetHighlightController : MonoBehaviour
{
    [Header("Pulse Settings")]
    [Tooltip("Scale pulse speed in Hz-like units (higher = faster).")]
    public float pulseSpeed = 3.0f;

    [Tooltip("Scale pulse amplitude (e.g. 0.08 = ±8%).")]
    public float scaleAmplitude = 0.08f;

    [Header("Tint Settings")]
    [Tooltip("Tint color while highlighted.")]
    public Color highlightColor = new Color(1f, 0.92f, 0.2f, 1f);

    private Vector3 baseScale;
    private SpriteRenderer[] spriteRenderers;
    private Image[] uiImages;
    private Color[] spriteOriginalColors;
    private Color[] uiOriginalColors;
    private bool isHighlighted;

    public void SetHighlighted(bool on)
    {
        isHighlighted = on;
        if (isHighlighted)
            ApplyTint();
        else
            Restore();
    }

    void Awake()
    {
        baseScale = transform.localScale;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        uiImages = GetComponentsInChildren<Image>(true);
        spriteOriginalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteOriginalColors[i] = spriteRenderers[i].color;
        uiOriginalColors = new Color[uiImages.Length];
        for (int i = 0; i < uiImages.Length; i++)
            uiOriginalColors[i] = uiImages[i].color;
    }

    void Update()
    {
        if (!isHighlighted)
            return;
        float s = 1f + scaleAmplitude * Mathf.Sin(Time.time * pulseSpeed);
        transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
    }

    private void ApplyTint()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr != null)
                sr.color = highlightColor;
        }
        for (int i = 0; i < uiImages.Length; i++)
        {
            var img = uiImages[i];
            if (img != null)
                img.color = highlightColor;
        }
    }

    private void Restore()
    {
        transform.localScale = baseScale;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr != null)
                sr.color = spriteOriginalColors[i];
        }
        for (int i = 0; i < uiImages.Length; i++)
        {
            var img = uiImages[i];
            if (img != null)
                img.color = uiOriginalColors[i];
        }
    }
}
