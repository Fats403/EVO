using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Restores game state from a serialized snapshot.
/// Used for reconnection recovery and resync after desync.
/// </summary>
public class GameStateRestorer : MonoBehaviour
{
    public static GameStateRestorer Instance { get; private set; }

    [Header("References")]
    [Tooltip("Reference to the CardDatabase asset for looking up cards")]
    [SerializeField]
    private CardDatabase cardDatabase;

    [Tooltip("Creature prefab for spawning restored creatures")]
    [SerializeField]
    private GameObject creaturePrefab;

    /// <summary>
    /// Raised before state restoration begins.
    /// </summary>
    public event Action OnRestoreStarted;

    /// <summary>
    /// Raised after state restoration completes.
    /// </summary>
    public event Action<int> OnRestoreCompleted;

    /// <summary>
    /// Raised if restoration fails.
    /// </summary>
    public event Action<string> OnRestoreFailed;

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
    /// Restores the game to the given state snapshot.
    /// This is the main entry point for reconnection recovery.
    /// </summary>
    public void RestoreState(GameStateSerialization.FullGameState state)
    {
        try
        {
            Debug.Log($"[GameStateRestorer] Beginning state restoration to round {state.round}...");
            OnRestoreStarted?.Invoke();

            // 1. Restore core game state
            RestoreGameManagerState(state);

            // 2. Restore RNG state
            RestoreRngState(state);

            // 3. Restore weather
            RestoreWeatherState(state);

            // 4. Restore scores
            RestoreScores(state);

            // 5. Clear existing creatures and restore from snapshot
            RestoreCreatures(state);

            // 6. Restore global effects
            RestoreGlobalEffects(state);

            Debug.Log($"[GameStateRestorer] State restoration complete (round {state.round}).");
            OnRestoreCompleted?.Invoke(state.round);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[GameStateRestorer] State restoration failed: {e.Message}\n{e.StackTrace}"
            );
            OnRestoreFailed?.Invoke(e.Message);
        }
    }

    private void RestoreGameManagerState(GameStateSerialization.FullGameState state)
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[GameStateRestorer] GameManager not found.");
            return;
        }

        gm.currentRound = state.round;
        gm.currentEra = state.era;
        gm.currentPhase = state.phase;
        gm.p1Momentum = state.p1Momentum;
        gm.p2Momentum = state.p2Momentum;

        // Restore food pile count directly
        if (gm.foodPile != null)
        {
            gm.foodPile.count = state.foodPileCount;
        }

        Debug.Log(
            $"[GameStateRestorer] Restored GameManager: round={state.round}, era={state.era}, phase={state.phase}"
        );
    }

    private void RestoreRngState(GameStateSerialization.FullGameState state)
    {
        // Re-initialize RNG with the same seed and fast-forward to the correct call count
        DeterministicRng.Initialize(state.rngSeed);
        GameStateChecksum.ResetRngCallCount();

        // Fast-forward the RNG to match the call count by making dummy calls
        for (int i = 0; i < state.rngCallCount; i++)
        {
            // Use NextInt to advance the RNG (the result is discarded)
            DeterministicRng.NextInt(0, 100);
        }

        Debug.Log(
            $"[GameStateRestorer] Restored RNG: seed={state.rngSeed}, calls={state.rngCallCount}"
        );
    }

    private void RestoreWeatherState(GameStateSerialization.FullGameState state)
    {
        var wm = WeatherManager.Instance;
        if (wm == null)
        {
            Debug.LogWarning("[GameStateRestorer] WeatherManager not found.");
            return;
        }

        // WeatherManager uses SetWeatherForRestore if available, otherwise skip
        // We'll add this method to WeatherManager
        wm.SetWeatherForRestore(state.currentWeather, state.lastWeather);

        Debug.Log($"[GameStateRestorer] Restored weather: {state.currentWeather}");
    }

    private void RestoreScores(GameStateSerialization.FullGameState state)
    {
        ScoreManager.player1 = state.p1Score;
        ScoreManager.player2 = state.p2Score;

        // Update UI via the instance's UpdateUI method
        ScoreManager.Instance?.UpdateUI();

        Debug.Log($"[GameStateRestorer] Restored scores: P1={state.p1Score}, P2={state.p2Score}");
    }

    private void RestoreCreatures(GameStateSerialization.FullGameState state)
    {
        // Get all board slots
        var allSlots = UnityEngine
            .Object.FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
            .ToDictionary(s => s.index, s => s);

        // Clear all existing creatures
        var existingCreatures = UnityEngine.Object.FindObjectsByType<Creature>(
            FindObjectsSortMode.None
        );
        foreach (var creature in existingCreatures)
        {
            if (creature != null)
            {
                var slot = allSlots.Values.FirstOrDefault(s => s.currentCreature == creature);
                if (slot != null)
                {
                    slot.Vacate();
                }
                Destroy(creature.gameObject);
            }
        }

        Debug.Log($"[GameStateRestorer] Cleared {existingCreatures.Length} existing creatures.");

        // Ensure we have a card database
        if (cardDatabase == null)
        {
            Debug.LogError("[GameStateRestorer] CardDatabase not assigned.");
            return;
        }

        foreach (var creatureState in state.creatures)
        {
            if (!allSlots.TryGetValue(creatureState.slotIndex, out var slot))
            {
                Debug.LogWarning(
                    $"[GameStateRestorer] Slot {creatureState.slotIndex} not found, skipping creature."
                );
                continue;
            }

            // Find the card data
            var cardDef = cardDatabase.GetById(creatureState.cardId);
            if (cardDef == null || !(cardDef is CreatureCard creatureCard))
            {
                Debug.LogWarning(
                    $"[GameStateRestorer] Card {creatureState.cardId} not found or not a creature, skipping."
                );
                continue;
            }

            // Spawn creature at slot
            var creature = SpawnCreatureAtSlot(creatureCard, slot, creatureState);
            if (creature != null)
            {
                ApplyCreatureState(creature, creatureState);
            }
        }

        Debug.Log($"[GameStateRestorer] Restored {state.creatures.Count} creatures.");
    }

    private Creature SpawnCreatureAtSlot(
        CreatureCard cardData,
        BoardSlot slot,
        GameStateSerialization.CreatureState creatureState
    )
    {
        // Use DeckManager if available
        var dm = DeckManager.Instance;
        if (dm != null)
        {
            // Clear the slot first to ensure SpawnCreature doesn't reject due to "occupied"
            slot.Vacate();
            var spawned = dm.SpawnCreature(cardData, slot, creatureState.owner);
            return spawned;
        }

        // Fallback: Manual spawn using prefab
        if (creaturePrefab == null)
        {
            Debug.LogError("[GameStateRestorer] Creature prefab not assigned.");
            return null;
        }

        var go = Instantiate(creaturePrefab, slot.transform.position, Quaternion.identity);
        var creatureComp = go.GetComponent<Creature>();
        if (creatureComp != null)
        {
            creatureComp.Initialize(cardData);
            creatureComp.owner = creatureState.owner;
            slot.Occupy(creatureComp);
        }

        return creatureComp;
    }

    private void ApplyCreatureState(Creature creature, GameStateSerialization.CreatureState state)
    {
        creature.currentHealth = state.currentHealth;
        creature.maxHealth = state.maxHealth;
        creature.body = state.body;
        creature.speed = state.speed;
        creature.eaten = state.eaten;

        // Restore trait flags
        creature.traitUsedWhirlwind = state.traitUsedWhirlwind;
        creature.traitUsedBloodthirsty = state.traitUsedBloodthirsty;
        creature.traitUsedUndyingSpirit = state.traitUsedUndyingSpirit;
        creature.traitGrantRadiantShield = state.traitGrantRadiantShield;
        creature.traitGrantEvasiveStealth = state.traitGrantEvasiveStealth;
        creature.traitGrantBloodRush = state.traitGrantBloodRush;
        creature.traitElementalHpBonus = state.traitElementalHpBonus;

        // Restore status effects by adding each status
        // First clear by iterating current statuses
        var currentTags = creature.GetActiveStatusTags().ToList();
        foreach (var tag in currentTags)
        {
            creature.ClearStatus(tag);
        }

        // Then add the saved statuses
        if (state.statuses != null)
        {
            foreach (var kvp in state.statuses)
            {
                creature.AddStatus(kvp.Key, kvp.Value);
            }
        }

        // Refresh the creature's stats UI
        creature.RefreshStatsUI();
    }

    private void RestoreGlobalEffects(GameStateSerialization.FullGameState state)
    {
        var rm = ResolutionManager.Instance;
        if (rm == null)
        {
            Debug.LogWarning("[GameStateRestorer] ResolutionManager not found.");
            return;
        }

        // Clear existing global effects
        rm.activeGlobalEffects?.Clear();

        // Restore global effects
        if (state.globalEffects == null || state.globalEffects.Count == 0)
        {
            Debug.Log("[GameStateRestorer] No global effects to restore.");
            return;
        }

        foreach (var geState in state.globalEffects)
        {
            // Find the effect type by name and instantiate
            var effectType = FindGlobalEffectType(geState.effectTypeName);
            if (effectType == null)
            {
                Debug.LogWarning(
                    $"[GameStateRestorer] Global effect type {geState.effectTypeName} not found."
                );
                continue;
            }

            // Create instance and restore state
            var effect = ScriptableObject.CreateInstance(effectType) as GlobalEffectBase;
            if (effect != null)
            {
                effect.remainingRounds = geState.remainingRounds;
                effect.owner = geState.owner;
                rm.activeGlobalEffects?.Add(effect);
            }
        }

        Debug.Log($"[GameStateRestorer] Restored {state.globalEffects.Count} global effects.");
    }

    private Type FindGlobalEffectType(string typeName)
    {
        // Search for the type in loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null && typeof(GlobalEffectBase).IsAssignableFrom(type))
            {
                return type;
            }
        }
        return null;
    }
}
