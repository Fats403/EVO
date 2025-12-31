using System.Collections;
using UnityEngine;

/// <summary>
/// Simple controller for a deterministic coin flip animation that reveals
/// heads/tails on the final frame.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class CoinFlipController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField]
    private Animator animator;

    [Tooltip("Trigger parameter that starts the flip animation.")]
    [SerializeField]
    private string playTriggerName = "Play";

    [Tooltip("Integer parameter used to choose heads/tails. 0 = Heads, 1 = Tails.")]
    [SerializeField]
    private string resultParameterName = "Result";

    [Header("UI Motion")]
    [Tooltip("Optional CanvasGroup used for fading the coin in and out.")]
    [SerializeField]
    private CanvasGroup canvasGroup;

    [Tooltip("How far (in anchored UI units) the coin drops in from above at the start.")]
    [SerializeField]
    private float introDropDistance = 50f;

    [Header("Timings (seconds)")]
    [SerializeField]
    private float initialDelay = 1f;

    [SerializeField]
    private float introDropDuration = 0.5f;

    [SerializeField]
    private float preFlipHoldSeconds = 1f;

    [SerializeField]
    private float postFlipHoldSeconds = 2f;

    [SerializeField]
    private float fadeOutDuration = 0.25f;

    private bool _isPlaying;
    private RectTransform _rectTransform;
    private Vector2 _originalAnchoredPosition;
    private bool _hasOriginalPosition;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        _rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (_rectTransform != null)
        {
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            _hasOriginalPosition = true;
        }

        // Ensure the coin starts fully transparent; it will fade in when the
        // sequence runs.
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Full front-end sequence for the coin:
    /// - Waits initialDelay from scene start.
    /// - Drops the coin down with a small fade-in.
    /// - Waits preFlipHoldSeconds.
    /// - Plays the flip (heads/tails).
    /// - Waits postFlipHoldSeconds.
    ///
    /// This does NOT fade the coin away; call FadeOutAndHide afterwards so
    /// GameManager can show text between the flip and the fade.
    /// </summary>
    public IEnumerator PlayCoinSequence(int result)
    {
        if (animator == null)
            yield break;

        // Ensure we start invisible before being (re)enabled.
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        gameObject.SetActive(true);

        if (_rectTransform != null && _hasOriginalPosition)
        {
            var startPos = _originalAnchoredPosition + Vector2.up * introDropDistance;
            _rectTransform.anchoredPosition = startPos;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        // Initial delay after entering the scene.
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        // Drop + fade-in.
        if (_rectTransform != null && introDropDuration > 0f && _hasOriginalPosition)
        {
            Vector2 startPos = _originalAnchoredPosition + Vector2.up * introDropDistance;
            Vector2 endPos = _originalAnchoredPosition;
            float t = 0f;
            while (t < introDropDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / introDropDuration);
                // Ease-in for a more natural "fall" feeling (starts slow, accelerates).
                p = p * p;
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
                if (canvasGroup != null)
                    canvasGroup.alpha = p;
                yield return null;
            }
            _rectTransform.anchoredPosition = endPos;
        }
        else
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        if (preFlipHoldSeconds > 0f)
            yield return new WaitForSeconds(preFlipHoldSeconds);

        // Play the actual flip animation (stops on heads/tails). At this point
        // the coin visibly shows the result; callers can show text and then
        // optionally wait postFlipHoldSeconds before fading out.
        yield return PlayCoinFlip(result);
    }

    /// <summary>
    /// Plays only the flip animation and waits until the final frame has been
    /// reached. Does not perform any intro / fade logic.
    ///
    /// result: 0 = Heads (host/Player1 goes first), 1 = Tails (peer/Player2 goes first).
    /// </summary>
    public IEnumerator PlayCoinFlip(int result)
    {
        if (animator == null)
            yield break;

        // Ensure the coin is visible while flipping.
        gameObject.SetActive(true);

        _isPlaying = true;

        if (!string.IsNullOrEmpty(resultParameterName))
            animator.SetInteger(resultParameterName, result);

        if (!string.IsNullOrEmpty(playTriggerName))
            animator.SetTrigger(playTriggerName);

        // Wait until the animation event marks the flip as finished.
        while (_isPlaying)
            yield return null;
    }

    /// <summary>
    /// Fades the coin out and disables the GameObject at the end.
    /// </summary>
    public IEnumerator FadeOutAndHide()
    {
        if (canvasGroup == null || fadeOutDuration <= 0f)
        {
            gameObject.SetActive(false);
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Exposes the configured post-flip hold so GameManager can decide how long
    /// to keep the coin on-screen after the result is visible.
    /// </summary>
    public float PostFlipHoldSeconds => postFlipHoldSeconds;

    /// <summary>
    /// Called from the last frame of the flip animation via an Animation Event.
    /// </summary>
    public void OnFlipAnimationFinished()
    {
        _isPlaying = false;
    }
}
