using System.Collections;
using UnityEngine;

/// <summary>
/// Handles the visual animation of a card being drawn from the deck into the player's hand.
/// Spawns a temporary card-back at the deck anchor, flies it toward the hand, then flips into
/// the real card UI created by the DeckManager.
/// </summary>
public class CardDrawAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("UI anchor representing the top of the deck in the player's UI.")]
    public RectTransform deckAnchor;

    [Tooltip("The panel containing the player's hand (parent of card UI objects).")]
    public RectTransform handPanel;

    [Tooltip("UI prefab for the card back used during the draw animation.")]
    public GameObject cardBackPrefab;

    [Tooltip("DeckManager used to actually create the front-side card UI when the flip completes.")]
    public DeckManager deckManager;

    [Header("Timing")]
    [Tooltip("Time for the card-back to travel from deck to hand (seconds).")]
    public float travelDuration = 0.5f;

    [Tooltip("Total time for the flip animation (seconds).")]
    public float flipDuration = 0.25f;

    [Tooltip("How far the card is pulled downward off the deck before flying toward the hand.")]
    public float pullDownDistance = 80f;

    [Range(0.05f, 0.9f)]
    [Tooltip("Fraction of travel time spent on the initial pull-down motion.")]
    public float pullDownPortion = 0.35f;

    [Header("Curves")]
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve flipCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// Plays a draw animation for the given card data. Falls back to an immediate spawn if
    /// required references are missing.
    /// </summary>
    public IEnumerator DrawCardAnimated(ScriptableObject data)
    {
        if (data == null)
            yield break;

        // If we don't have everything wired up, fall back to an immediate spawn.
        if (
            deckManager == null
            || cardBackPrefab == null
            || deckAnchor == null
            || handPanel == null
        )
        {
            deckManager?.CreateCardUI(data, triggerLayoutAndUI: true);
            yield break;
        }

        // Spawn temporary card-back near the deck.
        GameObject backGO = Instantiate(cardBackPrefab, deckAnchor.parent);
        RectTransform backRT =
            backGO.GetComponent<RectTransform>() ?? backGO.AddComponent<RectTransform>();
        backRT.position = deckAnchor.position;
        backRT.localScale = Vector3.one;
        backRT.localRotation = deckAnchor.localRotation;

        Vector3 startPos = deckAnchor.position;
        HandLayoutController layoutForTarget =
            handPanel != null ? handPanel.GetComponentInParent<HandLayoutController>() : null;
        Vector3 predictedEnd =
            layoutForTarget != null
                ? layoutForTarget.GetPredictedWorldPositionForNewCard()
                : handPanel.position;
        Vector3 pullPos = startPos + new Vector3(0f, -Mathf.Abs(pullDownDistance), 0f);
        Vector3 endPos = predictedEnd;

        // Travel from deck to hand: first pull straight down off the deck, then arc toward the hand.
        float travelTime = Mathf.Max(0.01f, travelDuration);
        float pullTime = Mathf.Clamp01(pullDownPortion) * travelTime;
        float moveTime = Mathf.Max(0.01f, travelTime - pullTime);

        float t = 0f;
        // Phase 1: pull downwards from the deck.
        if (pullTime > 0.01f)
        {
            while (t < pullTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / pullTime);
                float eased = moveCurve != null ? moveCurve.Evaluate(u) : u;
                backRT.position = Vector3.LerpUnclamped(startPos, pullPos, eased);
                backRT.localScale = Vector3.one;
                yield return null;
            }
        }
        else
        {
            backRT.position = pullPos;
        }

        // Phase 2: fly from the pulled-down position toward the hand.
        t = 0f;
        while (t < moveTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / moveTime);
            float eased = moveCurve != null ? moveCurve.Evaluate(u) : u;
            backRT.position = Vector3.LerpUnclamped(pullPos, endPos, eased);
            // Slight scale-up as it approaches the hand.
            float scale = Mathf.LerpUnclamped(0.95f, 1.05f, eased);
            backRT.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        backRT.position = endPos;
        backRT.localScale = Vector3.one;

        // Flip: first half, shrink the back's X scale to 0.
        float totalFlip = Mathf.Max(0.01f, flipDuration);
        float halfFlip = totalFlip * 0.5f;
        t = 0f;
        Vector3 backStartScale = backRT.localScale;
        while (t < halfFlip)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / halfFlip);
            float eased = flipCurve != null ? flipCurve.Evaluate(u) : u;
            float sx = Mathf.LerpUnclamped(backStartScale.x, 0f, eased);
            backRT.localScale = new Vector3(sx, backStartScale.y, backStartScale.z);
            yield return null;
        }

        // Remove the back; we'll flip in the real card front.
        Destroy(backGO);

        // Create the real card UI but do not trigger layout/hand UI yet.
        if (layoutForTarget != null)
            layoutForTarget.suppressNextAutoLayout = true;

        GameObject cardGO = deckManager.CreateCardUI(data, triggerLayoutAndUI: false);
        if (cardGO != null)
        {
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            if (cardRT != null)
            {
                // Start the front exactly where the back ended, with X scale at 0, then grow to full.
                cardRT.position = endPos;
                Vector3 originalScale = cardRT.localScale;
                if (originalScale == Vector3.zero)
                    originalScale = Vector3.one;
                cardRT.localScale = new Vector3(0f, originalScale.y, originalScale.z);

                t = 0f;
                while (t < halfFlip)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / halfFlip);
                    float eased = flipCurve != null ? flipCurve.Evaluate(u) : u;
                    float sx = Mathf.LerpUnclamped(0f, originalScale.x, eased);
                    cardRT.localScale = new Vector3(sx, originalScale.y, originalScale.z);
                    yield return null;
                }
                cardRT.localScale = originalScale;
            }
        }

        // Let the hand re-fan now that the new card is present.
        HandLayoutController layout = handPanel?.GetComponentInParent<HandLayoutController>();
        layout?.RequestLayout();
        deckManager.UpdateHandUI();
    }
}
