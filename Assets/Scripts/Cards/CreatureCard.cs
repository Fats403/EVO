using UnityEngine;

[CreateAssetMenu(fileName = "NewCreatureCard", menuName = "Cards/Creature Card")]
public class CreatureCard : ScriptableObject
{
    [Header("Identity")]
    public string cardName;
    public string dinosaurName;

    [Header("Visuals")]
    public Sprite artwork;
    public CardType type; // Herbivore, Carnivore, Avian
    public Sprite background; // Optional custom background for the card type

    [Header("Core Stats")]
    public int size = 1; // Affects food requirement and strength
    public int speed = 1; // Feeding order priority

    [Header("Vitals")]
    public int maxHealth = 3;

    [Header("Cost & Conditions")]
    [Tooltip("Momentum cost to play this creature card")]
    [Min(1)]
    public int momentumCost = 1;

    [Header("Base Traits")]
    public Trait[] baseTraits;
}

public enum CardType
{
    Herbivore,
    Carnivore,
    Avian,
}
