using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;

public class Creature : MonoBehaviour
{
    public CreatureCard data;
    private SpriteRenderer sr;
    public int body;
    public int speed;
    public int eaten;
    public SlotOwner owner;
    public List<Trait> traits = new List<Trait>();
    public int maxHealth;
    public int currentHealth;
    public int fatigueStacks;
    public bool isDying;

    // Unified status stacks (Shielded, Infected, etc.)
    private readonly System.Collections.Generic.Dictionary<StatusTag, int> statuses
        = new System.Collections.Generic.Dictionary<StatusTag, int>();

    public int roundDamageDealt;
    public int roundHealingUndone;
    private HashSet<Creature> damagedTargetsThisRound = new HashSet<Creature>();

    public static event Action<Creature, int> OnAnyCreatureHealed;

    public bool IsWounded => currentHealth < maxHealth;

    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text healthText;
    private int baseBody;
    private int baseSpeed;

    public void Initialize(CreatureCard cardData)
    {
        data = cardData;
        name = $"{data.cardName}";

        sr = GetComponent<SpriteRenderer>();
        if (sr != null && data.artwork != null)
        {
            sr.sprite = data.artwork;
            // subtle tint by type
            if (data.type == CardType.Herbivore) sr.color = new Color(0.9f, 1f, 0.9f);
            else if (data.type == CardType.Carnivore) sr.color = new Color(1f, 0.9f, 0.9f);
            else if (data.type == CardType.Avian) sr.color = new Color(0.9f, 0.95f, 1f);
        }
        body = data.size;
        speed = data.speed;
        baseBody = data.size;
        baseSpeed = data.speed;
        eaten = 0;
        maxHealth = Mathf.Max(1, data != null ? data.maxHealth : 1);
        currentHealth = maxHealth;
        fatigueStacks = 0;
        isDying = false;
        roundDamageDealt = 0;
        roundHealingUndone = 0;
        damagedTargetsThisRound.Clear();

        traits.Clear();
        if (data.baseTraits != null && data.baseTraits.Length > 0)
            traits.AddRange(data.baseTraits);

        EnsureTextReferences();
        RefreshStatsUI();
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
        if (amount <= 0) return;
        if (damagedTargetsThisRound != null && damagedTargetsThisRound.Contains(healed))
        {
            roundHealingUndone += amount;
        }
    }

	public System.Collections.IEnumerator PlayAttackBump(float distance = 0.3f, float duration = 0.2f)
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

	public System.Collections.IEnumerator FlashDamage(float duration = 0.12f)
	{
		if (sr == null) sr = GetComponent<SpriteRenderer>();
		if (sr != null)
		{
			Color original = sr.color;
			sr.color = new Color(1f, 0.3f, 0.3f);
			yield return new WaitForSeconds(duration);
			sr.color = original;
		}
	}

    public void ApplyDamage(int amount, Creature source)
    {
        // Shielded negates the next incoming damage instance per charge
        if (amount > 0 && GetStatus(StatusTag.Shielded) > 0)
        {
            DecrementStatus(StatusTag.Shielded, 1);
            FeedbackManager.Instance?.ShowFloatingText("Shielded", transform.position, Color.cyan);
            return;
        }

        int dmg = Mathf.Max(0, amount);
        if (dmg == 0) return;
        currentHealth = Mathf.Max(0, currentHealth - dmg);
        if (source != null)
        {
            source.roundDamageDealt += dmg;
            if (!source.damagedTargetsThisRound.Contains(this)) source.damagedTargetsThisRound.Add(this);
        }
        StartCoroutine(FlashDamage(0.12f));
        // Trait hooks
        if (source != null && source.traits != null)
        {
            foreach (var tr in source.traits) { if (tr != null) tr.OnDamageDealt(source, this, dmg); }
        }
        if (traits != null)
        {
            foreach (var tr in traits) { if (tr != null) tr.OnDamageTaken(this, source, dmg); }
        }
        RefreshStatsUI();
        if (currentHealth == 0)
        {
            Kill("Damage");
        }
    }

    public void Heal(int amount)
    {
        int prev = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        int healed = Mathf.Max(0, currentHealth - prev);
        if (healed > 0)
        {
            // Any healing clears all Bleeding stacks
            if (GetStatus(StatusTag.Bleeding) > 0) ClearStatus(StatusTag.Bleeding);
            OnAnyCreatureHealed?.Invoke(this, healed);
            RefreshStatsUI();
        }
    }

    public void Kill(string reason)
    {
        if (isDying) return;
        isDying = true;
        var s = FindSlotOf(this);
        if (s != null) s.Vacate();
        StartCoroutine(FadeAndDestroy(0.5f));
    }


    private System.Collections.IEnumerator FadeAndDestroy(float duration)
    {
        float t = 0f;
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        var texts = GetComponentsInChildren<TMP_Text>(true);
        // capture original colors
        var srColors = renderers.Select(r => r != null ? r.color : Color.white).ToArray();
        var txtColors = texts.Select(txt => txt != null ? txt.color : Color.white).ToArray();
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float a = 1f - u;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    var c = srColors[i]; c.a = a; renderers[i].color = c;
                }
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    var c = txtColors[i]; c.a = a; texts[i].color = c;
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
        if (damagedTargetsThisRound != null) damagedTargetsThisRound.Clear();
    }

    public void RefreshStatsUI()
    {
        // Speed display with bonuses and temp mods
        if (speedText != null)
        {
            int traitSpeed = (!HasStatus(StatusTag.Suppressed) && traits != null)
                ? traits.Sum(t => t != null ? t.SpeedBonus(this) : 0)
                : 0;
            int displaySpeed = speed - GetStatus(StatusTag.Fatigued) + traitSpeed;
            speedText.text = displaySpeed.ToString();
            if (displaySpeed > baseSpeed) speedText.color = Color.green;
            else if (displaySpeed < baseSpeed) speedText.color = Color.red;
            else speedText.color = Color.white;
        }

        // Body display relative to base body
        if (bodyText != null)
        {
            bodyText.text = body.ToString();
            if (body > baseBody) bodyText.color = Color.green;
            else if (body < baseBody) bodyText.color = Color.red;
            else bodyText.color = Color.white;
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}";
            if (IsWounded) healthText.color = new Color(0.8f, 0.1f, 0.1f);
            else healthText.color = Color.white;
        }

        var sic = GetComponentInChildren<StatusIconController>(true);
        if (sic != null) sic.Refresh(this);
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
        if (stacks <= 0) return;
        // Immune blocks negative statuses, consuming one charge
        if (IsNegativeStatus(tag) && GetStatus(StatusTag.Immune) > 0)
        {
            DecrementStatus(StatusTag.Immune, 1);
            FeedbackManager.Instance?.ShowFloatingText("Immune", transform.position, Color.cyan);
            return;
        }
        int newValue = GetStatus(tag) + stacks;
        // Stealth is non-stacking: clamp to 1
        if (tag == StatusTag.Stealth) newValue = newValue > 0 ? 1 : 0;
        statuses[tag] = newValue;
        RefreshStatsUI();
    }

    public void DecrementStatus(StatusTag tag, int amount = 1)
    {
        if (amount <= 0) return;
        int v = GetStatus(tag) - amount;
        if (v <= 0) statuses.Remove(tag);
        else statuses[tag] = v;
        RefreshStatsUI();
    }

    public void ClearStatus(StatusTag tag)
    {
        if (statuses.Remove(tag)) RefreshStatsUI();
    }

    public System.Collections.Generic.IEnumerable<StatusTag> GetActiveStatusTags()
    {
        foreach (var kv in statuses)
        {
            if (kv.Value > 0) yield return kv.Key;
        }
    }

    private static bool IsNegativeStatus(StatusTag tag)
    {
        // Consider these negative; adjust as desired
        return tag switch
        {
            StatusTag.Infected or StatusTag.Fatigued or StatusTag.Stunned or StatusTag.Suppressed or StatusTag.NoForage or StatusTag.Bleeding => true,
            _ => false,
        };
    }

    // Convenience
    public void ApplyInfected(int stacks) => AddStatus(StatusTag.Infected, stacks);
    public void ApplyShield(int charges) => AddStatus(StatusTag.Shielded, charges);
    public void ApplyBleeding(int stacks) => AddStatus(StatusTag.Bleeding, stacks);
    public void ApplyRegen(int stacks) => AddStatus(StatusTag.Regen, stacks);
    public void ApplyRage() => AddStatus(StatusTag.Rage, 1);
    public void ApplyStunned(int rounds = 1) => AddStatus(StatusTag.Stunned, rounds);
    public void ApplySuppressed(int rounds) => AddStatus(StatusTag.Suppressed, rounds);
    public void ApplyDamageUp(int stacks) => AddStatus(StatusTag.DamageUp, stacks);
    public void ApplyNoForage(int rounds) => AddStatus(StatusTag.NoForage, rounds);
    public void ApplyImmune(int charges = 1) => AddStatus(StatusTag.Immune, charges);
    public void ApplyFatigued(int stacks) => AddStatus(StatusTag.Fatigued, stacks);

    public void TickStatusesAtRoundStart()
    {
        // Infected: deal 1, then -1 stack
        if (GetStatus(StatusTag.Infected) > 0)
        {
            ApplyDamage(1, null);
            DecrementStatus(StatusTag.Infected, 1);
        }
    }

    public void TickStatusesAtRoundEnd()
    {
        // Fatigued: -1
        if (GetStatus(StatusTag.Fatigued) > 0) DecrementStatus(StatusTag.Fatigued, 1);

        // DamageUp: clear all
        if (GetStatus(StatusTag.DamageUp) > 0) ClearStatus(StatusTag.DamageUp);

        // Regen: heal equal to stacks, then -1
        int regen = GetStatus(StatusTag.Regen);
        if (regen > 0)
        {
            Heal(regen);
            DecrementStatus(StatusTag.Regen, 1);
        }

        // Bleeding: damage equal to stacks (does not self-decrement)
        int bleed = GetStatus(StatusTag.Bleeding);
        if (bleed > 0)
        {
            ApplyDamage(bleed, null);
        }

        // Suppressed: -1
        if (GetStatus(StatusTag.Suppressed) > 0) DecrementStatus(StatusTag.Suppressed, 1);

        // Stunned: -1
        if (GetStatus(StatusTag.Stunned) > 0) DecrementStatus(StatusTag.Stunned, 1);

        // NoForage: -1
        if (GetStatus(StatusTag.NoForage) > 0) DecrementStatus(StatusTag.NoForage, 1);
    }

    private void EnsureTextReferences()
    {
        if (speedText != null && bodyText != null) return;
        var texts = GetComponentsInChildren<TMP_Text>(true);
        if (speedText == null)
        {
            speedText = texts.FirstOrDefault(t => t != null && (t.name == "SpeedText" || t.name.Contains("Speed")));
        }
        if (bodyText == null)
        {
            bodyText = texts.FirstOrDefault(t => t != null && (t.name == "BodyText" || t.name == "BodySizeText" || t.name.Contains("Body") || t.name.Contains("Size")));
        }
        if (healthText == null)
        {
            healthText = texts.FirstOrDefault(t => t != null && (t.name == "HealthText" || t.name.Contains("HP") || t.name.Contains("Health")));
        }
        if (speedText == null || bodyText == null)
        {
            Debug.LogWarning($"[Creature] Could not auto-find stat texts on {name}. Assign them in the prefab.");
        }
    }

    private BoardSlot FindSlotOf(Creature c)
    {
        var slots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
        foreach (var s in slots)
        {
            if (s.currentCreature == c) return s;
        }
        return null;
    }
}
