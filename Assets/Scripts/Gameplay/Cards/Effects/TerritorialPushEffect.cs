using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Territorial Push")]
public class TerritorialPushEffect : EffectTraitBase
{
    public float moveDuration = 0.45f;

    public override void OnApply(Creature target)
    {
        if (target == null)
            return;

        // Always stun the target this round.
        target.AddStatus(StatusTag.Stun, 1);
        FeedbackManager.Instance?.ShowFloatingText(
            "Territorial Push",
            target.transform.position,
            GameColorPalette.TextWarning
        );
        FeedbackManager.Instance?.ShowFloatingText(
            "Stunned",
            target.transform.position,
            GameColorPalette.TextWarning
        );

        // Try to shove the target into a random empty slot on its side.
        var fromSlot = BoardUtils.GetSlotOf(target);
        if (fromSlot != null)
        {
            var toSlot = BoardUtils.GetRandomEmptySlot(fromSlot.owner);
            if (toSlot != null && ResolutionManager.Instance != null)
            {
                ResolutionManager.Instance.StartCoroutine(
                    BoardMovement.MoveCreatureToSlot(target, fromSlot, toSlot, moveDuration)
                );
            }
            else
            {
                // No empty space to move into: also Suppress the target.
                target.AddStatus(StatusTag.Suppress, 1);
                FeedbackManager.Instance?.ShowFloatingText(
                    "Suppressed",
                    target.transform.position,
                    GameColorPalette.TextWarning
                );
            }
        }

        remainingRounds = 0;
    }
}
