using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Traits/Herbivores/Maternal Care")]
public class MaternalCareTrait : Trait
{
    public override void OnRoundEnd(Creature self)
    {
        if (self == null)
            return;
        if (self.HasStatus(StatusTag.Suppressed))
            return;

        var allies = Object
            .FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c =>
                c != null && c != self && c.currentHealth > 0 && !c.isDying && c.owner == self.owner
            )
            .ToList();
        if (allies.Count == 0)
            return;

        var target = allies
            .OrderBy(c => c.currentHealth)
            .ThenBy(_ =>
                GameManager.Instance != null
                    ? GameManager.Instance.NextRandomInt(0, allies.Count)
                    : Random.Range(0, allies.Count)
            )
            .FirstOrDefault();
        if (target == null)
            return;

        int before = target.currentHealth;
        target.Heal(2);
        int healed = target.currentHealth - before;
        if (healed > 0)
        {
            FeedbackManager.Instance?.ShowFloatingText(
                $"+{healed} HP",
                target.transform.position,
                GameColorPalette.Heal
            );
        }
    }
}
