using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flock-level controller for boid agents.
/// - Holds tunable steering parameters.
/// - Optionally spawns agents at runtime.
/// - Provides shared neighbor queries and target anchors.
/// </summary>
[AddComponentMenu("AI/Boids/Boid Flock")]
public class BoidFlock : MonoBehaviour
{
    [Header("Spawn (Optional)")]
    public BoidAgent agentPrefab;
    [Range(0, 1024)] public int spawnCount = 0;
    public Vector3 spawnExtents = new Vector3(3, 1, 3);

    [Header("Context")]
    [Tooltip("Where the swarm tends to stay around (e.g., Beehive). If null, uses this transform.")]
    public Transform anchor;
    public float anchorRadius = 5f;

    [Header("Neighbourhood")]
    public float neighborRadius = 2.5f;
    public float separationRadius = 1.0f;

    [Header("Weights")]
    public float separationWeight = 1.2f;
    public float alignmentWeight  = 1.0f;
    public float cohesionWeight   = 0.8f;
    public float anchorWeight     = 0.6f;
    public float wanderWeight     = 0.4f;
    public float chaseWeight      = 1.5f;

    [Header("Motion")]
    public float maxSpeed = 5f;
    public float maxSteer = 10f;
    public float wanderJitter = 0.5f;

    [Header("Bounds (Optional)")]
    public bool confineToSphere = true;
    public float confineRadius = 12f;

    private readonly List<BoidAgent> _agents = new();

    public IReadOnlyList<BoidAgent> Agents => _agents;

    void Awake()
    {
        if (anchor == null)
            anchor = transform;
    }

    void Start()
    {
        if (agentPrefab != null && spawnCount > 0)
        {
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 local = new Vector3(
                    Random.Range(-spawnExtents.x, spawnExtents.x),
                    Random.Range(-spawnExtents.y, spawnExtents.y),
                    Random.Range(-spawnExtents.z, spawnExtents.z)
                );
                var pos = transform.TransformPoint(local);
                var agent = Instantiate(agentPrefab, pos, Random.rotation, transform);
                Register(agent);
            }
        }
        else
        {
            // Auto-register any children
            foreach (var agent in GetComponentsInChildren<BoidAgent>())
            {
                Register(agent);
            }
        }
    }

    public void Register(BoidAgent agent)
    {
        if (agent == null) return;
        if (_agents.Contains(agent)) return;
        _agents.Add(agent);
        agent.Initialize(this);
    }

    public void Unregister(BoidAgent agent)
    {
        if (agent == null) return;
        _agents.Remove(agent);
    }

    public IEnumerable<BoidAgent> GetNeighbors(BoidAgent self)
    {
        float r2 = neighborRadius * neighborRadius;
        for (int i = 0; i < _agents.Count; i++)
        {
            var other = _agents[i];
            if (other == null || other == self) continue;
            var d2 = (other.Position - self.Position).sqrMagnitude;
            if (d2 <= r2)
                yield return other;
        }
    }

    public Vector3 AnchorPosition => anchor != null ? anchor.position : transform.position;

    public Vector3 Confine(Vector3 position)
    {
        if (!confineToSphere) return position;
        var center = AnchorPosition;
        var offset = position - center;
        if (offset.sqrMagnitude > confineRadius * confineRadius)
        {
            offset = offset.normalized * confineRadius;
            return center + offset;
        }
        return position;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(anchor ? anchor.position : transform.position, anchorRadius);
        if (confineToSphere)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            Gizmos.DrawWireSphere(anchor ? anchor.position : transform.position, confineRadius);
        }
    }
#endif
}
