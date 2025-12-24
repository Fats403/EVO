using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central manager for board-wide or targeted visual effects (meteors, lightning, etc.).
/// For now, it handles spawning meteors with per-target impact callbacks and
/// a special game-ending extinction event. Other VFX (like lightning strikes) can be added here.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Meteor Settings")]
    [SerializeField]
    private Meteor meteorPrefab;

    [Tooltip("How far from the camera center to spawn on X (0-1, as fraction of screen width).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float horizontalOffsetFactor = 0.7f;

    [Tooltip("Extra world units above the top of the camera to spawn meteors.")]
    [SerializeField]
    private float spawnAboveScreen = 1f;

    [Header("Meteor Variance")]
    [Tooltip("Random +/- factor applied to meteor speed (e.g., 0.15 = ±15%).")]
    [Range(0f, 0.9f)]
    [SerializeField]
    private float meteorSpeedVariance = 0.15f;

    [Tooltip("Random +/- factor applied to meteor size (e.g., 0.1 = ±10%).")]
    [Range(0f, 0.9f)]
    [SerializeField]
    private float meteorSizeVariance = 0.1f;

    [Tooltip("Maximum random delay (seconds) before a spawned meteor starts moving.")]
    [SerializeField]
    private float meteorMaxStartDelay = 0.25f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool HasMeteorPrefab => meteorPrefab != null;

    /// <summary>
    /// Helper to get a deterministic random value if GameManager is available,
    /// otherwise fall back to UnityEngine.Random (warning logged).
    /// </summary>
    private float NextFloat(float minInclusive, float maxExclusive)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning(
                "VFXManager: GameManager.Instance is null during NextFloat. Determinism may be compromised."
            );
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
        // System.Random.NextDouble is [0, 1)
        double val = (double)GameManager.Instance.NextRandomInt(0, 1000000) / 1000000.0;
        return (float)(minInclusive + (val * (maxExclusive - minInclusive)));
    }

    /// <summary>
    /// Spawn a single meteor above the camera, aimed at the given creature.
    /// The provided callback is invoked when the meteor connects.
    /// </summary>
    public void SpawnMeteor(Creature target, Action<Creature> onImpact)
    {
        if (meteorPrefab == null || target == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("VFXManager: No main camera found for meteor spawn.");
            return;
        }

        float camHeight = 2f * cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        bool spawnRight = NextFloat(0f, 1f) > 0.5f;
        float clampedFactor = Mathf.Clamp(horizontalOffsetFactor, 0f, 0.99f);
        float xOffset = camWidth * clampedFactor * 0.5f;
        float spawnX = cam.transform.position.x + (spawnRight ? xOffset : -xOffset);
        float spawnY = cam.transform.position.y + camHeight * 0.5f + spawnAboveScreen;

        Vector3 spawnPos = new Vector3(spawnX, spawnY, target.transform.position.z);

        Meteor meteor = Instantiate(meteorPrefab, spawnPos, Quaternion.identity);

        // Mirror on X if spawning on the right side for a more natural look
        if (spawnRight)
        {
            Vector3 scale = meteor.transform.localScale;
            scale.x *= -1f;
            meteor.transform.localScale = scale;
        }

        // Per-instance variance
        float speedMult = 1f;
        if (meteorSpeedVariance > 0f)
        {
            float delta = NextFloat(-meteorSpeedVariance, meteorSpeedVariance);
            speedMult = Mathf.Max(0.1f, 1f + delta);
        }

        float sizeMult = 1f;
        if (meteorSizeVariance > 0f)
        {
            float delta = NextFloat(-meteorSizeVariance, meteorSizeVariance);
            sizeMult = Mathf.Max(0.3f, 1f + delta);
        }

        float startDelay = meteorMaxStartDelay > 0f ? NextFloat(0f, meteorMaxStartDelay) : 0f;

        meteor.Initialize(target, onImpact, speedMult, startDelay, sizeMult);
    }

    /// <summary>
    /// Spawn a meteor for each target creature, using the same impact callback.
    /// </summary>
    public void SpawnMeteorRain(IEnumerable<Creature> targets, Action<Creature> onImpact)
    {
        if (targets == null)
            return;

        foreach (var c in targets)
        {
            if (c == null)
                continue;
            SpawnMeteor(c, onImpact);
        }
    }

    /// <summary>
    /// Game-ending extinction: spawn meteors on all living creatures and kill them on impact.
    /// Falls back to immediate Kill() if no meteorPrefab is configured.
    /// </summary>
    public void TriggerGameEndingExtinction()
    {
        List<Creature> creatures;

        if (ResolutionManager.Instance != null)
        {
            creatures = ResolutionManager
                .Instance.AllCreatures()
                .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
                .ToList();
        }
        else
        {
            creatures = UnityEngine
                .Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
                .Where(c => c != null && c.currentHealth > 0 && !c.isDying)
                .ToList();
        }

        if (creatures == null || creatures.Count == 0)
            return;

        if (!HasMeteorPrefab)
        {
            // No VFX configured – just kill everything immediately.
            foreach (var c in creatures)
            {
                if (c != null && !c.isDying && c.currentHealth > 0)
                {
                    c.Kill("Final Extinction");
                }
            }

            return;
        }

        foreach (var c in creatures)
        {
            var captured = c;
            SpawnMeteor(
                captured,
                target =>
                {
                    if (target == null || target.isDying || target.currentHealth <= 0)
                        return;
                    target.Kill("Final Extinction");
                }
            );
        }
    }
}
