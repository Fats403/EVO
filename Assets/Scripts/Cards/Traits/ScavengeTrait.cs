using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Scavenge")]
public class ScavengeTrait : Trait
{
	public override void OnAnyDeath(Creature self, Creature dead)
	{
		if (self.data != null && self.data.type == CardType.Avian)
		{
			if (self.eaten < self.body)
				self.eaten += 1;
		}
	}

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(description))
        {
            description = "When any creature dies, gain +1 eaten (if not full).";
        }
    }
}


