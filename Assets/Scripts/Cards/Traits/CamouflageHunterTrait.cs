using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Carnivores/Camouflage Hunter")]
public class CamouflageHunterTrait : Trait
{
	public override void OnRoundStart(Creature self)
	{
		if (self == null) return;
		if (self.HasStatus(StatusTag.Suppressed)) return;
		self.AddStatus(StatusTag.Stealth, 1);
		FeedbackManager.Instance?.ShowFloatingText("Stealth", self.transform.position, Color.gray);
	}
}
