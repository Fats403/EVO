using UnityEngine;

[CreateAssetMenu(fileName = "NewCreatureCard", menuName = "Cards/Creature Card")]
public class CreatureCard : ScriptableObject
{
    [Header("Identity")]
    public string cardName;

    [Header("Visuals")]
    public Sprite artwork;
    public CardType type;        // Herbivore, Carnivore, Avian
    public Sprite background;    // Optional custom background for the card type

    [Header("Core Stats")]
    public int size = 1;  // Affects food requirement and strength
    public int speed = 1; // Feeding order priority
    [Range(1, 3)] public int tier = 1;  // Evolution stage
    
    [Header("Vitals")]
    public int maxHealth = 3;
    
    [Header("Base Traits")]
    public Trait[] baseTraits;
}

public enum CardType
{
    Herbivore,
    Carnivore,
    Avian
}