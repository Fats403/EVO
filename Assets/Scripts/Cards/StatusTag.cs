
public enum StatusTag
{
    Infected,    // Stacks infected, deals 1 damage each round, clears one stack at end of round
    Shielded,    // Stacks shielded, negates next damage, clears one stack at end of round
    Reflect,     // Stacks reflect, reflects next damage, clears one stack at end of round
    Fatigued,    // Stacks fatigue, reduces speed by number of stacks each round, clears one stack at end of round
    Starvation,  // Stacks starvation, deals 1 damage per stack of starvation, clears one stack at end of round, if above 3 starvation instant death
    Taunt,       // Stacks taunt, forces targeting of this creature, clears one stack at end of round
    Rage,        // Does not Stack, doubles next damage, clears once attack is made
    Stunned,     // Does not Stack, creatue cannot make any actions, clears at end of round
    Suppressed,  // Stacks suppressed, clears one stack at end of round
    DamageUp,    // Stacks damage up, adds 1 damage to all attacks, clears one stack at end of round
    NoForage,    // Does not stack, prevents foraging, clears at end of round
    Immune,      // Stacks immune, negates next damage, clears one stack at end of round
    Regen,       // Stacks regen, heals number ofstacks of health each round, clears one stack at end of round
    Bleeding,    // Stacks bleeding, deals 1 damage each round, clears one stack at end of round
    Stealth,     // Does not stack, prevents targeting, clears at end of round
    Absorb,      // Stacks absorb, absorbs damage up to number of stacks, clears all stacks at end of round
    BodyUp,      // Does not stack, adds number of stacks to body, clears one stack at end of round
    SpeedUp,     // Does not stack, adds number of stacks to speed, clears one stack at end of round
    Malnourished,// Does not stack, reduces body by number of stacks each round, clears one stack at end of round
}