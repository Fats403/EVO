using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

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

    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text bodyText;
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

        traits.Clear();
        if (data.baseTraits != null && data.baseTraits.Length > 0)
            traits.AddRange(data.baseTraits);

        tempSpeedMod = 0;
        defendedThisRound = false;

        EnsureTextReferences();
        RefreshStatsUI();
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
            else speedText.color = Color.black;
        }

        // Body display relative to base body
        if (bodyText != null)
        {
            bodyText.text = body.ToString();
            if (body > baseBody) bodyText.color = Color.green;
            else if (body < baseBody) bodyText.color = Color.red;
            else bodyText.color = Color.black;
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
        if (speedText == null || bodyText == null)
        {
            Debug.LogWarning($"[Creature] Could not auto-find stat texts on {name}. Assign them in the prefab.");
        }
    }
}
