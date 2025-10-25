using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Demoralize")]
public class DemoralizeTrait : Trait
{
	public override void OnAfterKill(Creature self, Creature target)
	{
		var enemies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(c => c != null && c.owner != self.owner && c.data != null && c.data.type == CardType.Herbivore)
			.OrderBy(c => Vector3.SqrMagnitude(c.transform.position - self.transform.position))
			.ToList();
		if (enemies.Count > 0)
		{
			enemies[0].tempSpeedMod -= 1;
		}
	}
}


