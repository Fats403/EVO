using UnityEngine;

/// <summary>
/// Centralised deterministic RNG used across the project (draft, AI, weather,
/// resolution, etc.). This is the single place that owns the random seed
/// for a given run, so that we can later drive it from a network-agreed
/// seed for multiplayer.
/// </summary>
public static class DeterministicRng
{
    private static System.Random _rng;
    private static bool _initialized;
    private static int _seed;

    /// <summary>True if the RNG has been explicitly initialised for this session.</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>The seed currently in use for this session.</summary>
    public static int Seed => _seed;

    /// <summary>
    /// Initialise the RNG with a specific seed. This should be called once at
    /// the start of a run (e.g., in DeckHub when the player starts a game, or
    /// by a network handshake in multiplayer).
    /// </summary>
    public static void Initialize(int seed)
    {
        _seed = seed;
        _rng = new System.Random(seed);
        _initialized = true;

        // Keep Unity's RNG in sync in case any legacy code still uses it.
        UnityEngine.Random.InitState(seed);
    }

    /// <summary>
    /// Returns a deterministic integer in [minInclusive, maxExclusive). If the
    /// RNG has not been explicitly initialised, a seed will be chosen from
    /// UnityEngine.Random the first time this is called.
    /// </summary>
    public static int NextInt(int minInclusive, int maxExclusive)
    {
        if (!_initialized)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Initialize(seed);
        }

        return _rng.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Derives a stable sub-seed from the current session seed and a salt.
    /// This is useful for creating independent deterministic streams (e.g.,
    /// per-player deck shuffles) that are still fully determined by the
    /// shared match seed.
    /// </summary>
    public static int DeriveSubSeed(int salt)
    {
        if (!_initialized)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Initialize(seed);
        }
        unchecked
        {
            int h = _seed;
            // FNV-style mixing with a small prime within 32-bit int range.
            const int prime = 16777619;
            h ^= salt * prime;
            h ^= (h << 6) + (h >> 2);
            return h;
        }
    }

    /// <summary>
    /// Creates a new System.Random instance using a sub-seed derived from the
    /// current session seed and the given salt.
    /// </summary>
    public static System.Random CreateSubRandom(int salt)
    {
        return new System.Random(DeriveSubSeed(salt));
    }
}
