using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Avians/Relentless")]
public class RelentlessTrait : Trait
{
    public override bool IgnoreAvianSpeedRequirement(Creature self, Creature target)
    {
        if (self == null)
            return false;
        if (self.HasStatus(StatusTag.Suppress))
            return false;
        return true;
    }
}
