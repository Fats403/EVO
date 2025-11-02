using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;

public class Creature : MonoBehaviour
{
    public CardData data;
    private SpriteRenderer sr;
    public int body;
    public int speed;
    public int eaten;
    public SlotOwner owner;
    public List<Trait> traits = new List<Trait>();
    public int tempSpeedMod;
    public bool defendedThisRound;
    public int maxHealth;
    public int currentHealth;
    public bool fatigued;

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

    public void Initialize(CardData cardData)
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
        fatigued = false;
        roundDamageDealt = 0;
        roundHealingUndone = 0;
        damagedTargetsThisRound.Clear();

        traits.Clear();
        if (data.baseTraits != null && data.baseTraits.Length > 0)
            traits.AddRange(data.baseTraits);

        tempSpeedMod = 0;
        defendedThisRound = false;

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
        int dmg = Mathf.Max(0, amount);
        if (dmg == 0) return;
        currentHealth = Mathf.Max(0, currentHealth - dmg);
        if (source != null)
        {
            source.roundDamageDealt += dmg;
            if (!source.damagedTargetsThisRound.Contains(this)) source.damagedTargetsThisRound.Add(this);
        }
        StartCoroutine(FlashDamage(0.12f));
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
            OnAnyCreatureHealed?.Invoke(this, healed);
            RefreshStatsUI();
        }
    }

    public void Kill(string reason)
    {
        var s = FindSlotOf(this);
        if (s != null) s.Vacate();
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
            int traitSpeed = (traits != null) ? traits.Sum(t => t != null ? t.SpeedBonus(this) : 0) : 0;
            int displaySpeed = speed + tempSpeedMod + traitSpeed;
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
