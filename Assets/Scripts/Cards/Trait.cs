using UnityEngine;

public abstract class Trait : ScriptableObject
{
	[Header("Trait")]
	public string traitName;
    [TextArea]
    public string description;

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

    // New combat/health hooks
    public virtual int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage) { return baseDamage; }
    public virtual int ModifyIncomingDamage(Creature self, Creature attacker, int baseDamage) { return baseDamage; }
    public virtual void OnDamageDealt(Creature self, Creature target, int finalDamage) {}
    public virtual void OnDamageTaken(Creature self, Creature attacker, int finalDamage) {}
    public virtual void OnWoundedRoundTick(Creature self) {}
    public virtual bool CanAttack(Creature self) { return self != null && self.data != null && self.data.type != CardType.Herbivore; }
    public virtual bool CanTarget(Creature self, Creature target) { return true; }
    public virtual bool CanForage(Creature self) { return true; }
}


