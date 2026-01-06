using System.Collections.Generic;

public enum GameActionType
{
    Invalid = 0,  // Default value - should never be processed
    Pass = 1,
    PlayCreature = 2,
    PlayEffect = 3,
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

    // For effects with pre-play choices (e.g., "Fight or Flight")
    // Contains the optionId of the chosen VirtualChoiceOption
    public string choicePayload;

    public static GameAction CreatePass(SlotOwner owner) =>
        new() { type = GameActionType.Pass, owner = owner };
}
