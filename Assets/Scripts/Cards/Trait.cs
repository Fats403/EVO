using UnityEngine;

public abstract class Trait : ScriptableObject
{
    [Header("Trait")]
    public string traitName;

    [TextArea]
    public string description;

    public virtual int SpeedBonus(Creature self)
    {
        return 0;
    }

    public virtual int BodyBonus(Creature self)
    {
        return 0;
    }

    public virtual int ModifyHerbivoreEatAmount(Creature self, int baseAmount, FoodPile pile)
    {
        return baseAmount;
    }

    public virtual void OnAfterKill(Creature self, Creature target) { }

    public virtual void OnRoundEnd(Creature self) { }

    public virtual void OnRoundStart(Creature self) { }

    public virtual int HerbivorePriorityBonus(Creature self)
    {
        return 0;
    }

    /// <summary>
    /// Optional bonus applied to a creature's position in the mixed action-phase order.
    /// Higher values act earlier (before speed tie-breaks). Use this for "always acts first"
    /// style traits without requiring special resolve sub-phases.
    /// </summary>
    public virtual int ActionPriorityBonus(Creature self)
    {
        return 0;
    }

    public virtual bool TryNegateAttack(Creature self, Creature attacker)
    {
        return false;
    }

    public virtual void OnAnyDeath(Creature self, Creature dead) { }

    // New combat/health hooks
    public virtual int ModifyOutgoingDamage(Creature self, Creature target, int baseDamage)
    {
        return baseDamage;
    }

    public virtual int ModifyIncomingDamage(Creature self, Creature attacker, int baseDamage)
    {
        return baseDamage;
    }

    public virtual void OnDamageDealt(Creature self, Creature target, int finalDamage) { }

    public virtual void OnDamageTaken(Creature self, Creature attacker, int finalDamage) { }

    public virtual void OnWoundedRoundTick(Creature self) { }

    public virtual bool CanAttack(Creature self)
    {
        return self != null && self.data != null && self.data.type != CardType.Herbivore;
    }

    public virtual bool CanTarget(Creature self, Creature target)
    {
        return true;
    }

    public virtual bool CanForage(Creature self)
    {
        return true;
    }

    /// <summary>
    /// Allows a herbivore to make a bonus attack immediately after successfully foraging
    /// during its action. Intended for special effects like "Stampede Formation" and
    /// should default to false to avoid making all attack-enabled herbivores act twice.
    /// </summary>
    public virtual bool AllowBonusAttackAfterForage(Creature self)
    {
        return false;
    }

    // Targeting overrides
    public virtual bool CanTargetAny(Creature self)
    {
        return false;
    }

    public virtual bool IgnoreAvianSpeedRequirement(Creature self, Creature target)
    {
        return false;
    }

    // Allow traits to ignore the normal body-size targeting rule (attacker may
    // attack only same-body-or-smaller prey). If this returns true for an
    // attacker/target pair, that attack ignores the body gate but still respects
    // stealth, taunt, and avian speed rules.
    public virtual bool IgnoreBodySizeRequirement(Creature self, Creature target)
    {
        return false;
    }

    // Damage override (e.g., fixed damage). If returns true, use fixedDamage and skip other modifiers.
    public virtual bool TryOverrideFinalDamage(Creature self, Creature target, out int fixedDamage)
    {
        fixedDamage = 0;
        return false;
    }

    // Fires after an attack attempt resolves (negated or applied)
    public virtual void OnAfterAttackResolved(Creature self, Creature target, bool wasNegated) { }

    // Allow attacker traits to choose a target from valid candidates
    public virtual Creature ChooseAttackTarget(
        Creature self,
        System.Collections.Generic.IEnumerable<Creature> candidates,
        Creature defaultTarget
    )
    {
        return defaultTarget;
    }

    // Eating and targeting reaction hooks
    public virtual void OnAfterEat(Creature self, int amountTaken, FoodPile pile) { }

    // Fires when this creature becomes the target of an attack (before negate/damage)
    public virtual void OnTargetedByAttack(Creature self, Creature attacker) { }

    // Fires on allies when some ally is targeted by an attack
    public virtual void OnAllyTargeted(Creature self, Creature ally, Creature attacker) { }

    // Global notification after any damage is finalized and applied
    public virtual void OnAnyDamage(
        Creature self,
        Creature victim,
        Creature attacker,
        int finalDamage
    ) { }

    // Weather penalty hook: traits can opt a creature out of negative weather effects
    // (e.g., storm fatigue, wildfire damage) for the current weather.
    public virtual bool NegateWeatherPenalty(Creature self, WeatherType weather)
    {
        return false;
    }

    // Weather change hook: fired whenever the global weather changes (including
    // via cards that force weather). Use this for traits whose stat bonuses
    // should appear/disappear immediately with weather, instead of relying only
    // on round-start hooks.
    public virtual void OnWeatherChanged(
        Creature self,
        WeatherType newWeather,
        WeatherType? lastWeather
    ) { }
}
