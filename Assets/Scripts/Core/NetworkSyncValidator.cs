using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Core networking system that validates game state synchronization between peers.
/// Responsibilities:
/// 1. Exchange checksums at round end to detect desync
/// 2. Detect and handle player disconnection
/// 3. Fire events for UI to respond to network issues
///
/// Attach this to a persistent GameObject alongside NetworkMatchManager.
/// </summary>
public class NetworkSyncValidator : MonoBehaviour
{
    public static NetworkSyncValidator Instance { get; private set; }

    [Header("Checksum Settings")]
    [Tooltip("Show an on-screen warning when desync is detected")]
    public bool showDesyncAlert = true;

    [Tooltip("Auto-save debug dumps when desync is detected")]
    public bool autoSaveDumps = true;

    [Tooltip("Pause game on desync for debugging (Editor only)")]
    public bool pauseOnDesync = false;

    [Header("Disconnect Settings")]
    [Tooltip("Seconds without any message before considering peer disconnected")]
    public float disconnectTimeoutSeconds = 10f;

    [Tooltip("Seconds to wait for reconnect before allowing forfeit")]
    public float reconnectGracePeriodSeconds = 30f;

    // Events for UI/game systems to respond to
    public event Action<
        int,
        GameStateChecksum.GameStateSnapshot,
        GameStateChecksum.GameStateSnapshot
    > OnDesyncDetected;
    public event Action OnPeerDisconnected;
    public event Action OnPeerReconnected;
    public event Action<float> OnWaitingForPeer; // Passes seconds waited so far
    public event Action OnResyncStarted;
    public event Action<int> OnResyncCompleted; // Passes round number
    public event Action<string> OnResyncFailed; // Passes error message

    // State
    private GameStateChecksum.GameStateSnapshot _lastLocalSnapshot;
    private GameStateChecksum.GameStateSnapshot _lastRemoteSnapshot;
    private bool _waitingForRemoteChecksum;
    private int _pendingRound;

    // Disconnect tracking
    private float _lastMessageReceivedTime;
    private bool _isPeerDisconnected;
    private bool _isWaitingForPeer;
    private float _waitingStartTime;

    // Reconnection state
    private int _lastValidatedRound;
    private int _lastValidatedChecksum;
    private bool _isResyncInProgress;

    public bool IsPeerDisconnected => _isPeerDisconnected;
    public bool IsWaitingForPeer => _isWaitingForPeer;
    public float SecondsSinceLastMessage => Time.unscaledTime - _lastMessageReceivedTime;
    public int LastValidatedRound => _lastValidatedRound;
    public int LastValidatedChecksum => _lastValidatedChecksum;
    public bool IsResyncInProgress => _isResyncInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        _lastMessageReceivedTime = Time.unscaledTime;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void Update()
    {
        if (!NetworkSessionStore.IsNetworkedGame)
        {
            // Uncomment to debug: Debug.Log("[NetworkSyncValidator] Not a networked game, skipping disconnect check.");
            return;
        }

        // CRITICAL: Only check for disconnects during interactive phases (Place).
        // During Resolve/End phases, no messages are expected and false positives occur.
        // During Setup/Draw phases, we're transitioning and shouldn't interrupt.
        var currentPhase = GameManager.Instance?.currentPhase ?? GamePhase.Setup;
        bool shouldCheckDisconnect =
            GameManager.Instance != null && currentPhase == GamePhase.Place;

        if (shouldCheckDisconnect)
        {
            float timeSinceMessage = SecondsSinceLastMessage;

            // Debug logging - remove after testing
            if (timeSinceMessage > 5f && !_isPeerDisconnected)
            {
                Debug.Log(
                    $"[NetworkSyncValidator] No message for {timeSinceMessage:F1}s (threshold: {disconnectTimeoutSeconds}s)"
                );
            }

            if (!_isPeerDisconnected && timeSinceMessage > disconnectTimeoutSeconds)
            {
                HandlePeerDisconnected();
            }
        }
        else if (!_isPeerDisconnected)
        {
            // Debug: Log why we're not checking (remove after testing)
            // Debug.Log($"[NetworkSyncValidator] Not checking disconnect - phase is {currentPhase}, need Place");
        }

        // Update waiting state
        if (_isWaitingForPeer)
        {
            float waitedSeconds = Time.unscaledTime - _waitingStartTime;
            OnWaitingForPeer?.Invoke(waitedSeconds);
        }
    }

    /// <summary>
    /// Call this whenever any message is received from the peer to reset the timeout.
    /// </summary>
    public void OnMessageReceived()
    {
        _lastMessageReceivedTime = Time.unscaledTime;

        // If we were disconnected and got a message, peer has reconnected
        if (_isPeerDisconnected)
        {
            HandlePeerReconnected();
        }
    }

    /// <summary>
    /// Forces the peer into a disconnected state immediately, bypassing the
    /// normal timeout check. Use this when the transport reports a hard
    /// disconnect (e.g., SteamP2P OnDisconnected).
    /// </summary>
    public void ForcePeerDisconnected(string reason = null)
    {
        Debug.LogWarning(
            $"[NetworkSyncValidator] ForcePeerDisconnected called. Reason: {reason ?? "transport disconnect"}"
        );

        // Only force disconnect if we are in a networked game and haven't
        // already marked the peer as disconnected.
        if (!NetworkSessionStore.IsNetworkedGame)
        {
            Debug.LogWarning(
                "[NetworkSyncValidator] ForcePeerDisconnected ignored - not a networked game."
            );
            return;
        }

        HandlePeerDisconnected();
    }

    private void HandlePeerDisconnected()
    {
        if (_isPeerDisconnected)
        {
            Debug.Log(
                "[NetworkSyncValidator] HandlePeerDisconnected called but already disconnected, ignoring."
            );
            return;
        }

        _isPeerDisconnected = true;
        _isWaitingForPeer = true;
        _waitingStartTime = Time.unscaledTime;

        Debug.LogWarning("[NetworkSyncValidator] Peer appears to have disconnected!");

        // Fire event - DisconnectOverlay handles the UI (single source of truth)
        int subscriberCount = OnPeerDisconnected?.GetInvocationList()?.Length ?? 0;
        Debug.Log(
            $"[NetworkSyncValidator] Firing OnPeerDisconnected event to {subscriberCount} subscriber(s)..."
        );

        if (subscriberCount == 0)
        {
            Debug.LogError(
                "[NetworkSyncValidator] NO SUBSCRIBERS for OnPeerDisconnected! DisconnectOverlay may not have subscribed."
            );
        }

        OnPeerDisconnected?.Invoke();
        Debug.Log("[NetworkSyncValidator] OnPeerDisconnected event fired.");
    }

    private void HandlePeerReconnected()
    {
        if (!_isPeerDisconnected)
            return;

        _isPeerDisconnected = false;
        _isWaitingForPeer = false;

        Debug.Log("[NetworkSyncValidator] Peer has reconnected!");

        // Fire event - DisconnectOverlay handles the UI (single source of truth)
        OnPeerReconnected?.Invoke();

        // Initiate resync to ensure both clients are in the same state
        // Only the client that was waiting (not the one who disconnected) initiates
        StartCoroutine(DelayedResyncRequest());
    }

    /// <summary>
    /// Delays the resync request slightly to allow the transport to stabilize.
    /// </summary>
    private System.Collections.IEnumerator DelayedResyncRequest()
    {
        // Small delay to let the connection stabilize
        yield return new WaitForSeconds(0.5f);

        // Check if we actually need to resync (if we have a last validated state)
        if (_lastValidatedRound > 0)
        {
            Debug.Log(
                $"[NetworkSyncValidator] Initiating resync from round {_lastValidatedRound}..."
            );
            OnResyncStarted?.Invoke();
            RequestResyncAfterReconnect();
        }
        else
        {
            Debug.Log("[NetworkSyncValidator] No validated state to resync from.");
        }
    }

    /// <summary>
    /// Called by the UI when the player chooses to forfeit while waiting.
    /// </summary>
    public void ForfeitMatch()
    {
        Debug.Log("[NetworkSyncValidator] Player chose to forfeit.");

        // Clear network session
        NetworkSessionStore.CurrentTransport?.Disconnect();
        NetworkSessionStore.Clear();

        // Return to main menu
        SceneTransitionManager.Instance?.LoadScene("MainMenu");
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (!NetworkSessionStore.IsNetworkedGame)
            return;

        // Reset disconnect timer when entering Place phase to avoid false positives
        // from long resolution phases.
        if (phase == GamePhase.Place)
        {
            _lastMessageReceivedTime = Time.unscaledTime;

            // Clear any false disconnect state from resolution phase
            if (_isPeerDisconnected && !_isWaitingForPeer)
            {
                _isPeerDisconnected = false;
            }
        }

        if (phase == GamePhase.End)
        {
            SendRoundChecksum();
        }
    }

    /// <summary>
    /// Computes and sends the local checksum to the remote peer.
    /// </summary>
    public void SendRoundChecksum()
    {
        if (!NetworkSessionStore.IsNetworkedGame)
            return;

        int round = GameManager.Instance?.currentRound ?? 0;
        _lastLocalSnapshot = GameStateChecksum.ComputeChecksum();
        _pendingRound = round;
        _waitingForRemoteChecksum = true;

        ActionLog.Instance?.LogRoundEnd(
            round,
            _lastLocalSnapshot.checksum,
            _lastLocalSnapshot.checksum,
            _lastLocalSnapshot.p1Score,
            _lastLocalSnapshot.p2Score,
            _lastLocalSnapshot.rngCallCount
        );

        SendChecksumMessage(round, _lastLocalSnapshot.checksum, _lastLocalSnapshot.rngCallCount);

        Debug.Log(
            $"[NetworkSyncValidator] Round {round} checksum: {_lastLocalSnapshot.checksum:X8}, RNG calls: {_lastLocalSnapshot.rngCallCount}"
        );
    }

    private void SendChecksumMessage(int round, int checksum, int rngCallCount)
    {
        var transport = NetworkSessionStore.CurrentTransport;
        if (transport == null || !transport.IsConnected)
            return;

        var payload = SerializeChecksumPayload(round, checksum, rngCallCount);
        var msg = new NetMessage
        {
            type = NetMessageType.RoundChecksum,
            sequenceId = round,
            payload = payload,
        };

        var bytes = NetSerialization.SerializeNetMessage(msg);
        transport.Send(bytes);
    }

    /// <summary>
    /// Called by NetworkMatchManager when a RoundChecksum message is received.
    /// </summary>
    public void HandleRemoteChecksum(int round, int remoteChecksum, int remoteRngCallCount)
    {
        OnMessageReceived(); // Reset disconnect timer

        Debug.Log(
            $"[NetworkSyncValidator] Remote checksum for round {round}: {remoteChecksum:X8}, RNG calls: {remoteRngCallCount}"
        );

        _lastRemoteSnapshot = new GameStateChecksum.GameStateSnapshot
        {
            round = round,
            checksum = remoteChecksum,
            rngCallCount = remoteRngCallCount,
        };

        if (_pendingRound != round || !_waitingForRemoteChecksum)
        {
            _lastLocalSnapshot = GameStateChecksum.ComputeChecksum();
            _pendingRound = round;
        }

        _waitingForRemoteChecksum = false;

        if (_lastLocalSnapshot.checksum != remoteChecksum)
        {
            Debug.LogError(
                $"[NetworkSyncValidator] MISMATCH! Local={_lastLocalSnapshot.checksum:X8} vs Remote={remoteChecksum:X8}"
            );
            HandleDesyncDetectedInternal(round, remoteChecksum, remoteRngCallCount);
        }
        else
        {
            Debug.Log(
                $"[NetworkSyncValidator] Round {round} checksums MATCH ✓ (checksum: {_lastLocalSnapshot.checksum:X8})"
            );

            // Save checkpoint on successful validation
            _lastValidatedRound = round;
            _lastValidatedChecksum = _lastLocalSnapshot.checksum;

            // Save to Firestore asynchronously
            Debug.Log($"[NetworkSyncValidator] Calling SaveCheckpointAsync for round {round}...");
            SaveCheckpointAsync(round, _lastLocalSnapshot.checksum);
        }
    }

    /// <summary>
    /// Saves a checkpoint to Firestore after successful checksum validation.
    /// </summary>
    private async void SaveCheckpointAsync(int round, int checksum)
    {
        if (MatchCheckpointManager.Instance != null)
        {
            await MatchCheckpointManager.Instance.SaveCheckpointAsync(round, checksum);
        }
    }

    /// <summary>
    /// Initiates a state sync request after reconnection.
    /// Call this when peer reconnects to ensure both clients are in sync.
    /// </summary>
    public void RequestResyncAfterReconnect()
    {
        if (_isResyncInProgress)
        {
            Debug.LogWarning("[NetworkSyncValidator] Resync already in progress.");
            return;
        }

        _isResyncInProgress = true;

        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnStateSyncApplied += HandleResyncCompleted;
            NetworkMatchManager.Instance.OnStateSyncFailed += HandleResyncFailed;
            NetworkMatchManager.Instance.RequestStateSync(
                _lastValidatedRound,
                _lastValidatedChecksum,
                true
            );
        }
        else
        {
            Debug.LogError("[NetworkSyncValidator] NetworkMatchManager not available for resync.");
            _isResyncInProgress = false;
        }
    }

    private void HandleResyncCompleted(int round)
    {
        _isResyncInProgress = false;
        _lastValidatedRound = round;

        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnStateSyncApplied -= HandleResyncCompleted;
            NetworkMatchManager.Instance.OnStateSyncFailed -= HandleResyncFailed;
        }

        Debug.Log($"[NetworkSyncValidator] Resync completed successfully (round {round}).");

        // Fire public event for UI
        OnResyncCompleted?.Invoke(round);
    }

    private void HandleResyncFailed(string error)
    {
        _isResyncInProgress = false;

        if (NetworkMatchManager.Instance != null)
        {
            NetworkMatchManager.Instance.OnStateSyncApplied -= HandleResyncCompleted;
            NetworkMatchManager.Instance.OnStateSyncFailed -= HandleResyncFailed;
        }

        Debug.LogError($"[NetworkSyncValidator] Resync failed: {error}");

        // Fire public event for UI
        OnResyncFailed?.Invoke(error);

        if (showDesyncAlert && FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Resync failed!\nGame state may be inconsistent.",
                GameColorPalette.Damage
            );
        }
    }

    private void HandleDesyncDetectedInternal(int round, int remoteChecksum, int remoteRngCallCount)
    {
        Debug.LogError($"[NetworkSyncValidator] DESYNC at round {round}!");
        Debug.LogError(
            $"  Local:  {_lastLocalSnapshot.checksum:X8} ({_lastLocalSnapshot.rngCallCount} RNG calls)"
        );
        Debug.LogError($"  Remote: {remoteChecksum:X8} ({remoteRngCallCount} RNG calls)");

        string localDump = GameStateChecksum.GenerateDebugDump(_lastLocalSnapshot);
        Debug.LogError($"[NetworkSyncValidator] Local state:\n{localDump}");

        OnDesyncDetected?.Invoke(round, _lastLocalSnapshot, _lastRemoteSnapshot);

        if (showDesyncAlert && FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                $"DESYNC DETECTED (Round {round})\nLocal: {_lastLocalSnapshot.checksum:X8}\nRemote: {remoteChecksum:X8}",
                GameColorPalette.Damage
            );
        }

        if (autoSaveDumps)
        {
            SaveDesyncDump(round);
        }

        if (pauseOnDesync)
        {
            Debug.LogWarning(
                "[NetworkSyncValidator] Pausing for debugging. Press Play to continue."
            );
            Debug.Break();
        }
    }

    private void SaveDesyncDump(int round)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"desync_r{round}_{timestamp}";
            string path = Path.Combine(Application.persistentDataPath, $"{filename}.txt");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== DESYNC DUMP - Round {round} ===");
            sb.AppendLine($"Timestamp: {DateTime.Now}");
            sb.AppendLine($"Local Role: {NetworkRoleHelper.LocalRole}");
            sb.AppendLine($"Is Guest: {NetworkRoleHelper.IsGuest}");
            sb.AppendLine();

            sb.AppendLine("--- LOCAL STATE ---");
            sb.AppendLine(GameStateChecksum.GenerateDebugDump(_lastLocalSnapshot));
            sb.AppendLine();

            sb.AppendLine("--- COMPARISON ---");
            sb.AppendLine($"Local Checksum:  {_lastLocalSnapshot.checksum:X8}");
            sb.AppendLine($"Remote Checksum: {_lastRemoteSnapshot.checksum:X8}");
            sb.AppendLine($"Local RNG Calls:  {_lastLocalSnapshot.rngCallCount}");
            sb.AppendLine($"Remote RNG Calls: {_lastRemoteSnapshot.rngCallCount}");

            if (_lastLocalSnapshot.rngCallCount != _lastRemoteSnapshot.rngCallCount)
            {
                sb.AppendLine();
                sb.AppendLine(">>> RNG CALL COUNT MISMATCH - Likely cause of desync!");
                sb.AppendLine(">>> Check for UnityEngine.Random usage instead of DeterministicRng");
            }
            sb.AppendLine();

            if (ActionLog.Instance != null)
            {
                sb.AppendLine("--- ACTION LOG ---");
                sb.AppendLine(ActionLog.Instance.GenerateReport());
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[NetworkSyncValidator] Saved dump to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkSyncValidator] Failed to save dump: {ex.Message}");
        }
    }

    // --- Serialization helpers ---

    public static byte[] SerializeChecksumPayload(int round, int checksum, int rngCallCount)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(round);
        bw.Write(checksum);
        bw.Write(rngCallCount);
        return ms.ToArray();
    }

    public static bool TryDeserializeChecksumPayload(
        byte[] payload,
        out int round,
        out int checksum,
        out int rngCallCount
    )
    {
        round = 0;
        checksum = 0;
        rngCallCount = 0;

        if (payload == null || payload.Length < 12)
            return false;

        try
        {
            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms);
            round = br.ReadInt32();
            checksum = br.ReadInt32();
            rngCallCount = br.ReadInt32();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
