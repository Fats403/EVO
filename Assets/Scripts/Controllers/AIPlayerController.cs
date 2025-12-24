using System;
using System.Collections.Generic;
using UnityEngine;

public class AIPlayerController : IPlayerController
{
    public SlotOwner Owner => SlotOwner.Player2;
    public event Action<GameAction> OnActionDecided;

    private AIManager aiManager;
    private float delayTimer = 0f;
    private bool awaitingAction = false;

    public AIPlayerController(AIManager manager)
    {
        aiManager = manager;
    }

    public void OnTurnStarted()
    {
        delayTimer = aiManager != null ? aiManager.actionDelay : 1.0f;
        awaitingAction = true;
    }

    public void OnTurnUpdate()
    {
        if (!awaitingAction)
            return;

        if (delayTimer > 0)
        {
            delayTimer -= Time.deltaTime;
            return;
        }

        awaitingAction = false;

        // Request the AI brain to decide on an action
        if (aiManager != null)
        {
            if (aiManager.TryPlaySingleAction(out GameAction action))
            {
                OnActionDecided?.Invoke(action);
            }
            else
            {
                OnActionDecided?.Invoke(GameAction.CreatePass(Owner));
            }
        }
    }

    public void RequestAIAction()
    {
        // This will be called by GameManager during ExecuteTurn
        // We'll refactor AIManager to provide the action data.
    }
}
