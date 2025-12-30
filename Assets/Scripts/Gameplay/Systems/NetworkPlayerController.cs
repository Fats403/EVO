using System;

/// <summary>
/// IPlayerController implementation for a remote networked player.
/// Actions are received from the network and queued for processing by GameManager.
/// </summary>
public class NetworkPlayerController : IPlayerController
{
    public SlotOwner Owner { get; }
    public event Action<GameAction> OnActionDecided;

    private GameAction _pendingAction;

    public NetworkPlayerController(SlotOwner owner)
    {
        Owner = owner;
    }

    /// <summary>
    /// Called by NetworkMatchManager when an action is received from the remote peer.
    /// </summary>
    public void EnqueueRemoteAction(GameAction action)
    {
        if (action != null && action.owner == Owner)
        {
            _pendingAction = action;
        }
    }

    public void OnTurnStarted()
    {
        // No-op: remote player handles their own turn timing
    }

    public void OnTurnUpdate()
    {
        if (_pendingAction != null)
        {
            OnActionDecided?.Invoke(_pendingAction);
            _pendingAction = null;
        }
    }
}
