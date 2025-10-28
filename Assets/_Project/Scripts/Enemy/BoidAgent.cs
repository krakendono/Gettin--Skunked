using UnityEngine;

/// <summary>
/// Boid agent with standard Reynolds steering + optional chase target.
/// Designed to be lightweight and highly tunable from the inspector.
/// </summary>
[AddComponentMenu("AI/Boids/Boid Agent")]
public class BoidAgent : MonoBehaviour
{
    [Header("Runtime (ReadOnly)")]
    [SerializeField] private Vector3 velocity;

    [Header("Aggression Influence")]
    [Range(0f, 1f)] public float aggressionLevel = 0f; // 0 = nice; 1 = killer
    public Transform chaseTarget;                       // Optional target to chase when aggressive
    public float chaseRadius = 6f;                      // Start steering toward target within this radius

    private BoidFlock _flock;
    private Vector3 _wander;

    public Vector3 Position => transform.position;

    public void Initialize(BoidFlock flock)
    {
        _flock = flock;
        velocity = transform.forward * (_flock != null ? _flock.maxSpeed * 0.25f : 1f);
        _wander = Random.insideUnitSphere;
    }

    void OnEnable()
    {
        if (_flock == null)
        {
            _flock = GetComponentInParent<BoidFlock>();
            if (_flock != null) _flock.Register(this);
        }
        if (_wander == Vector3.zero)
            _wander = Random.insideUnitSphere;
    }

    void OnDisable()
    {
        _flock?.Unregister(this);
    }

    void Update()
    {
        if (_flock == null)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 steer = Vector3.zero;

        // Neighbourhood-based steering
        Vector3 sep = Vector3.zero, ali = Vector3.zero, coh = Vector3.zero;
        int count = 0;
        foreach (var other in _flock.GetNeighbors(this))
        {
            count++;
            var toOther = other.Position - Position;
            // Separation
            float dist = toOther.magnitude;
            if (dist > 0.0001f && dist < _flock.separationRadius)
            {
                sep -= toOther / Mathf.Max(dist, 0.01f);
            }
            // Alignment
            ali += other.velocity;
            // Cohesion
            coh += other.Position;
        }
        if (count > 0)
        {
            ali /= count;
            coh = (coh / count - Position);
        }

        // Anchor attraction
        Vector3 toAnchor = _flock.AnchorPosition - Position;
        Vector3 anchor = Vector3.ClampMagnitude(toAnchor, _flock.maxSteer);

        // Wander noise
        _wander += Random.insideUnitSphere * _flock.wanderJitter * dt;
        _wander = Vector3.ClampMagnitude(_wander, 1f);

        // Chase when aggressive and target is near
        Vector3 chase = Vector3.zero;
        float agg = Mathf.Clamp01(aggressionLevel);
        if (chaseTarget != null)
        {
            Vector3 toT = (chaseTarget.position - Position);
            float r = Mathf.Lerp(chaseRadius * 0.5f, chaseRadius * 2f, agg);
            if (toT.sqrMagnitude <= r * r)
            {
                chase = toT.normalized * (_flock.maxSteer * Mathf.Lerp(0.6f, 1.4f, agg));
            }
        }

        // Weighting
        steer += sep.normalized * _flock.separationWeight;
        steer += ali.normalized * _flock.alignmentWeight;
        steer += coh.normalized * _flock.cohesionWeight;
        steer += anchor.normalized * _flock.anchorWeight;
        steer += _wander * _flock.wanderWeight;
        steer += chase * _flock.chaseWeight * agg; // chase scales with aggression

        steer = Vector3.ClampMagnitude(steer, _flock.maxSteer);

        // Update velocity with steering
        velocity += steer * dt;
        // Speed scales slightly with aggression
        float maxSpeed = _flock.maxSpeed * Mathf.Lerp(0.7f, 1.5f, agg);
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        // Move
        Vector3 newPos = Position + velocity * dt;
        newPos = _flock.Confine(newPos);
        transform.position = newPos;

        // Face movement direction
        if (velocity.sqrMagnitude > 0.0001f)
        {
            var rot = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, dt * 10f);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _flock ? _flock.neighborRadius : 2f);
        if (chaseTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, chaseTarget.position);
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, chaseRadius);
        }
    }
#endif
}
