using System.Collections.Generic;

public enum GameActionType
{
    Pass,
    PlayCreature,
    PlayEffect,
}

[System.Serializable]
public class GameAction
{
    public GameActionType type;
    public SlotOwner owner;

    // For PlayCreature/Effect
    public string cardId;
    public int slotIndex = -1; // Index of BoardSlot

    // For Effects/ManualSelection
    public List<int> targetSlotIndices = new(); // Indices of targeted Creature slots

    public static GameAction CreatePass(SlotOwner owner) =>
        new() { type = GameActionType.Pass, owner = owner };
}
