using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Windfall")]
public class WindfallTrait : Trait
{
    [Range(1,2)] public int stealAmount = 1;

    public override int PreHerbivorePileSteal(Creature self, FoodPile pile)
    {
        if (pile == null) return 0;
        if (self.data == null || self.data.type != CardType.Herbivore) return 0;
        if (self.eaten >= self.body) return 0;
        if (self.HasStatus(StatusTag.Suppressed)) return 0;
        return stealAmount;
    }
}


