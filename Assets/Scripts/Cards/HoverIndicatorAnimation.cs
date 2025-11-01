using UnityEngine;

public class HoverIndicatorAnimation : MonoBehaviour
{
    [Header("Pulse Settings")]
    [Tooltip("Minimum scale multiplier")]
    [SerializeField] private float minScale = 0.9f;

    [Tooltip("Maximum scale multiplier")]
    [SerializeField] private float maxScale = 1.1f;

    [Tooltip("Speed of the pulsing animation")]
    [SerializeField] private float pulseSpeed = 2f;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // Create a pulsing effect using sine wave
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f; // Normalize to 0-1 range

        // Interpolate between min and max scale
        float currentScale = Mathf.Lerp(minScale, maxScale, pulse);

        // Apply the scale
        transform.localScale = originalScale * currentScale;
    }
}
