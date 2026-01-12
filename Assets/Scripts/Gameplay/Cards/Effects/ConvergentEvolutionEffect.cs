using UnityEngine;

/// <summary>
/// Convergent Evolution: Target ally gains different permanent benefits based on creature type:
/// - Carnivore: Can attack any target (ignores body size restrictions)
/// - Herbivore: Can attack like a carnivore
/// - Avian: +2 Body permanently
/// </summary>
[CreateAssetMenu(menuName = "Effects/Convergent Evolution")]
public class ConvergentEvolutionEffect : EffectTraitBase
{
    // Track which type was buffed so we know which bonuses to apply
    private CardType buffedType = CardType.Herbivore;

    public override void OnApply(Creature self)
    {
        if (self == null || self.data == null)
            return;

        buffedType = self.data.type;

        switch (buffedType)
        {
            case CardType.Carnivore:
                // Carnivore gains "can attack any target" - handled via IgnoreBodySizeRequirement
                FeedbackManager.Instance?.ShowFloatingText(
                    "Evolved: Apex Hunter",
                    self.transform.position,
                    GameColorPalette.TextPositive
                );
                FeedbackManager.Instance?.Log(
                    $"{FeedbackManager.TagOwner(self.owner)} {self.name} evolved: can attack any target"
                );
                break;

            case CardType.Herbivore:
                // Herbivore gains "can attack" - handled via CanAttack override
                FeedbackManager.Instance?.ShowFloatingText(
                    "Evolved: Aggressive",
                    self.transform.position,
                    GameColorPalette.TextPositive
                );
                FeedbackManager.Instance?.Log(
                    $"{FeedbackManager.TagOwner(self.owner)} {self.name} evolved: can now attack"
                );
                break;

            case CardType.Avian:
                // Avian gains +2 Body permanently (direct stat modification)
                self.body += 2;
                self.RefreshStatsUI();
                FeedbackManager.Instance?.ShowFloatingText(
                    "Evolved: +2 Body",
                    self.transform.position,
                    GameColorPalette.TextPositive
                );
                FeedbackManager.Instance?.Log(
                    $"{FeedbackManager.TagOwner(self.owner)} {self.name} evolved: +2 Body permanently"
                );
                break;
        }

        // This effect is permanent - set to very high value so it doesn't expire
        remainingRounds = 9999;
    }

    /// <summary>
    /// Carnivore bonus: Can attack any target regardless of body size.
    /// Applied effects are not affected by Suppress.
    /// </summary>
    public override bool IgnoreBodySizeRequirement(Creature self, Creature target)
    {
        if (self == null || self.data == null)
            return false;
        if (buffedType != CardType.Carnivore)
            return false;

        return true;
    }

    /// <summary>
    /// Carnivore bonus: Can attack Avians regardless of speed requirements.
    /// Applied effects are not affected by Suppress.
    /// </summary>
    public override bool IgnoreAvianSpeedRequirement(Creature self, Creature target)
    {
        if (self == null || self.data == null)
            return false;
        if (buffedType != CardType.Carnivore)
            return false;

        return true;
    }

    /// <summary>
    /// Herbivore bonus: Can attack like a carnivore.
    /// Applied effects are not affected by Suppress.
    /// </summary>
    public override bool CanAttack(Creature self)
    {
        if (self == null || self.data == null)
            return base.CanAttack(self);
        if (buffedType != CardType.Herbivore)
            return base.CanAttack(self);

        return true;
    }
}
