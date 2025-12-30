using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Entry point for match-level networking. Wraps an IMatchTransport and
/// handles sending/receiving of game actions between peers.
/// </summary>
public class NetworkMatchManager : MonoBehaviour
{
    public static NetworkMatchManager Instance { get; private set; }

    [Tooltip(
        "Optional transport reference. If left null, will use NetworkSessionStore.CurrentTransport."
    )]
    [SerializeField]
    private MonoBehaviour transportBehaviour;

    [Header("Action Reliability")]
    [Tooltip("Time to wait for ACK before retrying (seconds).")]
    [SerializeField]
    private float ackTimeoutSeconds = 1.5f;

    [Tooltip("Maximum number of retry attempts before treating as disconnected.")]
    [SerializeField]
    private int maxRetries = 5;

    private IMatchTransport _transport;
    private int _nextSequenceId;

    // Pending action tracking for reliability
    private struct PendingAction
    {
        public int sequenceId;
        public byte[] serializedMessage;
        public float sentTime;
        public int retryCount;
    }

    private PendingAction? _pendingAction;
    private Coroutine _retryCoroutine;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

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
        if (Instance == this)
            Instance = null;

        if (_retryCoroutine != null)
        {
            StopCoroutine(_retryCoroutine);
            _retryCoroutine = null;
        }

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
                Debug.Log($"NetworkMatchManager: Received SessionHeader (seq={msg.sequenceId}).");
                // Session headers are handled during lobby phase, not in gameplay
                break;

            case NetMessageType.InputAction:
                var action = NetSerialization.DeserializeGameAction(msg.payload);

                // Only the guest needs to mirror received slot indices. The host's view
                // is canonical, so when the guest receives an action from the host, it
                // must be transformed to the guest's local coordinate system.
                // The host receives already-transformed indices from the guest, so no
                // mirroring is needed on the host side.
                if (NetworkRoleHelper.IsGuest)
                {
                    action.slotIndex = NetworkRoleHelper.MirrorSlotIndex(action.slotIndex);
                    action.targetSlotIndices = NetworkRoleHelper.MirrorSlotIndices(
                        action.targetSlotIndices
                    );
                }

                Debug.Log(
                    $"NetworkMatchManager: Received InputAction type={action.type} owner={action.owner} slot={action.slotIndex}"
                );

                // Send ACK back to confirm receipt
                SendInputActionAck(msg.sequenceId);

                // Route to the network controller
                var controller = GameManager.Instance?.GetNetworkController();
                if (controller != null)
                {
                    controller.EnqueueRemoteAction(action);
                }
                else
                {
                    Debug.LogWarning(
                        "NetworkMatchManager: No network controller to route action to."
                    );
                }
                break;

            case NetMessageType.InputActionAck:
                if (NetSerialization.TryDeserializeInputActionAck(msg.payload, out int ackedSeqId))
                {
                    HandleInputActionAck(ackedSeqId);
                }
                else
                {
                    Debug.LogWarning("NetworkMatchManager: Received malformed InputActionAck.");
                }
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
        // Phase 4 will add proper disconnection handling with UI modal
    }

    public void SendSessionHeader(NetSessionHeader header)
    {
        if (_transport == null || !_transport.IsConnected)
        {
            Debug.LogWarning("NetworkMatchManager.SendSessionHeader: No active transport.");
            return;
        }

        var payload = NetSerialization.SerializeNetSessionHeader(header);
        var msg = new NetMessage
        {
            type = NetMessageType.SessionHeader,
            sequenceId = _nextSequenceId++,
            payload = payload,
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
        var sequenceId = _nextSequenceId++;
        var msg = new NetMessage
        {
            type = NetMessageType.InputAction,
            sequenceId = sequenceId,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);

        // Track this action as pending until ACK is received
        _pendingAction = new PendingAction
        {
            sequenceId = sequenceId,
            serializedMessage = bytes,
            sentTime = Time.unscaledTime,
            retryCount = 0,
        };

        _transport.Send(bytes);

        Debug.Log(
            $"NetworkMatchManager: Sent InputAction type={action.type} owner={action.owner} seq={sequenceId}"
        );

        // Start retry coroutine if not already running
        if (_retryCoroutine == null)
        {
            _retryCoroutine = StartCoroutine(RetryPendingActionCoroutine());
        }
    }

    private void SendInputActionAck(int sequenceId)
    {
        if (_transport == null || !_transport.IsConnected)
            return;

        var payload = NetSerialization.SerializeInputActionAck(sequenceId);
        var msg = new NetMessage
        {
            type = NetMessageType.InputActionAck,
            sequenceId = sequenceId,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);

        Debug.Log($"NetworkMatchManager: Sent InputActionAck for seq={sequenceId}");
    }

    private void HandleInputActionAck(int acknowledgedSequenceId)
    {
        if (_pendingAction.HasValue && _pendingAction.Value.sequenceId == acknowledgedSequenceId)
        {
            Debug.Log(
                $"NetworkMatchManager: Received ACK for seq={acknowledgedSequenceId}, clearing pending action."
            );
            _pendingAction = null;

            // Stop the retry coroutine since there's nothing pending
            if (_retryCoroutine != null)
            {
                StopCoroutine(_retryCoroutine);
                _retryCoroutine = null;
            }
        }
        else
        {
            Debug.Log(
                $"NetworkMatchManager: Received ACK for seq={acknowledgedSequenceId}, but no matching pending action."
            );
        }
    }

    private IEnumerator RetryPendingActionCoroutine()
    {
        while (_pendingAction.HasValue)
        {
            // Wait for the timeout period
            yield return new WaitForSecondsRealtime(ackTimeoutSeconds);

            // Check if still pending (ACK may have arrived during wait)
            if (!_pendingAction.HasValue)
                break;

            var pending = _pendingAction.Value;

            // Check if we've exceeded max retries
            if (pending.retryCount >= maxRetries)
            {
                Debug.LogError(
                    $"NetworkMatchManager: Max retries ({maxRetries}) exceeded for seq={pending.sequenceId}. Treating as disconnected."
                );
                _pendingAction = null;
                HandleDisconnected();
                break;
            }

            // Retry sending
            if (_transport != null && _transport.IsConnected)
            {
                pending.retryCount++;
                pending.sentTime = Time.unscaledTime;
                _pendingAction = pending;

                _transport.Send(pending.serializedMessage);
                Debug.LogWarning(
                    $"NetworkMatchManager: Retrying InputAction seq={pending.sequenceId} (attempt {pending.retryCount}/{maxRetries})"
                );
            }
            else
            {
                Debug.LogError("NetworkMatchManager: Transport disconnected during retry.");
                _pendingAction = null;
                HandleDisconnected();
                break;
            }
        }

        _retryCoroutine = null;
    }
}
