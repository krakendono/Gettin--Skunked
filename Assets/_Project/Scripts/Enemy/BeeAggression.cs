using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editable aggression controller for a group of boid agents (bees).
/// - Set aggression with a slider (0=Nice, 1=Killer) or customize per-parameter.
/// - Finds players in range and sets boids to chase when aggro > 0.
/// - Applies sting damage via IDamageable when targets enter sting radius (local/offline).
///
/// Note: For networked validation, wire this to your server-authoritative damage flow
/// (e.g., send an RPC to a server-side system that validates range and applies damage).
/// </summary>
[AddComponentMenu("AI/Boids/Bee Aggression")]
public class BeeAggression : MonoBehaviour
{
    [Header("References")]
    public BoidFlock flock;
    [Tooltip("Optional: Explicit targets to consider. If empty, will try Player tag.")]
    public List<Transform> candidateTargets = new();

    [Header("Aggression")]
    [Range(0f, 1f)] public float aggressionLevel = 0f; // 0=Nice, 1=Killer
    public bool reactiveToHoneyTheft = true;            // Hook you can call from CollectHoney

    [Header("Ranges")]
    public float detectionRadius = 8f; // begin considering targets
    public float chaseRadius     = 6f; // pass to agents for chase
    public float stingRadius     = 1.0f; // damage when within this distance

    [Header("Damage")]
    public float damagePerSting = 5f;
    public float stingCooldown  = 1.5f;

    [Header("Behavior Scaling (Aggression -> Param)")]
    [Tooltip("How much the detection radius increases as aggression goes to 1.")]
    public float detectionRadiusBonus = 6f;
    [Tooltip("How much the sting damage increases as aggression goes to 1.")]
    public float damageBonus = 15f;

    [Header("Debug/UX")]
    public bool drawGizmos = true;

    private Transform _currentTarget;
    private float _nextStingTime = 0f;

    void Reset()
    {
        flock = GetComponent<BoidFlock>();
    }

    void Awake()
    {
        if (flock == null)
            flock = GetComponent<BoidFlock>();
    }

    void Update()
    {
        if (flock == null) return;

        // Update aggression-driven parameters
        float agg = Mathf.Clamp01(aggressionLevel);
        float dynDetect = detectionRadius + detectionRadiusBonus * agg;
        float dynDamage = damagePerSting + damageBonus * agg;

        // Acquire or validate target
        _currentTarget = SelectTarget(dynDetect);

        // Push settings to agents
        foreach (var agent in flock.Agents)
        {
            if (agent == null) continue;
            agent.aggressionLevel = agg;
            agent.chaseTarget = _currentTarget;
            agent.chaseRadius = chaseRadius;
        }

        // Try stinging if sufficiently aggressive and near
        if (_currentTarget != null && agg > 0.05f)
        {
            TrySting(_currentTarget, dynDamage);
        }
    }

    private Transform SelectTarget(float detectRadius)
    {
        // 1) Use provided candidates
        if (candidateTargets != null && candidateTargets.Count > 0)
        {
            Transform best = null;
            float bestD2 = float.MaxValue;
            foreach (var t in candidateTargets)
            {
                if (t == null) continue;
                float d2 = (t.position - transform.position).sqrMagnitude;
                if (d2 < bestD2 && d2 <= detectRadius * detectRadius)
                {
                    bestD2 = d2;
                    best = t;
                }
            }
            if (best != null) return best;
        }

        // 2) Fall back to Player tag
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float d2 = (player.transform.position - transform.position).sqrMagnitude;
            if (d2 <= detectRadius * detectRadius)
                return player.transform;
        }

        return null;
    }

    private void TrySting(Transform target, float dynDamage)
    {
        if (Time.time < _nextStingTime) return;

        // If any agent is very close, sting
        float rad2 = stingRadius * stingRadius;
        foreach (var agent in flock.Agents)
        {
            if (agent == null) continue;
            float d2 = (agent.Position - target.position).sqrMagnitude;
            if (d2 <= rad2)
            {
                ApplyDamage(target.gameObject, dynDamage);
                _nextStingTime = Time.time + stingCooldown;
                break;
            }
        }
    }

    private void ApplyDamage(GameObject go, float dmg)
    {
        // Local/offline damage via IDamageable
        var damageable = go.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg);
        }
        // Else: extend here to call into your networked damage path
    }

    /// <summary>
    /// Optional hook: call this from your honey collection script when a player steals honey.
    /// </summary>
    public void NotifyHoneyStolen(Transform thief, int amount)
    {
        if (!reactiveToHoneyTheft) return;
        // Raise aggression based on amount and set target to thief for a short time
        aggressionLevel = Mathf.Clamp01(aggressionLevel + Mathf.Clamp01(amount / 10f));
        if (thief != null)
        {
            if (!candidateTargets.Contains(thief))
                candidateTargets.Add(thief);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius + detectionRadiusBonus * Mathf.Clamp01(aggressionLevel));
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, stingRadius);
    }
#endif
}
