using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Creature : MonoBehaviour
{
    public CreatureCard data;
    public Image artworkImage;
    public int body;
    public int speed;
    public int eaten;
    public SlotOwner owner;
    public List<Trait> traits = new();
    public int maxHealth;
    public int currentHealth;
    public bool isDying;

    // Status stacks (Shielded, Infected, etc.)
    private readonly Dictionary<StatusTag, int> statuses = new();

    public int roundDamageDealt;
    public int roundHealingUndone;
    private readonly HashSet<Creature> damagedTargetsThisRound = new();

    // New per-round bookkeeping for the revamped scoring system.
    // Only living creatures score at the end of the round, so these are
    // accumulated during the round and converted into points during the
    // scoring phase (if still alive).
    [Header("Round Scoring (Runtime)")]
    public int roundKillBody; // Sum of victim body values killed this round.
    public int roundKillCount;
    public bool roundHasScavengePoint; // Avians: true if an enemy died while this avian was alive (cap 1).

    // Per-creature trait state flags (replaces static HashSets in traits for determinism)
    // These are cleared in ResetRoundBookkeeping() at round start.
    [Header("Trait State (Runtime)")]
    public bool traitUsedWhirlwind; // WhirlwindTrait: extra attack used this round
    public bool traitUsedBloodthirsty; // BloodthirstyTrait: extra attack used this round
    public bool traitUsedUndyingSpirit; // UndyingSpiritTrait: resurrection used (persists until death)
    public bool traitGrantRadiantShield; // RadiantScalesTrait: grant shield next round
    public bool traitGrantEvasiveStealth; // EvasiveTrait: grant stealth next round
    public bool traitGrantBloodRush; // BloodRushTrait: grant rage next round
    public int traitElementalHpBonus; // ElementalTrait: current wildfire HP bonus

    public static event Action<Creature, int> OnAnyCreatureHealed;

    public bool IsWounded => currentHealth < maxHealth;
    public bool IsImmovable => data != null && data.isImmovable;

    [SerializeField]
    private TMP_Text speedText;

    [SerializeField]
    private TMP_Text bodyText;

    [SerializeField]
    private TMP_Text healthText;
    private int baseBody;
    private int baseSpeed;

    public void Initialize(CreatureCard cardData)
    {
        data = cardData;
        name = $"{data.cardName}";

        body = data.size;
        speed = data.speed;
        baseBody = data.size;
        baseSpeed = data.speed;
        eaten = 0;
        maxHealth = Mathf.Max(1, data != null ? data.maxHealth : 1);
        currentHealth = maxHealth;
        isDying = false;
        roundDamageDealt = 0;
        roundHealingUndone = 0;
        damagedTargetsThisRound.Clear();
        roundKillBody = 0;
        roundKillCount = 0;
        roundHasScavengePoint = false;

        // Reset all trait state flags
        traitUsedWhirlwind = false;
        traitUsedBloodthirsty = false;
        traitUsedUndyingSpirit = false;
        traitGrantRadiantShield = false;
        traitGrantEvasiveStealth = false;
        traitGrantBloodRush = false;
        traitElementalHpBonus = 0;

        traits.Clear();
        if (data.baseTraits != null && data.baseTraits.Length > 0)
            traits.AddRange(data.baseTraits);

        RefreshStatsUI();

        if (artworkImage != null && data.artwork != null)
        {
            artworkImage.sprite = data.artwork;
        }

        // Ensure interaction handler and a 2D collider for mouse events
        if (GetComponent<CreatureInteractionHandler>() == null)
            gameObject.AddComponent<CreatureInteractionHandler>();
        if (GetComponent<Collider2D>() == null)
        {
            var bc = gameObject.AddComponent<BoxCollider2D>();
            if (artworkImage != null && artworkImage.sprite != null)
            {
                // approximate size from sprite bounds
                bc.size = artworkImage.sprite.bounds.size;
            }
        }
    }

    private void OnEnable()
    {
        OnAnyCreatureHealed += HandleAnyCreatureHealed;
    }

    private void OnDisable()
    {
        OnAnyCreatureHealed -= HandleAnyCreatureHealed;
    }

    private void HandleAnyCreatureHealed(Creature healed, int amount)
    {
        if (amount <= 0)
            return;
        if (damagedTargetsThisRound != null && damagedTargetsThisRound.Contains(healed))
        {
            roundHealingUndone += amount;
        }
    }

    public IEnumerator PlayEatAnimation(Vector3 targetPos, float duration)
    {
        // Cache original position and Z so we can safely adjust layering during the lunge.
        Vector3 originalStart = transform.position;
        float originalZ = originalStart.z;
        // Nudge slightly toward the camera so this creature renders above neighbors,
        // but keep it on the same "plane" so background ordering is preserved.
        float foregroundZ = originalZ - 0.1f;

        Vector3 start = new Vector3(originalStart.x, originalStart.y, foregroundZ);
        targetPos.z = foregroundZ;
        // Lunge ~70% of the way toward the food pile in X/Y, not fully into it.
        Vector3 mid = Vector3.Lerp(start, targetPos, 0.5f);

        // Temporarily bump this creature's render order above neighbors.
        const int sortBoost = 100;
        SortingState sortingState = SortingUtils.PushToForeground(transform, sortBoost);

        // Snap into the foreground start position before animating so we don't fight
        // with any other movement that might have happened this frame.
        transform.position = start;

        // Timings: 20% move in, 60% hold/pulse, 20% move back
        float moveTime = duration * 0.2f;
        float holdTime = duration * 0.6f;

        float t = 0f;
        // Move to food
        while (t < moveTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / moveTime);
            u = Mathf.Sin(u * Mathf.PI * 0.5f); // Ease out
            transform.position = Vector3.Lerp(start, mid, u);
            yield return null;
        }

        // Subtle double green pulse while holding: two small pulses toward a softer green.
        if (artworkImage != null)
        {
            Color originalCol = artworkImage.color;
            // Softer green so the art never goes fully neon.
            Color pulseCol = Color.Lerp(originalCol, Color.green, 0.45f);
            int pulses = 2;
            float halfPulse = holdTime / (pulses * 2f);

            for (int i = 0; i < pulses; i++)
            {
                // Fade to pulse color
                t = 0f;
                while (t < halfPulse)
                {
                    t += Time.deltaTime;
                    float u = t / halfPulse;
                    artworkImage.color = Color.Lerp(originalCol, pulseCol, u);
                    yield return null;
                }

                // Fade back to original
                t = 0f;
                while (t < halfPulse)
                {
                    t += Time.deltaTime;
                    float u = t / halfPulse;
                    artworkImage.color = Color.Lerp(pulseCol, originalCol, u);
                    yield return null;
                }
            }
            artworkImage.color = originalCol;
        }
        else
        {
            yield return new WaitForSeconds(holdTime);
        }

        // Move back
        t = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / moveTime);
            u = 1f - Mathf.Cos(u * Mathf.PI * 0.5f); // Ease in
            transform.position = Vector3.Lerp(mid, start, u);
            yield return null;
        }

        // Restore any modified sorting once the animation is complete.
        SortingUtils.RestoreSorting(sortingState);

        // Ensure we are exactly back at the original starting world position (including Z).
        transform.position = originalStart;
    }

    /// <summary>
    /// Generalized pulse effect: fades to target color, pulses alpha/intensity slightly, then fades back.
    /// </summary>
    public IEnumerator PulseColor(Color targetColor, float duration, int pulses = 1)
    {
        if (artworkImage == null)
            yield break;

        Color original = artworkImage.color;

        float halfPulse = duration / (pulses * 2f);

        for (int i = 0; i < pulses; i++)
        {
            // Lerp to target
            float t = 0f;
            while (t < halfPulse)
            {
                t += Time.deltaTime;
                float u = t / halfPulse;
                artworkImage.color = Color.Lerp(original, targetColor, u);
                yield return null;
            }

            // Lerp back
            t = 0f;
            while (t < halfPulse)
            {
                t += Time.deltaTime;
                float u = t / halfPulse;
                artworkImage.color = Color.Lerp(targetColor, original, u);
                yield return null;
            }
        }
        artworkImage.color = original;
    }

    public void PlayVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, transform.position, Quaternion.identity);
        }
    }

    public IEnumerator PlayAttackBump(float distance = 0.3f, float duration = 0.2f)
    {
        Vector3 start = transform.position;
        float dir = (owner == SlotOwner.Player1) ? 1f : -1f;
        Vector3 offset = Vector3.up * dir * distance;
        Vector3 mid = start + offset;
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / half);
            transform.position = Vector3.Lerp(start, mid, u);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / half);
            transform.position = Vector3.Lerp(mid, start, u);
            yield return null;
        }
    }

    /// <summary>
    /// Smooth "effect hit" presentation: a gentle scale up plus vertical bob,
    /// then return to the starting transform.
    /// </summary>
    public IEnumerator PlayEffectHitBounce(
        float scaleMultiplier = 1.08f,
        float height = 0.2f,
        float duration = 0.45f
    )
    {
        if (duration <= 0f)
            yield break;

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        Vector3 peakPos = startPos + Vector3.up * height;
        Vector3 peakScale = startScale * scaleMultiplier;

        float half = duration * 0.5f;
        float t = 0f;

        // Move/scale up
        while (t < half)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / half);
            // Ease-out curve for going up
            u = Mathf.Sin(u * Mathf.PI * 0.5f);
            transform.position = Vector3.Lerp(startPos, peakPos, u);
            transform.localScale = Vector3.Lerp(startScale, peakScale, u);
            yield return null;
        }

        // Move/scale back down
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / half);
            // Ease-in curve for coming back down
            u = 1f - Mathf.Cos(u * Mathf.PI * 0.5f);
            transform.position = Vector3.Lerp(peakPos, startPos, u);
            transform.localScale = Vector3.Lerp(peakScale, startScale, u);
            yield return null;
        }

        // Snap back to the exact starting transform to avoid drift from rounding.
        transform.position = startPos;
        transform.localScale = startScale;
    }

    public IEnumerator FlashDamage(float duration = 0.12f)
    {
        if (artworkImage == null)
            artworkImage = GetComponentInChildren<Image>();
        if (artworkImage != null)
        {
            Color original = artworkImage.color;
            artworkImage.color = GameColorPalette.Damage;
            yield return new WaitForSeconds(duration);
            artworkImage.color = original;
        }
    }

    /// <summary>
    /// Apply incoming damage to this creature, returning the actual HP lost after
    /// shields, absorb, reflect, etc. This value is also what contributes to
    /// roundDamageDealt and scoring.
    /// </summary>
    public int ApplyDamage(
        int amount,
        Creature source,
        GameObject vfxPrefab = null,
        string damageSourceLabel = null
    )
    {
        return ApplyDamageInternal(
            amount,
            source,
            allowReflect: true,
            vfxPrefab,
            damageSourceLabel
        );
    }

    /// <summary>
    /// Internal damage routine that can optionally skip reflect (used when
    /// reflecting damage back to an attacker to avoid infinite loops).
    /// Returns the actual HP lost by this creature.
    /// </summary>
    private int ApplyDamageInternal(
        int amount,
        Creature source,
        bool allowReflect,
        GameObject vfxPrefab = null,
        string damageSourceLabel = null
    )
    {
        // Shielded negates the next incoming damage instance per charge
        if (amount > 0 && GetStatus(StatusTag.Shield) > 0)
        {
            DecrementStatus(StatusTag.Shield, 1);
            string label =
                damageSourceLabel != null ? $"Shielded ({damageSourceLabel})" : "Shielded";
            FeedbackManager.Instance?.ShowFloatingText(
                label,
                transform.position,
                GameColorPalette.Shield
            );
            return 0;
        }

        // Reflect: negate and reflect the would-be damage (one charge), but do not re-reflect
        if (allowReflect && amount > 0 && GetStatus(StatusTag.Reflect) > 0)
        {
            DecrementStatus(StatusTag.Reflect, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Reflect",
                transform.position,
                GameColorPalette.Reflect
            );
            int reflected = Mathf.Max(0, amount);
            if (source != null)
            {
                // Apply reflected damage to the attacker; allow their Shielded but not their Reflect to trigger.
                // Pass null VFX for reflection or maybe a specific one later.
                int reflectedApplied = source.ApplyDamageInternal(
                    reflected,
                    this,
                    allowReflect: false,
                    vfxPrefab: null,
                    damageSourceLabel: null
                );
                if (reflectedApplied > 0)
                {
                    FeedbackManager.Instance?.ShowFloatingText(
                        $"-{reflectedApplied} HP [Reflect]",
                        source.transform.position,
                        GameColorPalette.Damage
                    );
                }
            }
            // Original target took no damage.
            return 0;
        }

        // Absorb: stacks reduce damage cumulatively; allow damage to be 0; decrement stacks by absorbed amount
        if (amount > 0)
        {
            int absorb = GetStatus(StatusTag.Absorb);
            if (absorb > 0)
            {
                int absorbed = Mathf.Min(absorb, amount);
                amount -= absorbed;
                DecrementStatus(StatusTag.Absorb, absorbed);
                if (absorbed > 0)
                {
                    string label =
                        damageSourceLabel != null
                            ? $"Absorbed [{absorbed}] ({damageSourceLabel})"
                            : $"Absorbed [{absorbed}]";
                    FeedbackManager.Instance?.ShowFloatingText(
                        label,
                        transform.position,
                        GameColorPalette.Absorb
                    );
                }
                if (amount <= 0)
                {
                    return 0;
                }
            }
        }

        int dmg = Mathf.Max(0, amount);
        if (dmg == 0)
            return 0;

        // Play VFX if provided
        PlayVFX(vfxPrefab);

        // Taking real damage breaks Stealth
        if (GetStatus(StatusTag.Stealth) > 0)
            ClearStatus(StatusTag.Stealth);
        int prevHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - dmg);
        int applied = Mathf.Max(0, prevHealth - currentHealth);
        if (applied <= 0)
            return 0;
        if (source != null)
        {
            // Clamp scored/recorded damage to the actual HP lost so damage and
            // scoring never exceed the victim's remaining health.
            source.roundDamageDealt += applied;
            if (!source.damagedTargetsThisRound.Contains(this))
                source.damagedTargetsThisRound.Add(this);

            // Feeding rule: any successful damaging hit counts as "fed" for carnivores.
            if (source.data != null && source.data.type == CardType.Carnivore)
            {
                source.eaten = Mathf.Max(source.eaten, 1);
            }
        }
        StartCoroutine(FlashDamage(0.2f));
        // Trait hooks
        if (source != null && source.traits != null)
        {
            foreach (var tr in source.traits)
            {
                if (tr != null)
                    tr.OnDamageDealt(source, this, applied);
            }
        }
        if (traits != null)
        {
            foreach (var tr in traits)
            {
                if (tr != null)
                    tr.OnDamageTaken(this, source, applied);
            }
        }
        // Global post-damage notification
        // CRITICAL: Sort by slot index for deterministic iteration order
        var dmgSlots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .ToDictionary(s => s, s => s.index);
        var all = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .OrderBy(c => GetSlotIndexForCreature(c, dmgSlots))
            .ToArray();
        foreach (var other in all)
        {
            if (other == null || other.traits == null)
                continue;
            foreach (var tr in other.traits.ToArray())
            {
                if (tr != null)
                    tr.OnAnyDamage(other, this, source, applied);
            }
        }
        RefreshStatsUI();
        if (currentHealth == 0)
        {
            // Record kill credit for the damage source (scored later only if the killer survives).
            if (source != null)
            {
                source.RecordKill(this);
            }
            Kill("Damage");
        }

        return applied;
    }

    public void Heal(int amount)
    {
        int prev = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        int healed = Mathf.Max(0, currentHealth - prev);
        if (healed > 0)
        {
            // Any healing clears all Bleeding stacks
            if (GetStatus(StatusTag.Bleed) > 0)
                ClearStatus(StatusTag.Bleed);
            OnAnyCreatureHealed?.Invoke(this, healed);
            RefreshStatsUI();
        }
    }

    public void Kill(string reason, GameObject vfxPrefab = null)
    {
        if (isDying)
            return;
        isDying = true;

        PlayVFX(vfxPrefab);

        // Avian scavenging: when an ENEMY creature dies, living avians may scavenge:
        // - They become "fed" for starvation purposes (eaten set to at least 1)
        // - They may earn +1 scavenge point at end of round (cap 1 per avian per round)
        // CRITICAL: Sort by slot index for deterministic iteration order
        var deathSlots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .ToDictionary(s => s, s => s.index);
        var allForDeath = FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .OrderBy(c => GetSlotIndexForCreature(c, deathSlots))
            .ToArray();
        foreach (var other in allForDeath)
        {
            if (other == null || other == this)
                continue;
            if (other.currentHealth <= 0 || other.isDying)
                continue;
            if (other.data != null && other.data.type == CardType.Avian)
            {
                // Only scavenge from enemy deaths.
                if (other.owner == owner)
                    continue;

                // Grant the scavenge point (capped at 1 per round).
                other.roundHasScavengePoint = true;

                // Feeding from deaths is survival-focused: one food is enough to avoid starvation.
                if (other.eaten <= 0)
                {
                    other.eaten = 1;
                    FeedbackManager.Instance?.ShowFloatingText(
                        "Scavenge",
                        other.transform.position,
                        GameColorPalette.ScavengeGain
                    );
                    FeedbackManager.Instance?.Log(
                        $"{FeedbackManager.TagOwner(other.owner)} {other.name} scavenges +1"
                    );
                }
            }
        }
        // Notify all traits about this death
        foreach (var other in allForDeath)
        {
            if (other == null || other == this)
                continue;
            if (other.traits == null)
                continue;
            var trSnapshot = other.traits.ToArray();
            foreach (var tr in trSnapshot)
            {
                if (tr != null)
                    tr.OnAnyDeath(other, this);
            }
        }
        var s = FindSlotOf(this);
        if (s != null)
            s.Vacate();
        StartCoroutine(FadeAndDestroy(0.5f));
    }

    private IEnumerator FadeAndDestroy(float duration)
    {
        float t = 0f;
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        var texts = GetComponentsInChildren<TMP_Text>(true);
        var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
        // capture original colors
        var srColors = renderers.Select(r => r != null ? r.color : Color.white).ToArray();
        var txtColors = texts.Select(txt => txt != null ? txt.color : Color.white).ToArray();
        var imgColors = images.Select(img => img != null ? img.color : Color.white).ToArray();
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float a = 1f - u;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    var c = srColors[i];
                    c.a = a;
                    renderers[i].color = c;
                }
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    var c = txtColors[i];
                    c.a = a;
                    texts[i].color = c;
                }
            }
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    var c = imgColors[i];
                    c.a = a;
                    images[i].color = c;
                }
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    public void ResetRoundBookkeeping()
    {
        roundDamageDealt = 0;
        roundHealingUndone = 0;
        if (damagedTargetsThisRound != null)
            damagedTargetsThisRound.Clear();
        roundKillBody = 0;
        roundKillCount = 0;
        roundHasScavengePoint = false;

        // Reset per-round trait flags (UndyingSpirit persists - only reset on creature death/init)
        traitUsedWhirlwind = false;
        traitUsedBloodthirsty = false;
        // Process "grant next round" effects before clearing them
        if (traitGrantRadiantShield)
        {
            AddStatus(StatusTag.Shield, 1);
            traitGrantRadiantShield = false;
        }
        if (traitGrantEvasiveStealth)
        {
            AddStatus(StatusTag.Stealth, 1);
            traitGrantEvasiveStealth = false;
        }
        if (traitGrantBloodRush)
        {
            AddStatus(StatusTag.Rage, 1);
            traitGrantBloodRush = false;
        }
    }

    public void RecordKill(Creature victim)
    {
        if (victim == null)
            return;
        if (data == null)
            return;
        // Only creatures that can meaningfully "get kills" care about this,
        // but we keep it generic so trait-granted attacks on herbivores still work.
        int bodyForScore = victim.GetEffectiveBodyForScoring();
        roundKillBody += Mathf.Max(0, bodyForScore);
        roundKillCount += 1;
    }

    public int GetEffectiveBodyForScoring()
    {
        int traitBody =
            (!HasStatus(StatusTag.Suppress) && traits != null)
                ? traits.Sum(t => t != null ? t.BodyBonus(this) : 0)
                : 0;
        int temp = GetStatus(StatusTag.BodyUp) - GetStatus(StatusTag.Malnourish);
        return body + traitBody + temp;
    }

    public void RefreshStatsUI()
    {
        // Speed display with bonuses and temp mods
        if (speedText != null)
        {
            int traitSpeed =
                (!HasStatus(StatusTag.Suppress) && traits != null)
                    ? traits.Sum(t => t != null ? t.SpeedBonus(this) : 0)
                    : 0;
            int tempSpeed = GetStatus(StatusTag.SpeedUp) - GetStatus(StatusTag.Fatigue);
            int displaySpeed = speed + tempSpeed + traitSpeed;
            speedText.text = displaySpeed.ToString();
            if (displaySpeed > baseSpeed)
                speedText.color = Color.green;
            else if (displaySpeed < baseSpeed)
                speedText.color = Color.red;
            else
                speedText.color = Color.white;
        }

        // Body display relative to base body
        if (bodyText != null)
        {
            int traitBody =
                (!HasStatus(StatusTag.Suppress) && traits != null)
                    ? traits.Sum(t => t != null ? t.BodyBonus(this) : 0)
                    : 0;
            int displayBody =
                body + traitBody + GetStatus(StatusTag.BodyUp) - GetStatus(StatusTag.Malnourish);
            bodyText.text = displayBody.ToString();
            if (displayBody > baseBody)
                bodyText.color = Color.green;
            else if (displayBody < baseBody)
                bodyText.color = Color.red;
            else
                bodyText.color = Color.white;
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}";
            if (IsWounded)
                healthText.color = GameColorPalette.TextNegative;
            else
                healthText.color = GameColorPalette.TextNeutral;
        }

        var sic = GetComponentInChildren<StatusIconController>(true);
        if (sic != null)
            sic.Refresh(this);
    }

    // --- Unified status API ---

    public int GetStatus(StatusTag tag)
    {
        return statuses.TryGetValue(tag, out var v) ? v : 0;
    }

    public bool HasStatus(StatusTag tag)
    {
        return GetStatus(tag) > 0;
    }

    public void AddStatus(StatusTag tag, int stacks = 1)
    {
        if (stacks <= 0)
            return;
        // Immune blocks negative statuses, consuming one charge
        if (IsNegativeStatus(tag) && GetStatus(StatusTag.Immune) > 0)
        {
            DecrementStatus(StatusTag.Immune, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                "Immune",
                transform.position,
                GameColorPalette.Immune
            );
            return;
        }

        // Mutual exclusivity: BodyUp vs Malnourished; SpeedUp vs Fatigued
        if (tag == StatusTag.BodyUp)
            ClearStatus(StatusTag.Malnourish);
        if (tag == StatusTag.Malnourish)
            ClearStatus(StatusTag.BodyUp);
        if (tag == StatusTag.SpeedUp)
            ClearStatus(StatusTag.Fatigue);
        if (tag == StatusTag.Fatigue)
            ClearStatus(StatusTag.SpeedUp);

        int newValue = GetStatus(tag) + stacks;

        // Stealth, Sheilded, Stunned, No Forage, Rage, Immune is non-stacking: clamp to 1
        if (tag == StatusTag.Stealth)
            newValue = newValue > 0 ? 1 : 0;
        if (tag == StatusTag.Shield)
            newValue = newValue > 0 ? 1 : 0;
        if (tag == StatusTag.Stun)
            newValue = newValue > 0 ? 1 : 0;
        if (tag == StatusTag.NoForage)
            newValue = newValue > 0 ? 1 : 0;
        if (tag == StatusTag.Rage)
            newValue = newValue > 0 ? 1 : 0;
        if (tag == StatusTag.Immune)
            newValue = newValue > 0 ? 1 : 0;

        statuses[tag] = newValue;
        RefreshStatsUI();
    }

    public void DecrementStatus(StatusTag tag, int amount = 1)
    {
        if (amount <= 0)
            return;
        int v = GetStatus(tag) - amount;
        if (v <= 0)
            statuses.Remove(tag);
        else
            statuses[tag] = v;
        RefreshStatsUI();
    }

    public void ClearStatus(StatusTag tag)
    {
        if (statuses.Remove(tag))
            RefreshStatsUI();
    }

    /// <summary>
    /// If this creature is currently Stealthed, clear Stealth and show a small
    /// "Revealed" popup. Use this when the creature takes an overt action
    /// (attack, forage, active trait, etc.).
    /// </summary>
    public void RevealIfStealthed()
    {
        if (GetStatus(StatusTag.Stealth) <= 0)
            return;
        ClearStatus(StatusTag.Stealth);
        FeedbackManager.Instance?.ShowFloatingText(
            "Revealed",
            transform.position,
            GameColorPalette.Reveal
        );
    }

    public System.Collections.Generic.IEnumerable<StatusTag> GetActiveStatusTags()
    {
        foreach (var kv in statuses)
        {
            if (kv.Value > 0)
                yield return kv.Key;
        }
    }

    private static bool IsNegativeStatus(StatusTag tag)
    {
        return StatusTagGroups.Negative.Contains(tag);
    }

    public void ClearAllNegativeStatuses()
    {
        foreach (var tag in StatusTagGroups.Negative)
        {
            ClearStatus(tag);
        }
    }

    /// <summary>
    /// Ticks start-of-round status effects and returns true if any visible or
    /// state-changing effect occurred (used to decide whether to pause for UI).
    /// </summary>
    public bool TickStatusesAtRoundStart()
    {
        bool didAny = false;
        // Infected: deal 1, then -1 stack
        if (GetStatus(StatusTag.Infection) > 0)
        {
            didAny = true;
            int applied = ApplyDamage(1, null, null, "Infected");
            DecrementStatus(StatusTag.Infection, 1);
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP (Infected)",
                    transform.position,
                    GameColorPalette.Poison
                );
            }
        }
        return didAny;
    }

    /// <summary>
    /// Ticks end-of-round status effects and returns true if any visible or
    /// state-changing effect occurred (used to decide whether to pause for UI).
    /// </summary>
    public bool TickStatusesAtRoundEnd()
    {
        bool didAny = false;
        // Fatigued: -1
        if (GetStatus(StatusTag.Fatigue) > 0)
        {
            DecrementStatus(StatusTag.Fatigue, 1);
            didAny = true;
        }
        // SpeedUp: -1
        if (GetStatus(StatusTag.SpeedUp) > 0)
        {
            DecrementStatus(StatusTag.SpeedUp, 1);
            didAny = true;
        }
        // Taunt: -1
        if (GetStatus(StatusTag.Taunt) > 0)
        {
            DecrementStatus(StatusTag.Taunt, 1);
            didAny = true;
        }

        // DamageUp: clear all
        if (GetStatus(StatusTag.DamageUp) > 0)
        {
            ClearStatus(StatusTag.DamageUp);
            didAny = true;
        }

        // Regen: heal equal to stacks, then -1
        int regen = GetStatus(StatusTag.Regen);
        if (regen > 0)
        {
            didAny = true;
            Heal(regen);
            DecrementStatus(StatusTag.Regen, 1);
            FeedbackManager.Instance?.ShowFloatingText(
                $"+{regen} HP",
                transform.position,
                GameColorPalette.Regen
            );
        }

        // Bleeding: damage equal to stacks (does not self-decrement)
        int bleed = GetStatus(StatusTag.Bleed);
        if (bleed > 0)
        {
            didAny = true;
            int applied = ApplyDamage(bleed, null, null, "Bleed");
            if (applied > 0)
            {
                FeedbackManager.Instance?.ShowFloatingText(
                    $"-{applied} HP (Bleed)",
                    transform.position,
                    GameColorPalette.Bleed
                );
            }
        }

        // BodyUp: -1 ; Malnourished: -1
        if (GetStatus(StatusTag.BodyUp) > 0)
        {
            DecrementStatus(StatusTag.BodyUp, 1);
            didAny = true;
        }

        if (GetStatus(StatusTag.Malnourish) > 0)
        {
            DecrementStatus(StatusTag.Malnourish, 1);
            didAny = true;
        }
        // Absorb: clear remaining stacks at end of round
        if (GetStatus(StatusTag.Absorb) > 0)
        {
            ClearStatus(StatusTag.Absorb);
            didAny = true;
        }

        // Suppressed: -1
        if (GetStatus(StatusTag.Suppress) > 0)
        {
            DecrementStatus(StatusTag.Suppress, 1);
            didAny = true;
        }

        // Stunned: -1
        if (GetStatus(StatusTag.Stun) > 0)
        {
            DecrementStatus(StatusTag.Stun, 1);
            didAny = true;
        }

        // NoForage: -1
        if (GetStatus(StatusTag.NoForage) > 0)
        {
            DecrementStatus(StatusTag.NoForage, 1);
            didAny = true;
        }
        return didAny;
    }

    private BoardSlot FindSlotOf(Creature c)
    {
        var slots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
        foreach (var s in slots)
        {
            if (s.currentCreature == c)
                return s;
        }
        return null;
    }

    /// <summary>
    /// Gets the slot index for a creature, or a large value if not on a slot.
    /// Used for deterministic ordering in networked games.
    /// </summary>
    private static int GetSlotIndexForCreature(Creature c, Dictionary<BoardSlot, int> slotIndices)
    {
        foreach (var kvp in slotIndices)
        {
            if (kvp.Key.currentCreature == c)
                return kvp.Value;
        }
        return int.MaxValue;
    }
}
