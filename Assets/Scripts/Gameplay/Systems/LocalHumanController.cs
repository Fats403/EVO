using System;
using System.Collections.Generic;

/// <summary>
/// IPlayerController implementation for the local human player.
/// Handles input from UI (card drags, buttons) and broadcasts actions
/// both locally and over the network in multiplayer mode.
/// </summary>
public class LocalHumanController : IPlayerController
{
    /// <summary>
    /// Returns the SlotOwner for the local player, which varies based on game mode.
    /// </summary>
    public SlotOwner Owner => NetworkRoleHelper.LocalRole;

    public event Action<GameAction> OnActionDecided;

    public void OnTurnStarted()
    {
        // UI is already listening for drags
    }

    public void OnTurnUpdate()
    {
        // No per-frame logic needed
    }

    public void RequestPass()
    {
        var action = GameAction.CreatePass(Owner);
        BroadcastAction(action);
    }

    public void RequestPlayCreature(string cardId, int slotIndex)
    {
        var action = new GameAction
        {
            type = GameActionType.PlayCreature,
            owner = Owner,
            cardId = cardId,
            slotIndex = slotIndex,
        };
        BroadcastAction(action);
    }

    public void RequestPlayEffect(string cardId, List<int> targetSlotIndices, string choicePayload = null)
    {
        var action = new GameAction
        {
            type = GameActionType.PlayEffect,
            owner = Owner,
            cardId = cardId,
            targetSlotIndices = targetSlotIndices,
            choicePayload = choicePayload,
        };
        BroadcastAction(action);
    }

    /// <summary>
    /// Broadcasts an action to both the local GameManager and the network (if in networked mode).
    /// </summary>
    private void BroadcastAction(GameAction action)
    {
        // Notify local GameManager
        OnActionDecided?.Invoke(action);

        // Send to remote peer if in networked mode
        if (NetworkSessionStore.IsNetworkedGame)
        {
            // Only the guest needs to mirror slot indices. The host's perspective is
            // canonical, so guest actions are transformed to host coordinates for
            // transmission. The host sends actions unmodified.
            var networkAction = NetworkRoleHelper.IsGuest ? CreateMirroredAction(action) : action;
            NetworkMatchManager.Instance?.SendInputAction(networkAction);
        }
    }

    /// <summary>
    /// Creates a copy of the action with slot indices mirrored for network transmission.
    /// Used by the guest to convert local slot indices to the host's canonical view.
    /// </summary>
    private GameAction CreateMirroredAction(GameAction original)
    {
        var mirrored = new GameAction
        {
            type = original.type,
            owner = original.owner,
            cardId = original.cardId,
            slotIndex = NetworkRoleHelper.MirrorSlotIndex(original.slotIndex),
            targetSlotIndices = NetworkRoleHelper.MirrorSlotIndices(original.targetSlotIndices),
            choicePayload = original.choicePayload,
        };
        return mirrored;
    }
}
