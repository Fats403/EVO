using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardPreviewManager : MonoBehaviour
{
    public static CardPreviewManager Instance { get; private set; }

    private enum ForcedPreviewType
    {
        None,
        Creature,
        Effect,
    }

    [Header("Preview UI")]
    [Tooltip("Creature card preview UI anchored to the HUD.")]
    public CreatureCardUI creaturePreviewUI;

    [Tooltip("Effect card preview UI anchored to the HUD.")]
    public EffectCardUI effectPreviewUI;

    [Tooltip("Optional caption text shown only for forced previews (who played what).")]
    public TextMeshProUGUI forcedCaptionText;

    [Header("Timing")]
    [Tooltip("Seconds a forced preview remains visible before hover can take over.")]
    public float forcedDisplaySeconds = 3.0f;

    private Creature hoverCreature;
    private ForcedPreviewType forcedType = ForcedPreviewType.None;
    private Coroutine forcedRoutine;

    [Header("Animation")]
    [Tooltip("Seconds to fade/scale previews in.")]
    public float showDuration = 0.2f;

    [Tooltip("Seconds to fade/scale previews out.")]
    public float hideDuration = 0.15f;

    [Tooltip("Starting scale for previews as they appear.")]
    public float showStartScale = 0.8f;

    [Tooltip("Final scale once the preview has fully appeared.")]
    public float idleScale = 1.5f;

    private CanvasGroup creatureGroup;
    private RectTransform creatureRect;
    private Coroutine creatureAnimRoutine;

    private CanvasGroup effectGroup;
    private RectTransform effectRect;
    private Coroutine effectAnimRoutine;

    // One-time flags so we only strip interaction from the HUD previews once.
    private bool creaturePreviewMadePassive;
    private bool effectPreviewMadePassive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HideAll();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowHoverCreature(Creature creature)
    {
        hoverCreature = creature;
        if (forcedType != ForcedPreviewType.None)
            return;

        if (creature != null && creature.data != null)
            ShowCreaturePreview(creature);
        else
            HideCreaturePreview();
    }

    public void HideHoverCreature(Creature creature)
    {
        if (hoverCreature != creature)
            return;

        hoverCreature = null;
        if (forcedType != ForcedPreviewType.None)
            return;

        HideCreaturePreview();
    }

    public void ShowForcedCreature(Creature creature)
    {
        if (creature == null || creature.data == null)
            return;

        forcedType = ForcedPreviewType.Creature;
        ShowCreaturePreview(creature);
        // Caption: who played what (forced previews only)
        if (forcedCaptionText != null)
        {
            // Use network-aware check for local vs opponent
            string who = NetworkRoleHelper.IsLocalPlayer(creature.owner)
                ? "You play"
                : "Opponent plays";
            forcedCaptionText.text = $"{who} {creature.data.cardName}";
            forcedCaptionText.gameObject.SetActive(true);
        }
        BeginForcedTimer();
    }

    public void ShowForcedEffect(EffectCard card, SlotOwner owner)
    {
        if (card == null)
            return;

        forcedType = ForcedPreviewType.Effect;
        ShowEffectPreview(card, owner);
        // Caption: who played what (forced previews only)
        if (forcedCaptionText != null)
        {
            // Use network-aware check for local vs opponent
            string who = NetworkRoleHelper.IsLocalPlayer(owner) ? "You play" : "Opponent plays";
            forcedCaptionText.text = $"{who} {card.effectName}";
            forcedCaptionText.gameObject.SetActive(true);
        }
        BeginForcedTimer();
    }

    /// <summary>
    /// Show an effect card preview with a custom instructional caption, without
    /// starting the forced preview timer. Used for manual-selection flows.
    /// </summary>
    public void ShowEffectSelection(EffectCard card, SlotOwner owner, string caption)
    {
        if (card == null)
            return;

        forcedType = ForcedPreviewType.Effect;
        ShowEffectPreview(card, owner);

        if (forcedCaptionText != null)
        {
            forcedCaptionText.text = caption ?? string.Empty;
            forcedCaptionText.gameObject.SetActive(true);
        }
    }

    public void ClearForced()
    {
        if (forcedRoutine != null)
        {
            StopCoroutine(forcedRoutine);
            forcedRoutine = null;
        }

        forcedType = ForcedPreviewType.None;

        if (forcedCaptionText != null)
        {
            forcedCaptionText.gameObject.SetActive(false);
            forcedCaptionText.text = string.Empty;
        }

        if (hoverCreature != null && hoverCreature.data != null)
            ShowCreaturePreview(hoverCreature);
        else
            HideAll();
    }

    public void HideAll()
    {
        HideCreaturePreview();
        HideEffectPreview();
        if (forcedCaptionText != null)
        {
            forcedCaptionText.gameObject.SetActive(false);
            forcedCaptionText.text = string.Empty;
        }
    }

    void ShowCreaturePreview(Creature creature)
    {
        if (creaturePreviewUI == null || creature == null || creature.data == null)
            return;

        EnsureCreaturePreviewComponents();

        creaturePreviewUI.gameObject.SetActive(true);
        creaturePreviewUI.Initialize(creature.data);

        if (effectPreviewUI != null)
            HideEffectPreview();

        if (creatureAnimRoutine != null)
            StopCoroutine(creatureAnimRoutine);
        creatureAnimRoutine = StartCoroutine(FadeScaleIn(creatureGroup, creatureRect));
    }

    void HideCreaturePreview()
    {
        if (creaturePreviewUI == null || !creaturePreviewUI.gameObject.activeSelf)
            return;

        EnsureCreaturePreviewComponents();

        if (creatureAnimRoutine != null)
            StopCoroutine(creatureAnimRoutine);
        creatureAnimRoutine = StartCoroutine(
            FadeScaleOut(creatureGroup, creatureRect, creaturePreviewUI.gameObject)
        );
    }

    void ShowEffectPreview(EffectCard card, SlotOwner owner)
    {
        if (effectPreviewUI == null || card == null)
            return;

        EnsureEffectPreviewComponents();

        effectPreviewUI.owner = owner;
        effectPreviewUI.gameObject.SetActive(true);
        effectPreviewUI.Initialize(card);

        if (creaturePreviewUI != null)
            HideCreaturePreview();

        if (effectAnimRoutine != null)
            StopCoroutine(effectAnimRoutine);
        effectAnimRoutine = StartCoroutine(FadeScaleIn(effectGroup, effectRect));
    }

    void HideEffectPreview()
    {
        if (effectPreviewUI == null || !effectPreviewUI.gameObject.activeSelf)
            return;

        EnsureEffectPreviewComponents();

        if (effectAnimRoutine != null)
            StopCoroutine(effectAnimRoutine);
        effectAnimRoutine = StartCoroutine(
            FadeScaleOut(effectGroup, effectRect, effectPreviewUI.gameObject)
        );
    }

    void BeginForcedTimer()
    {
        if (forcedRoutine != null)
            StopCoroutine(forcedRoutine);
        forcedRoutine = StartCoroutine(ForcedTimer());
    }

    IEnumerator ForcedTimer()
    {
        float duration = Mathf.Max(0.25f, forcedDisplaySeconds);
        yield return new WaitForSeconds(duration);
        forcedRoutine = null;
        forcedType = ForcedPreviewType.None;
        if (forcedCaptionText != null)
        {
            forcedCaptionText.gameObject.SetActive(false);
            forcedCaptionText.text = string.Empty;
        }
        if (hoverCreature != null && hoverCreature.data != null)
            ShowCreaturePreview(hoverCreature);
        else
            HideAll();
    }

    void EnsureCreaturePreviewComponents()
    {
        if (creaturePreviewUI == null)
            return;
        if (creatureRect == null)
            creatureRect = creaturePreviewUI.transform as RectTransform;
        if (creatureGroup == null)
        {
            creatureGroup = creaturePreviewUI.GetComponent<CanvasGroup>();
            if (creatureGroup == null)
                creatureGroup = creaturePreviewUI.gameObject.AddComponent<CanvasGroup>();
        }

        // Ensure the HUD preview cannot be dragged or intercept pointer events.
        // We only want it to display information; all interaction should go to
        // the actual hand cards / board.
        if (!creaturePreviewMadePassive)
        {
            var baseCard = creaturePreviewUI.GetComponent<BaseCardUI>();
            if (baseCard != null)
            {
                baseCard.enabled = false;
            }

            var graphics = creaturePreviewUI.GetComponentsInChildren<Graphic>(
                includeInactive: true
            );
            foreach (var g in graphics)
            {
                if (g != null)
                    g.raycastTarget = false;
            }

            creaturePreviewMadePassive = true;
        }
    }

    void EnsureEffectPreviewComponents()
    {
        if (effectPreviewUI == null)
            return;
        if (effectRect == null)
            effectRect = effectPreviewUI.transform as RectTransform;
        if (effectGroup == null)
        {
            effectGroup = effectPreviewUI.GetComponent<CanvasGroup>();
            if (effectGroup == null)
                effectGroup = effectPreviewUI.gameObject.AddComponent<CanvasGroup>();
        }

        // Ensure the HUD preview for effect cards cannot be dragged or clicked.
        if (!effectPreviewMadePassive)
        {
            var baseCard = effectPreviewUI.GetComponent<BaseCardUI>();
            if (baseCard != null)
            {
                baseCard.enabled = false;
            }

            var graphics = effectPreviewUI.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var g in graphics)
            {
                if (g != null)
                    g.raycastTarget = false;
            }

            effectPreviewMadePassive = true;
        }
    }

    IEnumerator FadeScaleIn(CanvasGroup group, RectTransform rect)
    {
        if (group == null || rect == null)
            yield break;

        float dur = Mathf.Max(0.01f, showDuration);
        float t = 0f;
        group.alpha = 0f;
        rect.localScale = Vector3.one * showStartScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            group.alpha = Mathf.Lerp(0f, 1f, u);

            // Simple one-way scale: from showStartScale up to idleScale (no bounce).
            float scale = Mathf.Lerp(showStartScale, idleScale, u);
            rect.localScale = Vector3.one * scale;
            yield return null;
        }

        group.alpha = 1f;
        rect.localScale = Vector3.one * idleScale;
    }

    IEnumerator FadeScaleOut(CanvasGroup group, RectTransform rect, GameObject go)
    {
        if (group == null || rect == null || go == null)
            yield break;

        float dur = Mathf.Max(0.01f, hideDuration);
        float t = 0f;
        float startAlpha = group.alpha;
        Vector3 startScale = rect.localScale;
        Vector3 targetScale = Vector3.one * showStartScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            group.alpha = Mathf.Lerp(startAlpha, 0f, u);
            rect.localScale = Vector3.Lerp(startScale, targetScale, u);
            yield return null;
        }

        group.alpha = 0f;
        rect.localScale = Vector3.one * idleScale;
        go.SetActive(false);
    }
}
