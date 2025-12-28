using System;
using UnityEngine;

/// <summary>
/// Minimal entry point for match-level networking. For now this is a thin
/// wrapper around an IMatchTransport that logs incoming NetMessages and
/// exposes helpers for sending session headers and input actions.
/// </summary>
public class NetworkMatchManager : MonoBehaviour
{
    [Tooltip(
        "Optional transport reference. If left null, will use NetworkSessionStore.CurrentTransport."
    )]
    [SerializeField]
    private MonoBehaviour transportBehaviour;

    private IMatchTransport _transport;
    private int _nextSequenceId;

    private void Awake()
    {
        _transport = transportBehaviour as IMatchTransport ?? NetworkSessionStore.CurrentTransport;

        if (_transport == null)
        {
            Debug.LogWarning("NetworkMatchManager: No transport assigned; networking disabled.");
            return;
        }

        _transport.OnDataReceived += HandleDataReceived;
        _transport.OnDisconnected += HandleDisconnected;
    }

    private void OnDestroy()
    {
        if (_transport != null)
        {
            _transport.OnDataReceived -= HandleDataReceived;
            _transport.OnDisconnected -= HandleDisconnected;
        }
    }

    private void HandleDataReceived(byte[] data)
    {
        if (!NetSerialization.TryDeserializeNetMessage(data, out var msg))
        {
            Debug.LogWarning("NetworkMatchManager: Received malformed NetMessage.");
            return;
        }

        switch (msg.type)
        {
            case NetMessageType.SessionHeader:
                Debug.Log(
                    $"NetworkMatchManager: Received SessionHeader (seq={msg.sequenceId}, bytes={msg.payload?.Length ?? 0})."
                );
                // In a later pass, this will be used only in the hub/handshake
                // phase; gameplay scenes will already have an agreed header.
                break;

            case NetMessageType.InputAction:
                Debug.Log(
                    $"NetworkMatchManager: Received InputAction (seq={msg.sequenceId}, bytes={msg.payload?.Length ?? 0})."
                );
                // A future step will deserialize the GameAction and enqueue it
                // into GameManager for the appropriate player.
                break;

            default:
                Debug.LogWarning(
                    $"NetworkMatchManager: Received NetMessage with unknown type {msg.type}."
                );
                break;
        }
    }

    private void HandleDisconnected()
    {
        Debug.Log("NetworkMatchManager: Transport disconnected.");
    }

    public void SendSessionHeader(NetSessionHeader header)
    {
        if (_transport == null || !_transport.IsConnected)
        {
            Debug.LogWarning("NetworkMatchManager.SendSessionHeader: No active transport.");
            return;
        }

        // For now we do not serialise the structured header; a later pass can
        // add a dedicated serializer. This just sends an empty payload with
        // the appropriate type/sequence for wiring tests.
        var msg = new NetMessage
        {
            type = NetMessageType.SessionHeader,
            sequenceId = _nextSequenceId++,
            payload = Array.Empty<byte>(),
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);
    }

    public void SendInputAction(GameAction action)
    {
        if (_transport == null || !_transport.IsConnected)
        {
            Debug.LogWarning("NetworkMatchManager.SendInputAction: No active transport.");
            return;
        }

        var payload = NetSerialization.SerializeGameAction(action);
        var msg = new NetMessage
        {
            type = NetMessageType.InputAction,
            sequenceId = _nextSequenceId++,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);
    }
}


