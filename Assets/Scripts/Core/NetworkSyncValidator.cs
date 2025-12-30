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
    public event Action<int, GameStateChecksum.GameStateSnapshot, GameStateChecksum.GameStateSnapshot> OnDesyncDetected;
    public event Action OnPeerDisconnected;
    public event Action OnPeerReconnected;
    public event Action<float> OnWaitingForPeer; // Passes seconds waited so far

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

    public bool IsPeerDisconnected => _isPeerDisconnected;
    public bool IsWaitingForPeer => _isWaitingForPeer;
    public float SecondsSinceLastMessage => Time.unscaledTime - _lastMessageReceivedTime;

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
            return;

        // Check for disconnect timeout
        float timeSinceMessage = SecondsSinceLastMessage;

        if (!_isPeerDisconnected && timeSinceMessage > disconnectTimeoutSeconds)
        {
            HandlePeerDisconnected();
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

    private void HandlePeerDisconnected()
    {
        if (_isPeerDisconnected)
            return;

        _isPeerDisconnected = true;
        _isWaitingForPeer = true;
        _waitingStartTime = Time.unscaledTime;

        Debug.LogWarning("[NetworkSyncValidator] Peer appears to have disconnected!");

        OnPeerDisconnected?.Invoke();

        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Connection lost...\nWaiting for opponent",
                GameColorPalette.TextWarning
            );
        }
    }

    private void HandlePeerReconnected()
    {
        if (!_isPeerDisconnected)
            return;

        _isPeerDisconnected = false;
        _isWaitingForPeer = false;

        Debug.Log("[NetworkSyncValidator] Peer has reconnected!");

        OnPeerReconnected?.Invoke();

        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ShowGlobalAlert(
                "Opponent reconnected!",
                GameColorPalette.TextPositive
            );
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
        if (phase == GamePhase.End && NetworkSessionStore.IsNetworkedGame)
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
            HandleDesyncDetectedInternal(round, remoteChecksum, remoteRngCallCount);
        }
        else
        {
            Debug.Log($"[NetworkSyncValidator] Round {round} checksums MATCH ✓");
        }
    }

    private void HandleDesyncDetectedInternal(int round, int remoteChecksum, int remoteRngCallCount)
    {
        Debug.LogError($"[NetworkSyncValidator] DESYNC at round {round}!");
        Debug.LogError($"  Local:  {_lastLocalSnapshot.checksum:X8} ({_lastLocalSnapshot.rngCallCount} RNG calls)");
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
            Debug.LogWarning("[NetworkSyncValidator] Pausing for debugging. Press Play to continue.");
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

