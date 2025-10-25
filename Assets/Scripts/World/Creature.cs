using UnityEngine;

public class Creature : MonoBehaviour
{
    public CardData data;
    private SpriteRenderer sr;
    public int body;
    public int speed;
    public int eaten;
    public SlotOwner owner;
    public System.Collections.Generic.List<Trait> traits = new System.Collections.Generic.List<Trait>();
    public int tempSpeedMod;
    public bool defendedThisRound;

    public void Initialize(CardData cardData)
    {
        data = cardData;
        name = $"Creature_{data.cardName}";

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
}
