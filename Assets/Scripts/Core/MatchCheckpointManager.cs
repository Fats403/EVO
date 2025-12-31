using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Data about an active match that can be resumed.
/// Stored locally in PlayerPrefs for persistence.
/// </summary>
[Serializable]
public class ActiveMatchInfo
{
    public string matchId;
    public string opponentSteamId;
    public string opponentName;
    public int lastCheckpointRound;
    public long timestampTicks; // DateTime.UtcNow.Ticks
    public bool wasHost;

    public DateTime Timestamp => new DateTime(timestampTicks, DateTimeKind.Utc);

    /// <summary>
    /// Returns true if this match is still valid (less than 24 hours old).
    /// </summary>
    public bool IsValid => (DateTime.UtcNow - Timestamp).TotalHours < 24;
}

/// <summary>
/// Manages game state checkpoints in Firestore for reconnection recovery.
/// Saves validated game state at round end when checksums match.
/// </summary>
public class MatchCheckpointManager : MonoBehaviour
{
    public static MatchCheckpointManager Instance { get; private set; }

    private const string ActiveMatchKey = "EVO_ActiveMatch";

    [Header("Settings")]
    [Tooltip("Maximum number of checkpoints to keep per match")]
    [SerializeField]
    private int maxCheckpointsPerMatch = 5;

    /// <summary>
    /// The unique match ID for the current session. Generated at match start.
    /// </summary>
    public string CurrentMatchId { get; private set; }

    /// <summary>
    /// Last successfully saved checkpoint round.
    /// </summary>
    public int LastCheckpointRound { get; private set; }

    /// <summary>
    /// The last known good game state that was validated and saved.
    /// </summary>
    public GameStateSerialization.FullGameState? LastCheckpoint { get; private set; }

    /// <summary>
    /// Info about the active match (for rejoin functionality).
    /// </summary>
    public ActiveMatchInfo ActiveMatch { get; private set; }

    // Events
    public event Action<int> OnCheckpointSaved;
    public event Action<GameStateSerialization.FullGameState> OnCheckpointLoaded;

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
        // Load any existing active match on startup
        LoadActiveMatchFromPrefs();
    }

    /// <summary>
    /// Loads any saved active match info from PlayerPrefs.
    /// </summary>
    public void LoadActiveMatchFromPrefs()
    {
        string json = PlayerPrefs.GetString(ActiveMatchKey, "");
        if (string.IsNullOrEmpty(json))
        {
            ActiveMatch = null;
            return;
        }

        try
        {
            ActiveMatch = JsonUtility.FromJson<ActiveMatchInfo>(json);

            // Clear if expired
            if (ActiveMatch != null && !ActiveMatch.IsValid)
            {
                Debug.Log("[MatchCheckpointManager] Active match expired, clearing.");
                ClearActiveMatch();
            }
            else if (ActiveMatch != null)
            {
                Debug.Log(
                    $"[MatchCheckpointManager] Loaded active match: {ActiveMatch.matchId} with {ActiveMatch.opponentName} at round {ActiveMatch.lastCheckpointRound}"
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MatchCheckpointManager] Failed to load active match: {e.Message}");
            ActiveMatch = null;
        }
    }

    /// <summary>
    /// Saves active match info to PlayerPrefs.
    /// Call this when a match starts.
    /// </summary>
    public void SaveActiveMatch(string opponentSteamId, string opponentName, bool wasHost)
    {
        if (string.IsNullOrEmpty(CurrentMatchId))
        {
            Debug.LogWarning("[MatchCheckpointManager] Cannot save active match - no match ID.");
            return;
        }

        ActiveMatch = new ActiveMatchInfo
        {
            matchId = CurrentMatchId,
            opponentSteamId = opponentSteamId,
            opponentName = opponentName,
            lastCheckpointRound = LastCheckpointRound,
            timestampTicks = DateTime.UtcNow.Ticks,
            wasHost = wasHost,
        };

        string json = JsonUtility.ToJson(ActiveMatch);
        PlayerPrefs.SetString(ActiveMatchKey, json);
        PlayerPrefs.Save();

        Debug.Log(
            $"[MatchCheckpointManager] Saved active match: {CurrentMatchId} with {opponentName}"
        );
    }

    /// <summary>
    /// Updates the checkpoint round in the saved active match.
    /// </summary>
    public void UpdateActiveMatchRound(int round)
    {
        if (ActiveMatch == null)
            return;

        ActiveMatch.lastCheckpointRound = round;
        ActiveMatch.timestampTicks = DateTime.UtcNow.Ticks;

        string json = JsonUtility.ToJson(ActiveMatch);
        PlayerPrefs.SetString(ActiveMatchKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Clears the active match (call when match ends normally or is abandoned).
    /// </summary>
    public void ClearActiveMatch()
    {
        ActiveMatch = null;
        PlayerPrefs.DeleteKey(ActiveMatchKey);
        PlayerPrefs.Save();
        Debug.Log("[MatchCheckpointManager] Cleared active match.");
    }

    /// <summary>
    /// Returns true if there's a valid active match that can be resumed.
    /// </summary>
    public bool HasActiveMatch => ActiveMatch != null && ActiveMatch.IsValid;

    /// <summary>
    /// Initializes the checkpoint manager for a new match.
    /// Call this when starting a networked game.
    /// </summary>
    public void InitializeForMatch(
        string hostId,
        string guestId,
        string opponentName = null,
        bool isHost = true
    )
    {
        Debug.Log(
            $"[MatchCheckpointManager] InitializeForMatch called with hostId={hostId}, guestId={guestId}, isHost={isHost}"
        );

        // Generate a deterministic match ID from both player IDs and timestamp
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string combined = $"{hostId}_{guestId}_{timestamp}";
        CurrentMatchId = ComputeMatchId(combined);
        LastCheckpointRound = 0;
        LastCheckpoint = null;

        // Save active match info for rejoin functionality
        string opponentId = isHost ? guestId : hostId;
        SaveActiveMatch(opponentId, opponentName ?? "Opponent", isHost);

        Debug.Log($"[MatchCheckpointManager] Initialized for match: {CurrentMatchId}");
        Debug.Log(
            $"[MatchCheckpointManager] Firestore available: {FirebaseManager.Instance?.Db != null}"
        );
    }

    /// <summary>
    /// Initializes the checkpoint manager to resume an existing match.
    /// </summary>
    public void InitializeForResume(ActiveMatchInfo matchInfo)
    {
        if (matchInfo == null)
        {
            Debug.LogWarning("[MatchCheckpointManager] Cannot resume - no match info.");
            return;
        }

        CurrentMatchId = matchInfo.matchId;
        LastCheckpointRound = matchInfo.lastCheckpointRound;
        ActiveMatch = matchInfo;

        Debug.Log(
            $"[MatchCheckpointManager] Resuming match: {CurrentMatchId} at round {LastCheckpointRound}"
        );
    }

    private string ComputeMatchId(string input)
    {
        // Simple hash for match ID
        int hash = input.GetHashCode();
        return $"match_{Math.Abs(hash):X8}";
    }

    /// <summary>
    /// Saves the current game state as a checkpoint after successful checksum validation.
    /// Call this at round end when both clients confirm matching checksums.
    /// </summary>
    public async Task SaveCheckpointAsync(int round, int checksum)
    {
        Debug.Log(
            $"[MatchCheckpointManager] SaveCheckpointAsync called for round {round}, checksum {checksum:X8}"
        );

        if (string.IsNullOrEmpty(CurrentMatchId))
        {
            Debug.LogWarning(
                "[MatchCheckpointManager] No match ID set, cannot save checkpoint. Was InitializeForMatch called?"
            );
            return;
        }

        Debug.Log($"[MatchCheckpointManager] CurrentMatchId: {CurrentMatchId}");

        if (FirebaseManager.Instance?.Db == null)
        {
            Debug.LogWarning(
                "[MatchCheckpointManager] Firestore not available, saving locally only."
            );
            SaveCheckpointLocally(round);
            return;
        }

        Debug.Log($"[MatchCheckpointManager] Firestore available, attempting to save...");

        try
        {
            var gameState = GameStateSerialization.CaptureState();
            var serializedState = GameStateSerialization.Serialize(gameState);

            // Store checkpoint data
            var checkpointData = new Dictionary<string, object>
            {
                { "round", round },
                { "checksum", checksum },
                { "stateData", Convert.ToBase64String(serializedState) },
                { "timestamp", FieldValue.ServerTimestamp },
                { "p1Score", gameState.p1Score },
                { "p2Score", gameState.p2Score },
                { "era", gameState.era.ToString() },
                { "weather", gameState.currentWeather.ToString() },
            };

            // Save to Firestore: matches/{matchId}/checkpoints/{round}
            var docRef = FirebaseManager
                .Instance.Db.Collection("matches")
                .Document(CurrentMatchId)
                .Collection("checkpoints")
                .Document(round.ToString());

            await docRef.SetAsync(checkpointData);

            LastCheckpointRound = round;
            LastCheckpoint = gameState;

            // Update the active match with new round
            UpdateActiveMatchRound(round);

            Debug.Log(
                $"[MatchCheckpointManager] Saved checkpoint for round {round}, checksum: {checksum:X8}"
            );

            // Clean up old checkpoints
            await CleanupOldCheckpointsAsync();

            OnCheckpointSaved?.Invoke(round);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchCheckpointManager] Failed to save checkpoint: {e.Message}");
            // Fall back to local storage
            SaveCheckpointLocally(round);
        }
    }

    /// <summary>
    /// Saves checkpoint to local memory only (fallback when Firestore unavailable).
    /// </summary>
    private void SaveCheckpointLocally(int round)
    {
        LastCheckpoint = GameStateSerialization.CaptureState();
        LastCheckpointRound = round;
        Debug.Log($"[MatchCheckpointManager] Saved checkpoint locally for round {round}");
        OnCheckpointSaved?.Invoke(round);
    }

    /// <summary>
    /// Loads the most recent checkpoint from Firestore.
    /// </summary>
    public async Task<GameStateSerialization.FullGameState?> LoadLatestCheckpointAsync()
    {
        if (string.IsNullOrEmpty(CurrentMatchId))
        {
            Debug.LogWarning("[MatchCheckpointManager] No match ID set.");
            return LastCheckpoint;
        }

        if (FirebaseManager.Instance?.Db == null)
        {
            Debug.LogWarning(
                "[MatchCheckpointManager] Firestore not available, using local checkpoint."
            );
            return LastCheckpoint;
        }

        try
        {
            var query = FirebaseManager
                .Instance.Db.Collection("matches")
                .Document(CurrentMatchId)
                .Collection("checkpoints")
                .OrderByDescending("round")
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync();

            if (snapshot.Count == 0)
            {
                Debug.Log("[MatchCheckpointManager] No checkpoints found in Firestore.");
                return LastCheckpoint;
            }

            var doc = snapshot.Documents.First();
            var stateDataBase64 = doc.GetValue<string>("stateData");
            var stateBytes = Convert.FromBase64String(stateDataBase64);
            var gameState = GameStateSerialization.Deserialize(stateBytes);

            LastCheckpoint = gameState;
            LastCheckpointRound = doc.GetValue<int>("round");

            Debug.Log(
                $"[MatchCheckpointManager] Loaded checkpoint from round {LastCheckpointRound}"
            );

            OnCheckpointLoaded?.Invoke(gameState);
            return gameState;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchCheckpointManager] Failed to load checkpoint: {e.Message}");
            return LastCheckpoint;
        }
    }

    /// <summary>
    /// Loads a specific checkpoint by round number.
    /// </summary>
    public async Task<GameStateSerialization.FullGameState?> LoadCheckpointAsync(int round)
    {
        if (string.IsNullOrEmpty(CurrentMatchId) || FirebaseManager.Instance?.Db == null)
        {
            return LastCheckpoint?.round == round ? LastCheckpoint : null;
        }

        try
        {
            var docRef = FirebaseManager
                .Instance.Db.Collection("matches")
                .Document(CurrentMatchId)
                .Collection("checkpoints")
                .Document(round.ToString());

            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                Debug.Log($"[MatchCheckpointManager] No checkpoint found for round {round}.");
                return null;
            }

            var stateDataBase64 = snapshot.GetValue<string>("stateData");
            var stateBytes = Convert.FromBase64String(stateDataBase64);
            var gameState = GameStateSerialization.Deserialize(stateBytes);

            Debug.Log($"[MatchCheckpointManager] Loaded checkpoint for round {round}");
            return gameState;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[MatchCheckpointManager] Failed to load checkpoint for round {round}: {e.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Removes old checkpoints beyond the max limit.
    /// </summary>
    private async Task CleanupOldCheckpointsAsync()
    {
        if (FirebaseManager.Instance?.Db == null)
            return;

        try
        {
            var query = FirebaseManager
                .Instance.Db.Collection("matches")
                .Document(CurrentMatchId)
                .Collection("checkpoints")
                .OrderByDescending("round");

            var snapshot = await query.GetSnapshotAsync();

            if (snapshot.Count <= maxCheckpointsPerMatch)
                return;

            // Delete checkpoints beyond the limit
            int deleteCount = 0;
            var docsToDelete = snapshot.Documents.Skip(maxCheckpointsPerMatch).ToList();
            foreach (var doc in docsToDelete)
            {
                await doc.Reference.DeleteAsync();
                deleteCount++;
            }

            if (deleteCount > 0)
            {
                Debug.Log($"[MatchCheckpointManager] Cleaned up {deleteCount} old checkpoints.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"[MatchCheckpointManager] Failed to cleanup old checkpoints: {e.Message}"
            );
        }
    }

    /// <summary>
    /// Updates match metadata in Firestore (useful for match history).
    /// </summary>
    public async Task UpdateMatchMetadataAsync(
        string hostId,
        string guestId,
        string hostName,
        string guestName
    )
    {
        if (string.IsNullOrEmpty(CurrentMatchId) || FirebaseManager.Instance?.Db == null)
            return;

        try
        {
            var matchData = new Dictionary<string, object>
            {
                { "hostId", hostId },
                { "guestId", guestId },
                { "hostName", hostName ?? "Player 1" },
                { "guestName", guestName ?? "Player 2" },
                { "startedAt", FieldValue.ServerTimestamp },
                { "status", "active" },
            };

            var docRef = FirebaseManager.Instance.Db.Collection("matches").Document(CurrentMatchId);

            await docRef.SetAsync(matchData, SetOptions.MergeAll);
            Debug.Log($"[MatchCheckpointManager] Updated match metadata for {CurrentMatchId}");
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"[MatchCheckpointManager] Failed to update match metadata: {e.Message}"
            );
        }
    }

    /// <summary>
    /// Marks the match as complete in Firestore and clears active match.
    /// </summary>
    public async Task CompleteMatchAsync(string winnerId, string reason)
    {
        // Clear active match since game ended normally
        ClearActiveMatch();

        if (string.IsNullOrEmpty(CurrentMatchId) || FirebaseManager.Instance?.Db == null)
            return;

        try
        {
            var matchData = new Dictionary<string, object>
            {
                { "status", "complete" },
                { "winnerId", winnerId ?? "" },
                { "endReason", reason ?? "normal" },
                { "endedAt", FieldValue.ServerTimestamp },
                { "finalRound", GameManager.Instance?.currentRound ?? 0 },
            };

            var docRef = FirebaseManager.Instance.Db.Collection("matches").Document(CurrentMatchId);

            await docRef.SetAsync(matchData, SetOptions.MergeAll);
            Debug.Log($"[MatchCheckpointManager] Match {CurrentMatchId} marked as complete.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MatchCheckpointManager] Failed to complete match: {e.Message}");
        }
    }

    /// <summary>
    /// Clears local checkpoint data (call when leaving a match).
    /// </summary>
    public void ClearLocalData()
    {
        CurrentMatchId = null;
        LastCheckpointRound = 0;
        LastCheckpoint = null;
    }
}
