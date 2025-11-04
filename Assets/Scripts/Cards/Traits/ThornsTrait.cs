using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Thorns")]
public class ThornsTrait : Trait
{
    [Range(1, 3)] public int reflectDamage = 1;

    public override void OnDamageTaken(Creature self, Creature attacker, int finalDamage)
    {
        if (attacker == null) return;
        if (finalDamage <= 0) return;
        // Apply reflected damage without scoring/loops (source null)
        attacker.ApplyDamage(reflectDamage, null);
        FeedbackManager.Instance?.ShowFloatingText($"-{reflectDamage} Thorns", attacker.transform.position, new Color(1f, 0.5f, 0.2f));
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(description))
        {
            description = "When hit, attacker takes 1 damage.";
        }
    }
}
