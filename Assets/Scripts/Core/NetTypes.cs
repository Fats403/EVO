using System;

/// <summary>
/// High-level session configuration that both peers must agree on before
/// starting a networked match. This will eventually be filled during the
/// DeckHub \"challenge / ready\" flow and consumed by the game bootstrapper.
/// </summary>
[Serializable]
public struct NetSessionHeader
{
    public int protocolVersion;

    /// <summary>
    /// Seed used to initialise the deterministic RNG for this match so that
    /// both peers simulate the same sequence of random events.
    /// </summary>
    public int rngSeed;

    /// <summary>Steam IDs or other platform identifiers for each player.</summary>
    public string hostId;
    public string guestId;

    /// <summary>
    /// The role that the local client will occupy (e.g., Player1 vs Player2).
    /// For now this is a simple SlotOwner, but it can be expanded later.
    /// </summary>
    public SlotOwner localRole;

    /// <summary>
    /// Optional deck identifiers / metadata for host and guest. The concrete
    /// deck lists are represented by DeckCardEntry arrays so they can be
    /// reconstructed on each client using the local CardDatabase.
    /// </summary>
    public string hostDeckId;
    public string guestDeckId;
    public DeckCardEntry[] hostDeck;
    public DeckCardEntry[] guestDeck;
}

/// <summary>
/// Top-level message kinds exchanged between peers. This is intentionally
/// small for now; more types can be added as features grow.
/// </summary>
public enum NetMessageType : byte
{
    Unknown = 0,
    SessionHeader = 1,
    InputAction = 2,
    SessionAck = 3,
    InputActionAck = 4,
    RoundChecksum = 5, // Sent at end of each round for desync detection
}

/// <summary>
/// Generic network message wrapper used by the transport and higher-level
/// systems. The payload format depends on the NetMessageType.
/// </summary>
[Serializable]
public struct NetMessage
{
    public NetMessageType type;

    /// <summary>
    /// Monotonic sequence ID used for ordering / de-duplication. For
    /// turn-based lockstep this can be the turn index or a simple counter.
    /// </summary>
    public int sequenceId;

    /// <summary>
    /// Raw payload bytes for this message. Higher-level helpers are
    /// responsible for encoding/decoding structured data into this buffer.
    /// </summary>
    public byte[] payload;
}

/// <summary>
/// Simple static holder for the active network session and transport during
/// a match. This avoids passing references through many layers of code.
/// </summary>
public static class NetworkSessionStore
{
    /// <summary>The agreed header for the current network session.</summary>
    public static NetSessionHeader? CurrentHeader { get; set; }

    /// <summary>
    /// The active transport used for this match. This is set up by the
    /// DeckHub challenge / invite flow and consumed by gameplay systems.
    /// </summary>
    public static IMatchTransport CurrentTransport { get; set; }

    /// <summary>
    /// Returns true if we are currently in a networked game with an active
    /// session header and transport. Use this to check game mode.
    /// </summary>
    public static bool IsNetworkedGame => CurrentHeader.HasValue && CurrentTransport != null;

    /// <summary>Clears the current session and transport references.</summary>
    public static void Clear()
    {
        CurrentHeader = null;
        CurrentTransport = null;
    }
}
