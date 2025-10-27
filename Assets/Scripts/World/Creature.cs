using UnityEngine;
using System.Collections.Generic;

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
        eaten = 0;

        traits.Clear();
        if (data.baseTraits != null && data.baseTraits.Length > 0)
            traits.AddRange(data.baseTraits);

        tempSpeedMod = 0;
        defendedThisRound = false;
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
}
