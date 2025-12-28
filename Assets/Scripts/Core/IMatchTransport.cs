using System;

/// <summary>
/// Minimal abstraction over a point-to-point transport for a single match.
/// Implementations (e.g., SteamP2PTransport) provide reliable, ordered
/// delivery of small messages between two peers.
/// </summary>
public interface IMatchTransport
{
    /// <summary>True if this side is acting as the host / authority.</summary>
    bool IsHost { get; }

    /// <summary>True if the underlying connection is currently active.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Sends a raw payload to the remote peer. Higher-level systems are
    /// responsible for serialising structured messages into this buffer.
    /// </summary>
    void Send(byte[] data);

    /// <summary>Cleanly closes the underlying connection.</summary>
    void Disconnect();

    /// <summary>
    /// Raised when a complete payload has been received from the remote peer.
    /// </summary>
    event Action<byte[]> OnDataReceived;

    /// <summary>
    /// Raised when the underlying connection has been closed or lost.
    /// </summary>
    event Action OnDisconnected;
}


