using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Traits/Ambush")]
public class AmbushTrait : Trait
{
	public override int SpeedBonus(Creature self) => 1;

    public override void OnRoundStart(Creature self)
    {
        // Grant 1 DamageUp for the first attack
        if (self != null) self.ApplyDamageUp(1);
    }
}


