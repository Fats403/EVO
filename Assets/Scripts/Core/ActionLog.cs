using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Records all game actions for debugging desyncs and supporting future reconnect functionality.
/// Each action is timestamped with the round and sequence number for replay purposes.
/// </summary>
public class ActionLog : MonoBehaviour
{
    public static ActionLog Instance { get; private set; }

    [Serializable]
    public struct ActionEntry
    {
        public int sequenceNumber;
        public int round;
        public float gameTime;
        public GameActionType actionType;
        public SlotOwner owner;
        public string cardId;
        public int slotIndex;
        public List<int> targetSlotIndices;
        public bool wasLocal; // True if this action originated locally
        public int checksumAfter; // Checksum after this action was processed (if computed)
    }

    [Serializable]
    public struct RoundEndEntry
    {
        public int round;
        public int checksumBefore; // Checksum at start of resolve phase
        public int checksumAfter; // Checksum at end of round
        public int p1Score;
        public int p2Score;
        public int rngCallCount;
    }

    [Header("Settings")]
    [Tooltip("Maximum number of actions to keep in memory")]
    public int maxLogSize = 1000;

    [Tooltip("Auto-save log to disk when desync is detected")]
    public bool autoSaveOnDesync = true;

    private readonly List<ActionEntry> _actionLog = new();
    private readonly List<RoundEndEntry> _roundEndLog = new();
    private int _sequenceNumber = 0;

    // Events for desync detection
    public event Action<int, int> OnChecksumMismatch; // local, remote

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

    /// <summary>
    /// Clears the action log. Call at the start of a new game.
    /// </summary>
    public void Clear()
    {
        _actionLog.Clear();
        _roundEndLog.Clear();
        _sequenceNumber = 0;
    }

    /// <summary>
    /// Records a game action.
    /// </summary>
    public void LogAction(GameAction action, bool wasLocal, int checksumAfter = 0)
    {
        if (action == null)
            return;

        var entry = new ActionEntry
        {
            sequenceNumber = _sequenceNumber++,
            round = GameManager.Instance?.currentRound ?? 0,
            gameTime = Time.time,
            actionType = action.type,
            owner = action.owner,
            cardId = action.cardId ?? "",
            slotIndex = action.slotIndex,
            targetSlotIndices =
                action.targetSlotIndices != null
                    ? new List<int>(action.targetSlotIndices)
                    : new List<int>(),
            wasLocal = wasLocal,
            checksumAfter = checksumAfter,
        };

        _actionLog.Add(entry);

        // Trim if over max size
        while (_actionLog.Count > maxLogSize)
        {
            _actionLog.RemoveAt(0);
        }

        Debug.Log(
            $"[ActionLog] #{entry.sequenceNumber} R{entry.round} {entry.actionType} by {entry.owner}"
                + $"{(entry.actionType != GameActionType.Pass ? $" card={entry.cardId}" : "")}"
                + $" (local={wasLocal})"
        );
    }

    /// <summary>
    /// Records a round end with checksums for validation.
    /// </summary>
    public void LogRoundEnd(
        int round,
        int checksumBefore,
        int checksumAfter,
        int p1Score,
        int p2Score,
        int rngCallCount
    )
    {
        var entry = new RoundEndEntry
        {
            round = round,
            checksumBefore = checksumBefore,
            checksumAfter = checksumAfter,
            p1Score = p1Score,
            p2Score = p2Score,
            rngCallCount = rngCallCount,
        };

        _roundEndLog.Add(entry);

        Debug.Log(
            $"[ActionLog] Round {round} ended - Checksum: {checksumAfter:X8}, P1: {p1Score}, P2: {p2Score}, RNG calls: {rngCallCount}"
        );
    }

    /// <summary>
    /// Returns all actions for a specific round.
    /// </summary>
    public List<ActionEntry> GetActionsForRound(int round)
    {
        return _actionLog.FindAll(e => e.round == round);
    }

    /// <summary>
    /// Returns the last N actions.
    /// </summary>
    public List<ActionEntry> GetRecentActions(int count)
    {
        int start = Mathf.Max(0, _actionLog.Count - count);
        return _actionLog.GetRange(start, _actionLog.Count - start);
    }

    /// <summary>
    /// Gets all round end entries.
    /// </summary>
    public IReadOnlyList<RoundEndEntry> GetRoundEndLog() => _roundEndLog;

    /// <summary>
    /// Validates a remote checksum against our local state.
    /// Returns true if they match.
    /// </summary>
    public bool ValidateChecksum(int round, int remoteChecksum)
    {
        // Find our round end entry
        var localEntry = _roundEndLog.Find(e => e.round == round);
        if (localEntry.round != round)
        {
            Debug.LogWarning($"[ActionLog] No local checksum found for round {round}");
            return true; // Can't validate without local data
        }

        if (localEntry.checksumAfter != remoteChecksum)
        {
            Debug.LogError(
                $"[ActionLog] DESYNC DETECTED at round {round}! "
                    + $"Local: {localEntry.checksumAfter:X8}, Remote: {remoteChecksum:X8}"
            );

            OnChecksumMismatch?.Invoke(localEntry.checksumAfter, remoteChecksum);

            if (autoSaveOnDesync)
            {
                SaveLogToDisk($"desync_round{round}_{DateTime.Now:yyyyMMdd_HHmmss}");
            }

            return false;
        }

        Debug.Log($"[ActionLog] Round {round} checksum validated: {remoteChecksum:X8}");
        return true;
    }

    /// <summary>
    /// Generates a full debug report of the action log.
    /// </summary>
    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ACTION LOG REPORT ===");
        sb.AppendLine($"Generated: {DateTime.Now}");
        sb.AppendLine($"Total Actions: {_actionLog.Count}");
        sb.AppendLine($"Total Rounds: {_roundEndLog.Count}");
        sb.AppendLine();

        // Round summaries
        sb.AppendLine("--- ROUND SUMMARIES ---");
        foreach (var round in _roundEndLog)
        {
            var actionsInRound = _actionLog.FindAll(a => a.round == round.round);
            sb.AppendLine(
                $"Round {round.round}: {actionsInRound.Count} actions, "
                    + $"Checksum: {round.checksumAfter:X8}, "
                    + $"Score: P1={round.p1Score} P2={round.p2Score}, "
                    + $"RNG calls: {round.rngCallCount}"
            );
        }
        sb.AppendLine();

        // Detailed action log
        sb.AppendLine("--- ACTION DETAILS ---");
        foreach (var action in _actionLog)
        {
            sb.Append($"[{action.sequenceNumber:D4}] R{action.round} t={action.gameTime:F2}s ");
            sb.Append($"{action.owner} {action.actionType}");

            if (action.actionType == GameActionType.PlayCreature)
            {
                sb.Append($" {action.cardId} -> slot {action.slotIndex}");
            }
            else if (action.actionType == GameActionType.PlayEffect)
            {
                sb.Append(
                    $" {action.cardId} -> targets [{string.Join(",", action.targetSlotIndices)}]"
                );
            }

            sb.Append(action.wasLocal ? " (LOCAL)" : " (REMOTE)");

            if (action.checksumAfter != 0)
            {
                sb.Append($" [cs:{action.checksumAfter:X8}]");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Saves the action log to disk for post-mortem analysis.
    /// </summary>
    public void SaveLogToDisk(string filename)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, $"{filename}.txt");
            string report = GenerateReport();

            // Also append current game state dump
            var snapshot = GameStateChecksum.ComputeChecksum();
            report += "\n\n" + GameStateChecksum.GenerateDebugDump(snapshot);

            File.WriteAllText(path, report);
            Debug.Log($"[ActionLog] Saved to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ActionLog] Failed to save log: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates JSON serialization of the action log for network transmission.
    /// </summary>
    public string SerializeForNetwork()
    {
        // Simple JSON-like format for recent actions
        var recentActions = GetRecentActions(50);
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < recentActions.Count; i++)
        {
            if (i > 0)
                sb.Append(",");
            var a = recentActions[i];
            sb.Append(
                $"{{\"seq\":{a.sequenceNumber},\"r\":{a.round},\"t\":\"{a.actionType}\","
                    + $"\"o\":\"{a.owner}\",\"c\":\"{a.cardId}\",\"s\":{a.slotIndex}}}"
            );
        }
        sb.Append("]");
        return sb.ToString();
    }
}
