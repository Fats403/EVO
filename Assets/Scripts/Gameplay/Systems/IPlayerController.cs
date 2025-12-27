using System;
using System.Collections;

public interface IPlayerController
{
    SlotOwner Owner { get; }

    // Called by GameManager when the turn starts for this owner
    void OnTurnStarted();

    // Called by GameManager every frame while it's this player's turn
    void OnTurnUpdate();

    // Event for when the controller has decided on an action
    event Action<GameAction> OnActionDecided;
}
