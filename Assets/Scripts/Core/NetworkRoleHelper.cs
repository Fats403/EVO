/// <summary>
/// Helper for determining local vs remote player perspective in networked games.
/// </summary>
public static class NetworkRoleHelper
{
    /// <summary>
    /// Returns true if the given SlotOwner represents the local player.
    /// In AI mode, Player1 is always local. In networked mode, uses the session header.
    /// </summary>
    public static bool IsLocalPlayer(SlotOwner owner)
    {
        if (!NetworkSessionStore.IsNetworkedGame)
            return owner == SlotOwner.Player1; // AI mode: you're always P1

        return owner == NetworkSessionStore.CurrentHeader.Value.localRole;
    }

    /// <summary>
    /// Returns the SlotOwner for the local player.
    /// </summary>
    public static SlotOwner LocalRole =>
        NetworkSessionStore.IsNetworkedGame
            ? NetworkSessionStore.CurrentHeader.Value.localRole
            : SlotOwner.Player1;

    /// <summary>
    /// Returns the SlotOwner for the remote player (opponent).
    /// </summary>
    public static SlotOwner RemoteRole =>
        LocalRole == SlotOwner.Player1 ? SlotOwner.Player2 : SlotOwner.Player1;

    /// <summary>
    /// Converts a local slot index to the index the remote peer expects using
    /// a full flip (row swap + column reversal). This creates the effect of
    /// viewing the board from the opposite side:
    ///   slot 0 (bottom-left) ↔ slot 9 (top-right)
    ///   slot 4 (bottom-right) ↔ slot 5 (top-left)
    /// Used when the guest sends/receives actions to maintain the illusion
    /// that each player is playing from "their side" of the board.
    /// </summary>
    /// <param name="localIndex">The slot index from the local player's perspective.</param>
    /// <param name="slotsPerPlayer">Number of slots per player (default 5).</param>
    /// <returns>The mirrored slot index for the remote peer.</returns>
    public static int MirrorSlotIndex(int localIndex, int slotsPerPlayer = 5)
    {
        if (localIndex < 0)
            return localIndex;
        int totalSlots = slotsPerPlayer * 2;
        return totalSlots - 1 - localIndex;
    }

    /// <summary>
    /// Mirrors a list of slot indices for network transmission.
    /// </summary>
    public static System.Collections.Generic.List<int> MirrorSlotIndices(
        System.Collections.Generic.List<int> indices,
        int slotsPerPlayer = 5
    )
    {
        if (indices == null)
            return null;
        var mirrored = new System.Collections.Generic.List<int>(indices.Count);
        foreach (var idx in indices)
        {
            mirrored.Add(MirrorSlotIndex(idx, slotsPerPlayer));
        }
        return mirrored;
    }

    /// <summary>
    /// Returns true if the given slot is in the local player's visual zone.
    /// For both host and guest, the bottom row (slots 0-4) is considered the
    /// "local player's side" for UI interaction purposes. This allows each
    /// player to place cards in the slots closest to them visually.
    /// </summary>
    /// <param name="slot">The board slot to check.</param>
    /// <param name="slotsPerPlayer">Number of slots per player (default 5).</param>
    /// <returns>True if the slot is in the local player's visual zone.</returns>
    public static bool IsLocalPlayerVisualSlot(BoardSlot slot, int slotsPerPlayer = 5)
    {
        if (slot == null)
            return false;
        // Bottom slots (0 to slotsPerPlayer-1) are the "local player's side" visually
        return slot.index >= 0 && slot.index < slotsPerPlayer;
    }

    /// <summary>
    /// Returns true if the local player is the guest (Player2) in a networked game.
    /// Used to determine when slot index mirroring should be applied.
    /// </summary>
    public static bool IsGuest =>
        NetworkSessionStore.IsNetworkedGame && LocalRole == SlotOwner.Player2;
}
