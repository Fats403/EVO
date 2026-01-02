using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Generates deterministic checksums of game state for detecting desync between networked clients.
/// Call ComputeChecksum() at the end of each round and compare between host and guest.
/// </summary>
public static class GameStateChecksum
{
    /// <summary>
    /// A snapshot of game state used for checksum computation and debugging.
    /// </summary>
    public struct GameStateSnapshot
    {
        public int round;
        public Era era;
        public int p1Score;
        public int p2Score;
        public int foodPileCount;
        public WeatherType weather;
        public int rngCallCount; // Track how many RNG calls have been made
        public List<CreatureSnapshot> creatures;
        public int checksum;
    }

    public struct CreatureSnapshot
    {
        public string cardId;
        public SlotOwner owner;
        public int slotIndex;
        public int currentHealth;
        public int maxHealth;
        public int body;
        public int speed;
        public int eaten;
        public bool isDying;
        public List<string> traitNames;
        public Dictionary<StatusTag, int> statuses;
    }

    private static int _rngCallCount = 0;

    /// <summary>
    /// Call this every time the deterministic RNG is used to track call count.
    /// </summary>
    public static void IncrementRngCallCount()
    {
        _rngCallCount++;
    }

    /// <summary>
    /// Reset RNG call counter (call at game start).
    /// </summary>
    public static void ResetRngCallCount()
    {
        _rngCallCount = 0;
    }

    public static int GetRngCallCount() => _rngCallCount;

    /// <summary>
    /// Computes a deterministic checksum of the current game state.
    /// Both clients should produce identical checksums if in sync.
    /// </summary>
    public static GameStateSnapshot ComputeChecksum()
    {
        var snapshot = new GameStateSnapshot
        {
            round = GameManager.Instance?.currentRound ?? 0,
            era = GameManager.Instance?.currentEra ?? Era.Triassic,
            p1Score = ScoreManager.player1,
            p2Score = ScoreManager.player2,
            foodPileCount = GameManager.Instance?.foodPile?.count ?? 0,
            weather = WeatherManager.Instance?.CurrentWeather ?? WeatherType.Clear,
            rngCallCount = _rngCallCount,
            creatures = new List<CreatureSnapshot>(),
        };

        // Get all creatures in a DETERMINISTIC order (by slot index)
        var slotLookup = DeterministicHelpers.GetSlotIndexLookup();
        var sortedCreatures = DeterministicHelpers
            .GetAllCreaturesInSlotOrder()
            .Where(c => !c.isDying)
            .ToList();

        foreach (var creature in sortedCreatures)
        {
            var creatureSnap = new CreatureSnapshot
            {
                cardId = creature.data?.cardId ?? "unknown",
                owner = creature.owner,
                slotIndex = DeterministicHelpers.GetSlotIndex(creature, slotLookup),
                currentHealth = creature.currentHealth,
                maxHealth = creature.maxHealth,
                body = creature.body,
                speed = creature.speed,
                eaten = creature.eaten,
                isDying = creature.isDying,
                traitNames =
                    creature
                        .traits?.Where(t => t != null)
                        .Select(t => t.traitName ?? t.GetType().Name)
                        .OrderBy(n => n) // Sort for determinism
                        .ToList() ?? new List<string>(),
                statuses = new Dictionary<StatusTag, int>(),
            };

            // Capture statuses in deterministic order
            foreach (var tag in creature.GetActiveStatusTags().OrderBy(t => (int)t))
            {
                creatureSnap.statuses[tag] = creature.GetStatus(tag);
            }

            snapshot.creatures.Add(creatureSnap);
        }

        // Compute the checksum hash
        snapshot.checksum = ComputeHash(snapshot);
        return snapshot;
    }

    private static int ComputeHash(GameStateSnapshot snapshot)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + snapshot.round;
            hash = hash * 31 + (int)snapshot.era;
            hash = hash * 31 + snapshot.p1Score;
            hash = hash * 31 + snapshot.p2Score;
            hash = hash * 31 + snapshot.foodPileCount;
            hash = hash * 31 + (int)snapshot.weather;
            hash = hash * 31 + snapshot.rngCallCount;

            foreach (var c in snapshot.creatures)
            {
                // CRITICAL: Use deterministic string hash, NOT String.GetHashCode()
                // which is randomized per-process in .NET for security.
                hash = hash * 31 + DeterministicStringHash(c.cardId);
                hash = hash * 31 + (int)c.owner;
                hash = hash * 31 + c.slotIndex;
                hash = hash * 31 + c.currentHealth;
                hash = hash * 31 + c.maxHealth;
                hash = hash * 31 + c.body;
                hash = hash * 31 + c.speed;
                hash = hash * 31 + c.eaten;
                hash = hash * 31 + (c.isDying ? 1 : 0);

                foreach (var trait in c.traitNames)
                {
                    hash = hash * 31 + DeterministicStringHash(trait);
                }

                foreach (var kvp in c.statuses.OrderBy(k => (int)k.Key))
                {
                    hash = hash * 31 + (int)kvp.Key;
                    hash = hash * 31 + kvp.Value;
                }
            }

            return hash;
        }
    }

    /// <summary>
    /// Computes a deterministic hash code for a string that is consistent
    /// across different machines and process runs. Uses FNV-1a algorithm.
    /// </summary>
    private static int DeterministicStringHash(string str)
    {
        if (string.IsNullOrEmpty(str))
            return 0;
        unchecked
        {
            // FNV-1a hash algorithm - deterministic and fast
            const int fnvOffsetBasis = unchecked((int)2166136261);
            const int fnvPrime = 16777619;

            int hash = fnvOffsetBasis;
            foreach (char c in str)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return hash;
        }
    }

    /// <summary>
    /// Generates a human-readable debug string of the current game state.
    /// Useful for comparing states between clients when a desync is detected.
    /// </summary>
    public static string GenerateDebugDump(GameStateSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== GAME STATE DUMP (Round {snapshot.round}) ===");
        sb.AppendLine($"Checksum: {snapshot.checksum:X8}");
        sb.AppendLine($"Era: {snapshot.era}");
        sb.AppendLine($"Weather: {snapshot.weather}");
        sb.AppendLine($"P1 Score: {snapshot.p1Score}");
        sb.AppendLine($"P2 Score: {snapshot.p2Score}");
        sb.AppendLine($"Food Pile: {snapshot.foodPileCount}");
        sb.AppendLine($"RNG Calls: {snapshot.rngCallCount}");
        sb.AppendLine($"Creatures ({snapshot.creatures.Count}):");

        foreach (var c in snapshot.creatures)
        {
            sb.AppendLine($"  [{c.slotIndex}] {c.cardId} ({c.owner})");
            sb.AppendLine(
                $"      HP: {c.currentHealth}/{c.maxHealth}, Body: {c.body}, Speed: {c.speed}, Eaten: {c.eaten}"
            );
            if (c.traitNames.Count > 0)
                sb.AppendLine($"      Traits: {string.Join(", ", c.traitNames)}");
            if (c.statuses.Count > 0)
                sb.AppendLine(
                    $"      Statuses: {string.Join(", ", c.statuses.Select(kvp => $"{kvp.Key}x{kvp.Value}"))}"
                );
        }

        return sb.ToString();
    }

    /// <summary>
    /// Compares two snapshots and returns a detailed diff of what's different.
    /// </summary>
    public static string ComparSnapshots(GameStateSnapshot local, GameStateSnapshot remote)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== DESYNC ANALYSIS ===");

        if (local.checksum == remote.checksum)
        {
            sb.AppendLine("Checksums match - no desync detected.");
            return sb.ToString();
        }

        sb.AppendLine($"CHECKSUM MISMATCH: Local={local.checksum:X8}, Remote={remote.checksum:X8}");
        sb.AppendLine();

        // Compare high-level state
        if (local.round != remote.round)
            sb.AppendLine($"DIFF Round: Local={local.round}, Remote={remote.round}");
        if (local.era != remote.era)
            sb.AppendLine($"DIFF Era: Local={local.era}, Remote={remote.era}");
        if (local.p1Score != remote.p1Score)
            sb.AppendLine($"DIFF P1 Score: Local={local.p1Score}, Remote={remote.p1Score}");
        if (local.p2Score != remote.p2Score)
            sb.AppendLine($"DIFF P2 Score: Local={local.p2Score}, Remote={remote.p2Score}");
        if (local.foodPileCount != remote.foodPileCount)
            sb.AppendLine($"DIFF Food: Local={local.foodPileCount}, Remote={remote.foodPileCount}");
        if (local.weather != remote.weather)
            sb.AppendLine($"DIFF Weather: Local={local.weather}, Remote={remote.weather}");
        if (local.rngCallCount != remote.rngCallCount)
            sb.AppendLine(
                $"DIFF RNG Calls: Local={local.rngCallCount}, Remote={remote.rngCallCount} (LIKELY CAUSE!)"
            );

        // Compare creature counts
        if (local.creatures.Count != remote.creatures.Count)
        {
            sb.AppendLine(
                $"DIFF Creature Count: Local={local.creatures.Count}, Remote={remote.creatures.Count}"
            );
        }

        // Compare creatures by slot
        var localBySlot = local.creatures.ToDictionary(c => c.slotIndex);
        var remoteBySlot = remote.creatures.ToDictionary(c => c.slotIndex);

        var allSlots = localBySlot.Keys.Union(remoteBySlot.Keys).OrderBy(x => x);
        foreach (var slot in allSlots)
        {
            bool hasLocal = localBySlot.TryGetValue(slot, out var localC);
            bool hasRemote = remoteBySlot.TryGetValue(slot, out var remoteC);

            if (hasLocal && !hasRemote)
            {
                sb.AppendLine($"DIFF Slot {slot}: Local has {localC.cardId}, Remote is empty");
            }
            else if (!hasLocal && hasRemote)
            {
                sb.AppendLine($"DIFF Slot {slot}: Local is empty, Remote has {remoteC.cardId}");
            }
            else if (hasLocal && hasRemote)
            {
                CompareCreatures(sb, slot, localC, remoteC);
            }
        }

        return sb.ToString();
    }

    private static void CompareCreatures(
        StringBuilder sb,
        int slot,
        CreatureSnapshot local,
        CreatureSnapshot remote
    )
    {
        var diffs = new List<string>();

        if (local.cardId != remote.cardId)
            diffs.Add($"CardId: {local.cardId} vs {remote.cardId}");
        if (local.owner != remote.owner)
            diffs.Add($"Owner: {local.owner} vs {remote.owner}");
        if (local.currentHealth != remote.currentHealth)
            diffs.Add($"HP: {local.currentHealth} vs {remote.currentHealth}");
        if (local.body != remote.body)
            diffs.Add($"Body: {local.body} vs {remote.body}");
        if (local.speed != remote.speed)
            diffs.Add($"Speed: {local.speed} vs {remote.speed}");
        if (local.eaten != remote.eaten)
            diffs.Add($"Eaten: {local.eaten} vs {remote.eaten}");

        // Compare traits
        var localTraits = string.Join(",", local.traitNames);
        var remoteTraits = string.Join(",", remote.traitNames);
        if (localTraits != remoteTraits)
            diffs.Add($"Traits: [{localTraits}] vs [{remoteTraits}]");

        // Compare statuses
        var localStatuses = string.Join(
            ",",
            local.statuses.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")
        );
        var remoteStatuses = string.Join(
            ",",
            remote.statuses.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")
        );
        if (localStatuses != remoteStatuses)
            diffs.Add($"Statuses: [{localStatuses}] vs [{remoteStatuses}]");

        if (diffs.Count > 0)
        {
            sb.AppendLine($"DIFF Slot {slot} ({local.cardId}):");
            foreach (var diff in diffs)
            {
                sb.AppendLine($"    {diff}");
            }
        }
    }
}
