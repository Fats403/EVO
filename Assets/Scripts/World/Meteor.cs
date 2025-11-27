using System;
using UnityEngine;

/// <summary>
/// Simple controller for a falling meteor that flies toward a Creature,
/// triggers an impact callback, plays an explosion animation, then destroys itself.
/// </summary>
public class Meteor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float speed = 10f;

    [SerializeField]
    private float hitRadius = 0.1f;

    private Creature target;
    private Action<Creature> onImpact;
    private bool hasHit;
    private Animator animator;

    // Per-instance runtime tuning set by the spawner
    private float speedMultiplier = 1f;
    private float startDelay = 0f;

    private static readonly int ExplodeHash = Animator.StringToHash("Explode");

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Initialize this meteor to fall toward a specific target and invoke a callback on impact.
    /// </summary>
    public void Initialize(Creature target, Action<Creature> onImpact)
    {
        Initialize(target, onImpact, 1f, 0f, 1f);
    }

    /// <summary>
    /// Initialize with optional per-instance variance controls.
    /// </summary>
    public void Initialize(
        Creature target,
        Action<Creature> onImpact,
        float speedMultiplier,
        float startDelay,
        float sizeMultiplier
    )
    {
        this.target = target;
        this.onImpact = onImpact;
        this.speedMultiplier = Mathf.Max(0.05f, speedMultiplier);
        this.startDelay = Mathf.Max(0f, startDelay);

        if (Mathf.Abs(sizeMultiplier - 1f) > 0.001f)
        {
            transform.localScale *= sizeMultiplier;
        }
    }

    private void Update()
    {
        if (hasHit)
            return;

        // Optional delay before this meteor starts moving
        if (startDelay > 0f)
        {
            startDelay -= Time.deltaTime;
            return;
        }

        // If the target is already gone, just explode where we are.
        if (target == null || target.isDying || target.currentHealth <= 0)
        {
            TriggerExplosion();
            return;
        }

        Vector3 targetPos = target.transform.position;
        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;
        float step = speed * speedMultiplier * Time.deltaTime;

        if (distance <= hitRadius || step >= distance)
        {
            // Snap to target and register hit
            transform.position = targetPos;
            HandleHit();
        }
        else
        {
            transform.position += toTarget.normalized * step;
        }
    }

    private void HandleHit()
    {
        if (hasHit)
            return;

        hasHit = true;

        // Perform gameplay callback if target is still valid
        if (onImpact != null && target != null && !target.isDying && target.currentHealth > 0)
        {
            try
            {
                onImpact(target);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Meteor impact callback threw: {ex}");
            }
        }

        TriggerExplosion();
    }

    private void TriggerExplosion()
    {
        if (animator != null)
        {
            animator.SetTrigger(ExplodeHash);
        }
        else
        {
            // No animator/animation – just destroy immediately
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Hook this to an Animation Event at the end of the explosion clip.
    /// </summary>
    public void OnExplosionFinished()
    {
        Destroy(gameObject);
    }
}
