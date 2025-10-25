using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Ambush")]
public class AmbushTrait : Trait
{
	public override int SpeedBonus(Creature self) => 1;
}


