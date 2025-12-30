using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Serialization utilities for full game state snapshots.
/// Used for:
/// 1. Reconnect - send full state to reconnecting player
/// 2. Resync - host sends authoritative state to fix desync
/// 3. Replays - save/load complete game states
/// </summary>
public static class GameStateSerialization
{
    [Serializable]
    public struct FullGameState
    {
        public int round;
        public Era era;
        public GamePhase phase;
        public int p1Score;
        public int p2Score;
        public int p1Momentum;
        public int p2Momentum;
        public int foodPileCount;
        public WeatherType currentWeather;
        public WeatherType? lastWeather;
        public int rngSeed;
        public int rngCallCount;
        public List<CreatureState> creatures;
        public List<GlobalEffectState> globalEffects;
    }

    [Serializable]
    public struct CreatureState
    {
        public string cardId;
        public int slotIndex;
        public SlotOwner owner;
        public int currentHealth;
        public int maxHealth;
        public int body;
        public int speed;
        public int eaten;
        public List<string> traitNames;
        public Dictionary<StatusTag, int> statuses;

        // Trait flags
        public bool traitUsedWhirlwind;
        public bool traitUsedBloodthirsty;
        public bool traitUsedUndyingSpirit;
        public bool traitGrantRadiantShield;
        public bool traitGrantEvasiveStealth;
        public bool traitGrantBloodRush;
        public int traitElementalHpBonus;

        // TODO: capture bleed and infection source for perfect replay?
    }

    [Serializable]
    public struct GlobalEffectState
    {
        public string effectTypeName;
        public int remainingRounds;
        public SlotOwner owner;
    }

    /// <summary>
    /// Captures the complete current game state for transmission or storage.
    /// </summary>
    public static FullGameState CaptureState()
    {
        var gm = GameManager.Instance;
        var sm = ScoreManager.Instance;
        var wm = WeatherManager.Instance;
        var rm = ResolutionManager.Instance;

        var state = new FullGameState
        {
            round = gm?.currentRound ?? 1,
            era = gm?.currentEra ?? Era.Triassic,
            phase = gm?.currentPhase ?? GamePhase.Setup,
            p1Score = ScoreManager.player1,
            p2Score = ScoreManager.player2,
            p1Momentum = gm?.p1Momentum ?? 0,
            p2Momentum = gm?.p2Momentum ?? 0,
            foodPileCount = gm?.foodPile?.count ?? 0,
            currentWeather = wm?.CurrentWeather ?? WeatherType.Clear,
            lastWeather = wm?.LastWeather,
            rngSeed = DeterministicRng.Seed,
            rngCallCount = GameStateChecksum.GetRngCallCount(),
            creatures = new List<CreatureState>(),
            globalEffects = new List<GlobalEffectState>(),
        };

        // Capture creatures in deterministic order
        var allSlots = UnityEngine
            .Object.FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .ToDictionary(s => s, s => s.index);

        var creatures = UnityEngine
            .Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
            .Where(c => c != null && !c.isDying)
            .OrderBy(c => GetSlotIndex(c, allSlots))
            .ToList();

        foreach (var c in creatures)
        {
            var cs = new CreatureState
            {
                cardId = c.data?.cardId ?? "",
                slotIndex = GetSlotIndex(c, allSlots),
                owner = c.owner,
                currentHealth = c.currentHealth,
                maxHealth = c.maxHealth,
                body = c.body,
                speed = c.speed,
                eaten = c.eaten,
                traitNames =
                    c.traits?.Where(t => t != null)
                        .Select(t => t.traitName ?? t.GetType().Name)
                        .ToList() ?? new List<string>(),
                statuses = new Dictionary<StatusTag, int>(),
                traitUsedWhirlwind = c.traitUsedWhirlwind,
                traitUsedBloodthirsty = c.traitUsedBloodthirsty,
                traitUsedUndyingSpirit = c.traitUsedUndyingSpirit,
                traitGrantRadiantShield = c.traitGrantRadiantShield,
                traitGrantEvasiveStealth = c.traitGrantEvasiveStealth,
                traitGrantBloodRush = c.traitGrantBloodRush,
                traitElementalHpBonus = c.traitElementalHpBonus,
            };

            foreach (var tag in c.GetActiveStatusTags())
            {
                cs.statuses[tag] = c.GetStatus(tag);
            }

            state.creatures.Add(cs);
        }

        // Capture global effects
        if (rm?.activeGlobalEffects != null)
        {
            foreach (var ge in rm.activeGlobalEffects)
            {
                if (ge == null)
                    continue;
                state.globalEffects.Add(
                    new GlobalEffectState
                    {
                        effectTypeName = ge.GetType().Name,
                        remainingRounds = ge.remainingRounds,
                        owner = ge.owner,
                    }
                );
            }
        }

        return state;
    }

    private static int GetSlotIndex(Creature c, Dictionary<BoardSlot, int> slotIndices)
    {
        foreach (var kvp in slotIndices)
        {
            if (kvp.Key.currentCreature == c)
                return kvp.Value;
        }
        return -1;
    }

    /// <summary>
    /// Serializes a full game state to bytes for network transmission.
    /// </summary>
    public static byte[] Serialize(FullGameState state)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(state.round);
        bw.Write((int)state.era);
        bw.Write((int)state.phase);
        bw.Write(state.p1Score);
        bw.Write(state.p2Score);
        bw.Write(state.p1Momentum);
        bw.Write(state.p2Momentum);
        bw.Write(state.foodPileCount);
        bw.Write((int)state.currentWeather);
        bw.Write(state.lastWeather.HasValue);
        if (state.lastWeather.HasValue)
            bw.Write((int)state.lastWeather.Value);
        bw.Write(state.rngSeed);
        bw.Write(state.rngCallCount);

        // Creatures
        bw.Write(state.creatures?.Count ?? 0);
        if (state.creatures != null)
        {
            foreach (var c in state.creatures)
            {
                bw.Write(c.cardId ?? "");
                bw.Write(c.slotIndex);
                bw.Write((int)c.owner);
                bw.Write(c.currentHealth);
                bw.Write(c.maxHealth);
                bw.Write(c.body);
                bw.Write(c.speed);
                bw.Write(c.eaten);

                // Traits (just names for now)
                bw.Write(c.traitNames?.Count ?? 0);
                if (c.traitNames != null)
                {
                    foreach (var t in c.traitNames)
                        bw.Write(t ?? "");
                }

                // Statuses
                bw.Write(c.statuses?.Count ?? 0);
                if (c.statuses != null)
                {
                    foreach (var kvp in c.statuses.OrderBy(k => (int)k.Key))
                    {
                        bw.Write((int)kvp.Key);
                        bw.Write(kvp.Value);
                    }
                }

                // Trait flags
                bw.Write(c.traitUsedWhirlwind);
                bw.Write(c.traitUsedBloodthirsty);
                bw.Write(c.traitUsedUndyingSpirit);
                bw.Write(c.traitGrantRadiantShield);
                bw.Write(c.traitGrantEvasiveStealth);
                bw.Write(c.traitGrantBloodRush);
                bw.Write(c.traitElementalHpBonus);
            }
        }

        // Global effects
        bw.Write(state.globalEffects?.Count ?? 0);
        if (state.globalEffects != null)
        {
            foreach (var ge in state.globalEffects)
            {
                bw.Write(ge.effectTypeName ?? "");
                bw.Write(ge.remainingRounds);
                bw.Write((int)ge.owner);
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes a full game state from bytes.
    /// </summary>
    public static FullGameState Deserialize(byte[] data)
    {
        var state = new FullGameState
        {
            creatures = new List<CreatureState>(),
            globalEffects = new List<GlobalEffectState>(),
        };

        if (data == null || data.Length == 0)
            return state;

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        state.round = br.ReadInt32();
        state.era = (Era)br.ReadInt32();
        state.phase = (GamePhase)br.ReadInt32();
        state.p1Score = br.ReadInt32();
        state.p2Score = br.ReadInt32();
        state.p1Momentum = br.ReadInt32();
        state.p2Momentum = br.ReadInt32();
        state.foodPileCount = br.ReadInt32();
        state.currentWeather = (WeatherType)br.ReadInt32();
        bool hasLastWeather = br.ReadBoolean();
        if (hasLastWeather)
            state.lastWeather = (WeatherType)br.ReadInt32();
        state.rngSeed = br.ReadInt32();
        state.rngCallCount = br.ReadInt32();

        // Creatures
        int creatureCount = br.ReadInt32();
        for (int i = 0; i < creatureCount; i++)
        {
            var c = new CreatureState
            {
                cardId = br.ReadString(),
                slotIndex = br.ReadInt32(),
                owner = (SlotOwner)br.ReadInt32(),
                currentHealth = br.ReadInt32(),
                maxHealth = br.ReadInt32(),
                body = br.ReadInt32(),
                speed = br.ReadInt32(),
                eaten = br.ReadInt32(),
                traitNames = new List<string>(),
                statuses = new Dictionary<StatusTag, int>(),
            };

            int traitCount = br.ReadInt32();
            for (int t = 0; t < traitCount; t++)
                c.traitNames.Add(br.ReadString());

            int statusCount = br.ReadInt32();
            for (int s = 0; s < statusCount; s++)
            {
                var tag = (StatusTag)br.ReadInt32();
                var stacks = br.ReadInt32();
                c.statuses[tag] = stacks;
            }

            c.traitUsedWhirlwind = br.ReadBoolean();
            c.traitUsedBloodthirsty = br.ReadBoolean();
            c.traitUsedUndyingSpirit = br.ReadBoolean();
            c.traitGrantRadiantShield = br.ReadBoolean();
            c.traitGrantEvasiveStealth = br.ReadBoolean();
            c.traitGrantBloodRush = br.ReadBoolean();
            c.traitElementalHpBonus = br.ReadInt32();

            state.creatures.Add(c);
        }

        // Global effects
        int geCount = br.ReadInt32();
        for (int i = 0; i < geCount; i++)
        {
            state.globalEffects.Add(
                new GlobalEffectState
                {
                    effectTypeName = br.ReadString(),
                    remainingRounds = br.ReadInt32(),
                    owner = (SlotOwner)br.ReadInt32(),
                }
            );
        }

        return state;
    }
}
