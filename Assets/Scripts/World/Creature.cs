using UnityEngine;

public class Creature : MonoBehaviour
{
    public CardData data;
    private SpriteRenderer sr;
    [SerializeField] private float fixedScale = 0.1f;

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
    }
}
