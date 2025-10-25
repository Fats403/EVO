using UnityEngine;

public abstract class Trait : ScriptableObject
{
	[Header("Trait")]
	public string traitName;
	public Sprite icon;

	public virtual int SpeedBonus(Creature self) { return 0; }
	public virtual bool CanTargetEqualBody(Creature self, Creature target) { return false; }
	public virtual int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile) { return baseAmount; }
	public virtual void OnAfterKill(Creature self, Creature target) {}
	public virtual void OnAfterCarnivorePhase(Creature self, FoodPile pile) {}
	public virtual void OnRoundEnd(Creature self) {}
	public virtual void OnRoundStart(Creature self) {}
	public virtual int HerbivorePriorityBonus(Creature self) { return 0; }
	public virtual int PreHerbivorePileSteal(Creature self, FoodPile pile) { return 0; }
	public virtual bool TryNegateAttack(Creature self, Creature attacker) { return false; }
	public virtual int PredatorBodyBonusForTargeting(Creature self) { return 0; }
	public virtual void OnAnyDeath(Creature self, Creature dead) {}
}


