using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Focused Strike")]
public class FocusedStrikeEffect : EffectTraitBase
{
    public int normalDamage = 3;
    public int buffedDamage = 5;

    public override void OnApply(Creature self)
    {
        if (self == null)
            return;

        int dmg = normalDamage;
        if (GameManager.Instance != null)
        {
            var era = GameManager.Instance.currentEra;
            if (era == Era.Cretaceous || era == Era.Extinction)
                dmg = buffedDamage;
        }

        int applied = self.ApplyDamage(Mathf.Max(0, dmg), null, null, "Focused Strike");
        if (applied > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"-{applied} HP",
                self.transform.position,
                new Color(1f, 0.4f, 0.4f)
            );
        }
        remainingRounds = 0;
    }
}
