using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Desperate Hunter")]
public class DesperateHunterTrait : Trait
{
    public override int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        if (self == null || target == null)
            return baseDamage;
        if (self.HasStatus(StatusTag.Suppressed))
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

    public override void OnRoundStart(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return;
        if (wm.CurrentWeather != WeatherType.Drought)
            return;

        self.AddStatus(StatusTag.BodyUp, 1);
    }
}

