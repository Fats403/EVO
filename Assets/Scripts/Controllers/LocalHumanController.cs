using System;
using System.Collections.Generic;
using UnityEngine;

public class LocalHumanController : IPlayerController
{
    public SlotOwner Owner => SlotOwner.Player1;
    public event Action<GameAction> OnActionDecided;

    public void OnTurnStarted()
    {
        // UI is already listening for drags
    }

    public void OnTurnUpdate()
    {
        // No per-frame logic needed for now
    }

    public void RequestPass()
    {
        OnActionDecided?.Invoke(GameAction.CreatePass(Owner));
    }

    public void RequestManualSelectionCancel()
    {
        OnActionDecided?.Invoke(
            new GameAction { type = GameActionType.ManualSelectionCancel, owner = Owner }
        );
    }

    public void RequestPlayCreature(string cardId, int slotIndex)
    {
        OnActionDecided?.Invoke(
            new GameAction
            {
                type = GameActionType.PlayCreature,
                owner = Owner,
                cardId = cardId,
                slotIndex = slotIndex,
            }
        );
    }

    public void RequestPlayEffect(string cardId, List<int> targetSlotIndices)
    {
        OnActionDecided?.Invoke(
            new GameAction
            {
                type = GameActionType.PlayEffect,
                owner = Owner,
                cardId = cardId,
                targetSlotIndices = targetSlotIndices,
            }
        );
    }
}
