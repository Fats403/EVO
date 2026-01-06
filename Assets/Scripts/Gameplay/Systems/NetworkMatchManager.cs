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

    [Header("Heartbeat Settings")]
    [Tooltip("Interval between heartbeat messages (seconds).")]
    [SerializeField]
    private float heartbeatIntervalSeconds = 3f;

    [Header("Reconnection Settings")]
    [Tooltip("Time to wait for state sync response (seconds).")]
    [SerializeField]
    private float stateSyncTimeoutSeconds = 10f;

    private IMatchTransport _transport;
    private int _nextSequenceId;
    private float _lastHeartbeatTime;

    // Reconnection state
    private bool _awaitingStateSync;
    private float _stateSyncRequestTime;

    /// <summary>
    /// Raised when a state sync is received and successfully applied.
    /// </summary>
    public event Action<int> OnStateSyncApplied;

    /// <summary>
    /// Raised when a state sync request fails.
    /// </summary>
    public event Action<string> OnStateSyncFailed;

    // Pending action tracking for reliability
    private struct PendingAction
    {
        public int sequenceId;
        public byte[] serializedMessage;
        public GameAction originalAction; // Keep original for retransmit
        public float sentTime;
        public int retryCount;
    }

    private PendingAction? _pendingAction;
    private Coroutine _retryCoroutine;

    // Retransmit request tracking
    [Header("Retransmit Settings")]
    [Tooltip("Maximum number of retransmit requests before giving up.")]
    [SerializeField]
    private int maxRetransmitRequests = 3;

    private int _retransmitRequestCount;
    private int _lastReceivedSequenceId = -1;

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

    private void Update()
    {
        if (_transport == null || !_transport.IsConnected)
            return;

        // Send periodic heartbeats to prevent false disconnect detection
        if (Time.unscaledTime - _lastHeartbeatTime >= heartbeatIntervalSeconds)
        {
            SendHeartbeat();
            _lastHeartbeatTime = Time.unscaledTime;
        }

        // Check for state sync timeout
        if (
            _awaitingStateSync
            && Time.unscaledTime - _stateSyncRequestTime > stateSyncTimeoutSeconds
        )
        {
            _awaitingStateSync = false;
            OnStateSyncFailed?.Invoke("State sync request timed out.");
            Debug.LogWarning("NetworkMatchManager: State sync request timed out.");
        }
    }

    private void HandleDataReceived(byte[] data)
    {
        // Notify sync validator that we received data (resets disconnect timer)
        NetworkSyncValidator.Instance?.OnMessageReceived();

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

                // Validate the action - reject Invalid or corrupted actions
                if (action.type == GameActionType.Invalid)
                {
                    Debug.LogError(
                        $"NetworkMatchManager: Received Invalid/corrupted InputAction (seq={msg.sequenceId}). Requesting retransmit."
                    );
                    // Request a retransmit instead of just ACKing
                    SendRetransmitRequest(msg.sequenceId, "Invalid or corrupted action payload");
                    break;
                }

                // Track received sequence for duplicate detection
                if (msg.sequenceId <= _lastReceivedSequenceId && _lastReceivedSequenceId >= 0)
                {
                    Debug.LogWarning(
                        $"NetworkMatchManager: Received duplicate or old InputAction (seq={msg.sequenceId}, last={_lastReceivedSequenceId}). Sending ACK but ignoring."
                    );
                    SendInputActionAck(msg.sequenceId);
                    break;
                }
                _lastReceivedSequenceId = msg.sequenceId;

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

            case NetMessageType.RoundChecksum:
                if (
                    NetworkSyncValidator.TryDeserializeChecksumPayload(
                        msg.payload,
                        out int round,
                        out int checksum,
                        out int rngCallCount
                    )
                )
                {
                    Debug.Log(
                        $"NetworkMatchManager: Received RoundChecksum round={round} checksum={checksum:X8}"
                    );
                    NetworkSyncValidator.Instance?.HandleRemoteChecksum(
                        round,
                        checksum,
                        rngCallCount
                    );
                }
                else
                {
                    Debug.LogWarning("NetworkMatchManager: Received malformed RoundChecksum.");
                }
                break;

            case NetMessageType.StateRequest:
                if (NetSerialization.TryDeserializeStateRequest(msg.payload, out var stateRequest))
                {
                    Debug.Log(
                        $"NetworkMatchManager: Received StateRequest (lastRound={stateRequest.lastKnownRound}, reconnecting={stateRequest.isReconnecting})"
                    );
                    HandleStateRequest(stateRequest);
                }
                else
                {
                    Debug.LogWarning("NetworkMatchManager: Received malformed StateRequest.");
                }
                break;

            case NetMessageType.StateSync:
                if (NetSerialization.TryDeserializeStateSync(msg.payload, out var stateSync))
                {
                    Debug.Log(
                        $"NetworkMatchManager: Received StateSync (round={stateSync.round}, success={stateSync.success})"
                    );
                    HandleStateSync(stateSync);
                }
                else
                {
                    Debug.LogWarning("NetworkMatchManager: Received malformed StateSync.");
                }
                break;

            case NetMessageType.Heartbeat:
                // Heartbeat just resets the disconnect timer (already done via OnMessageReceived)
                break;

            // Note: CardChoice messages are no longer used - pre-play choices are embedded in GameAction
            case NetMessageType.CardChoice:
            case NetMessageType.CardChoiceAck:
                Debug.LogWarning(
                    "NetworkMatchManager: Received deprecated CardChoice message. Pre-play choices are now in GameAction."
                );
                break;

            case NetMessageType.RetransmitRequest:
                if (
                    NetSerialization.TryDeserializeRetransmitRequest(
                        msg.payload,
                        out var retransmitRequest
                    )
                )
                {
                    Debug.LogWarning(
                        $"NetworkMatchManager: Received RetransmitRequest for seq={retransmitRequest.sequenceId} reason='{retransmitRequest.reason}'"
                    );
                    HandleRetransmitRequest(retransmitRequest);
                }
                else
                {
                    Debug.LogWarning("NetworkMatchManager: Received malformed RetransmitRequest.");
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
        // Notify the sync validator so it can show disconnect UI and begin
        // waiting for potential reconnect. This is a hard signal from the
        // transport, so we bypass the timeout-based check.
        if (NetworkSyncValidator.Instance != null)
        {
            Debug.Log("NetworkMatchManager: Calling ForcePeerDisconnected on validator...");
            NetworkSyncValidator.Instance.ForcePeerDisconnected("Transport reported disconnect");
        }
        else
        {
            Debug.LogError(
                "NetworkMatchManager: NetworkSyncValidator.Instance is NULL! Cannot trigger disconnect overlay."
            );
        }
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
            originalAction = action, // Store for potential retransmit
            sentTime = Time.unscaledTime,
            retryCount = 0,
        };

        _transport.Send(bytes);

        Debug.Log(
            $"NetworkMatchManager: Sent InputAction type={action.type} owner={action.owner} seq={sequenceId}"
        );

        // Reset retransmit counter for new action
        _retransmitRequestCount = 0;

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
            _retransmitRequestCount = 0; // Reset for next action

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

    // -------------------------------------------------------------------------
    // Retransmit Request
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a request to the peer to retransmit a specific action.
    /// Called when we receive a corrupted/invalid action payload.
    /// </summary>
    private void SendRetransmitRequest(int sequenceId, string reason)
    {
        if (_transport == null || !_transport.IsConnected)
            return;

        _retransmitRequestCount++;

        if (_retransmitRequestCount > maxRetransmitRequests)
        {
            Debug.LogError(
                $"NetworkMatchManager: Max retransmit requests ({maxRetransmitRequests}) exceeded for seq={sequenceId}. Triggering state sync recovery."
            );
            // Fall back to full state sync as a last resort
            var currentChecksum = GameStateChecksum.ComputeChecksum();
            int currentRound = GameManager.Instance?.CurrentRound ?? 0;
            RequestStateSync(currentRound, currentChecksum.checksum, isReconnecting: false);
            return;
        }

        var request = new RetransmitRequestPayload { sequenceId = sequenceId, reason = reason };

        var payload = NetSerialization.SerializeRetransmitRequest(request);
        var msg = new NetMessage
        {
            type = NetMessageType.RetransmitRequest,
            sequenceId = sequenceId,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);

        Debug.Log(
            $"NetworkMatchManager: Sent RetransmitRequest for seq={sequenceId} (attempt {_retransmitRequestCount}/{maxRetransmitRequests})"
        );
    }

    /// <summary>
    /// Handles a retransmit request from the peer by resending the action.
    /// </summary>
    private void HandleRetransmitRequest(RetransmitRequestPayload request)
    {
        // Check if we have the requested action pending
        if (!_pendingAction.HasValue)
        {
            Debug.LogWarning(
                $"NetworkMatchManager: Received RetransmitRequest for seq={request.sequenceId}, but no pending action available."
            );
            return;
        }

        var pending = _pendingAction.Value;

        if (pending.sequenceId != request.sequenceId)
        {
            Debug.LogWarning(
                $"NetworkMatchManager: Received RetransmitRequest for seq={request.sequenceId}, but pending action is seq={pending.sequenceId}."
            );
            return;
        }

        // Re-serialize and resend the action with the same sequence ID
        // This ensures the receiver can properly process it
        var freshPayload = NetSerialization.SerializeGameAction(pending.originalAction);
        var msg = new NetMessage
        {
            type = NetMessageType.InputAction,
            sequenceId = pending.sequenceId,
            payload = freshPayload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);

        // Update pending action with fresh serialization
        pending.serializedMessage = bytes;
        pending.sentTime = Time.unscaledTime;
        pending.retryCount++; // Count this as a retry
        _pendingAction = pending;

        _transport.Send(bytes);

        Debug.Log(
            $"NetworkMatchManager: Retransmitted InputAction type={pending.originalAction.type} owner={pending.originalAction.owner} seq={pending.sequenceId}"
        );
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

    // -------------------------------------------------------------------------
    // Heartbeat
    // -------------------------------------------------------------------------

    private void SendHeartbeat()
    {
        if (_transport == null || !_transport.IsConnected)
            return;

        var payload = NetSerialization.SerializeHeartbeat();
        var msg = new NetMessage
        {
            type = NetMessageType.Heartbeat,
            sequenceId = 0,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);
    }

    // -------------------------------------------------------------------------
    // State Synchronization (Reconnection)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Requests the current game state from the peer.
    /// Called when reconnecting or when a desync is detected.
    /// </summary>
    public void RequestStateSync(
        int lastKnownRound,
        int lastKnownChecksum,
        bool isReconnecting = true
    )
    {
        if (_transport == null || !_transport.IsConnected)
        {
            Debug.LogWarning("NetworkMatchManager.RequestStateSync: No active transport.");
            return;
        }

        if (_awaitingStateSync)
        {
            Debug.LogWarning("NetworkMatchManager: Already awaiting state sync.");
            return;
        }

        var request = new StateRequestPayload
        {
            lastKnownRound = lastKnownRound,
            lastKnownChecksum = lastKnownChecksum,
            isReconnecting = isReconnecting,
        };

        var payload = NetSerialization.SerializeStateRequest(request);
        var msg = new NetMessage
        {
            type = NetMessageType.StateRequest,
            sequenceId = _nextSequenceId++,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);

        _awaitingStateSync = true;
        _stateSyncRequestTime = Time.unscaledTime;

        Debug.Log(
            $"NetworkMatchManager: Sent StateRequest (lastRound={lastKnownRound}, checksum={lastKnownChecksum:X8})"
        );
    }

    /// <summary>
    /// Handles a state request from the peer by sending current game state.
    /// </summary>
    private void HandleStateRequest(StateRequestPayload request)
    {
        // Capture current game state
        var currentState = GameStateSerialization.CaptureState();
        var currentChecksum = GameStateChecksum.ComputeChecksum();

        // Check if peer is already in sync
        if (
            request.lastKnownRound == currentState.round
            && request.lastKnownChecksum == currentChecksum.checksum
        )
        {
            // Peer is already in sync, send success without full state
            SendStateSyncResponse(currentState.round, currentChecksum.checksum, null, true, null);
            Debug.Log("NetworkMatchManager: Peer already in sync, sent confirmation.");
            return;
        }

        // Serialize and send full state
        var stateBytes = GameStateSerialization.Serialize(currentState);
        SendStateSyncResponse(currentState.round, currentChecksum.checksum, stateBytes, true, null);

        Debug.Log(
            $"NetworkMatchManager: Sent StateSync (round={currentState.round}, size={stateBytes.Length} bytes)"
        );
    }

    /// <summary>
    /// Handles a state sync response from the peer.
    /// </summary>
    private void HandleStateSync(StateSyncPayload sync)
    {
        _awaitingStateSync = false;

        if (!sync.success)
        {
            Debug.LogError($"NetworkMatchManager: State sync failed: {sync.errorMessage}");
            OnStateSyncFailed?.Invoke(sync.errorMessage ?? "Unknown error");
            return;
        }

        // If no state data, peer confirmed we're already in sync
        if (sync.stateData == null || sync.stateData.Length == 0)
        {
            Debug.Log("NetworkMatchManager: Peer confirmed we're in sync.");
            OnStateSyncApplied?.Invoke(sync.round);
            return;
        }

        // Deserialize and apply the state
        var gameState = GameStateSerialization.Deserialize(sync.stateData);

        // Verify checksum
        // Note: We'll apply the state first, then verify. In production you might
        // want to validate before applying.

        Debug.Log($"NetworkMatchManager: Applying state sync for round {sync.round}...");

        // Apply the state through GameStateRestorer
        if (GameStateRestorer.Instance != null)
        {
            GameStateRestorer.Instance.RestoreState(gameState);
            OnStateSyncApplied?.Invoke(sync.round);
            Debug.Log(
                $"NetworkMatchManager: State sync applied successfully (round {sync.round})."
            );
        }
        else
        {
            Debug.LogError(
                "NetworkMatchManager: GameStateRestorer not found, cannot apply state sync."
            );
            OnStateSyncFailed?.Invoke("GameStateRestorer not available");
        }
    }

    /// <summary>
    /// Sends a state sync response to the peer.
    /// </summary>
    private void SendStateSyncResponse(
        int round,
        int checksum,
        byte[] stateData,
        bool success,
        string errorMessage
    )
    {
        if (_transport == null || !_transport.IsConnected)
            return;

        var sync = new StateSyncPayload
        {
            round = round,
            checksum = checksum,
            stateData = stateData ?? Array.Empty<byte>(),
            success = success,
            errorMessage = errorMessage ?? "",
        };

        var payload = NetSerialization.SerializeStateSync(sync);
        var msg = new NetMessage
        {
            type = NetMessageType.StateSync,
            sequenceId = _nextSequenceId++,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        _transport.Send(bytes);
    }

    /// <summary>
    /// Returns true if we're currently waiting for a state sync response.
    /// </summary>
    public bool IsAwaitingStateSync => _awaitingStateSync;

    /// <summary>
    /// Cancels any pending state sync request.
    /// </summary>
    public void CancelStateSyncRequest()
    {
        _awaitingStateSync = false;
    }
}
