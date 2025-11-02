using UnityEngine;

[CreateAssetMenu(fileName = "NewCreatureCard", menuName = "Cards/Creature Card")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string cardName;
    [TextArea] public string description;

    [Header("Visuals")]
    public Sprite artwork;
    public CardType type;        // Herbivore, Carnivore, Avian
    public Sprite background;    // Optional custom background for the card type

    [Header("Core Stats")]
    [Range(1, 6)] public int size = 1;  // Affects food requirement and strength
    [Range(1, 6)] public int speed = 1; // Feeding order priority
    [Range(1, 3)] public int tier = 1;  // Evolution stage
    
    [Header("Vitals")]
    [Range(1, 10)] public int maxHealth = 3;
    
    [Header("Base Traits")]
    public Trait[] baseTraits;
}

public enum CardType
{
    Herbivore,
    Carnivore,
    Avian
}