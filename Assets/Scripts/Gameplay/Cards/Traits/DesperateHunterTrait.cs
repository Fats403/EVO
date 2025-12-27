using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Desperate Hunter")]
public class DesperateHunterTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppress))
            return baseDamage;

        FoodPile pile = null;
        if (ResolutionManager.Instance != null)
            pile = ResolutionManager.Instance.foodPile;
        if (pile == null && GameManager.Instance != null)
            pile = GameManager.Instance.foodPile;

        if (pile != null && pile.count <= 1)
        {
            baseDamage += 2;
        }
        return Mathf.Max(0, baseDamage);
    }

    public override int BodyBonus(Creature self)
    {
        if (self == null)
            return 0;
        if (self.HasStatus(StatusTag.Suppress))
            return 0;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return 0;

        return wm.CurrentWeather == WeatherType.Drought ? 1 : 0;
    }
}
