using UnityEngine;
using System.Collections;

[System.Serializable]
public enum AxeState
{
    Idle,
    Attacking,
    Blocking,
    Cooldown
}

[System.Serializable]
public enum AttackType
{
    LeftClick,    // Primary attack - swing
    RightClick    // Secondary attack - heavy attack
}

public class Axe : MonoBehaviour
{
    [Header("Axe Settings")]
    public string axeName = "Battle Axe";
    public float primaryDamage = 50f;
    public float secondaryDamage = 75f;
    
    [Header("Attack Timing")]
    public float primaryAttackDuration = 0.6f;
    public float secondaryAttackDuration = 1.0f;
    public float attackCooldown = 0.3f;
    public float blockDuration = 2.0f;
    
    [Header("Collision Settings")]
    [Range(0f, 1f)] public float attackStartPercent = 0.2f; // When in animation to start collision
    [Range(0f, 1f)] public float attackEndPercent = 0.8f;   // When in animation to end collision
    
    [Header("Stamina Costs")]
    public float primaryAttackStaminaCost = 15f;
    public float secondaryAttackStaminaCost = 25f;
    
    [Header("Components")]
    public Collider axeCollider; // Non-trigger collider for the axe blade
    public LayerMask enemyLayers = 1; // What layers can be hit
    public Rigidbody axeRigidbody; // Required for collision detection
    
    [Header("Effects")]
    public TrailRenderer swingTrail;
    public ParticleSystem hitEffect;
    public AudioClip primarySwingSound;
    public AudioClip secondarySwingSound;
    public AudioClip hitSound;
    public AudioClip blockSound;
    
    [Header("Trail Settings")]
    public float trailDuration = 0.3f;
    public bool enableTrailOnlyDuringAttack = true;
    
    [Header("Animation")]
    public Animator axeAnimator;
    public string primaryAttackTrigger = "PrimaryAttack";
    public string secondaryAttackTrigger = "SecondaryAttack";
    public string blockTrigger = "Block";
    public string idleTrigger = "Idle";
    
    [Header("Blocking")]
    public float blockDamageReduction = 0.8f;
    public float blockStaminaCost = 10f;
    public float maxStamina = 100f;
    public float staminaRegenRate = 20f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool enableDebugLogs = false;
    public bool showColliderGizmos = true;
    
    // Private variables
    private AxeState currentState = AxeState.Idle;
    private AttackType lastAttackType;
    private float stateTimer = 0f;
    private AudioSource audioSource;
    private float currentStamina;
    private bool isBlocking = false;
    
    // Collision control
    private bool canHitThisAttack = false;
    private bool hasHitThisAttack = false;

    #region Unity Lifecycle
    
    void Start()
    {
        InitializeComponents();
        InitializeSettings();
        SetState(AxeState.Idle);
    }

    void Update()
    {
        HandleInput();
        UpdateState();
        RegenerateStamina();
    }

    #endregion

    #region Initialization

    void InitializeComponents()
    {
        // Get or add AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Setup axe collider
        SetupAxeCollider();
        
        // Setup trail renderer
        SetupTrailRenderer();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[{axeName}] Components initialized");
        }
    }

    void InitializeSettings()
    {
        // Initialize stamina
        currentStamina = maxStamina;
        
        // Validate settings
        if (attackStartPercent >= attackEndPercent)
        {
            Debug.LogWarning($"[{axeName}] Attack start percent should be less than end percent!");
            attackEndPercent = attackStartPercent + 0.1f;
        }
    }

    void SetupAxeCollider()
    {
        if (axeCollider == null)
        {
            // Try to find collider in children
            axeCollider = GetComponentInChildren<Collider>();
            if (axeCollider == null)
            {
                Debug.LogError($"[{axeName}] No axe collider assigned or found! Please assign a non-trigger collider.");
                return;
            }
        }

        // Ensure it's NOT a trigger for collision detection
        axeCollider.isTrigger = false;

        // Check for Rigidbody
        if (axeRigidbody == null)
        {
            axeRigidbody = axeCollider.GetComponent<Rigidbody>();
            if (axeRigidbody == null)
            {
                axeRigidbody = axeCollider.GetComponentInParent<Rigidbody>();
            }
        }

        if (axeRigidbody == null)
        {
            Debug.LogWarning($"[{axeName}] No Rigidbody found! Adding kinematic Rigidbody for collision detection.");
            axeRigidbody = axeCollider.gameObject.AddComponent<Rigidbody>();
            axeRigidbody.isKinematic = true;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[{axeName}] Collider setup complete: {axeCollider.name} (Non-trigger: {!axeCollider.isTrigger})");
        }
    }

    void SetupTrailRenderer()
    {
        if (swingTrail != null)
        {
            swingTrail.time = trailDuration;
            
            if (enableTrailOnlyDuringAttack)
            {
                swingTrail.enabled = false;
            }
        }
    }

    #endregion

    #region Input Handling

    void HandleInput()
    {
        // Check if input should be disabled (UI open or cursor unlocked)
        if (PlayerInputManager.ShouldDisableInput())
            return;
        
        // Only handle input when idle or blocking
        if (currentState != AxeState.Idle && currentState != AxeState.Blocking)
            return;

        // Primary attack (Left Click)
        if (Input.GetMouseButtonDown(0))
        {
            if (currentState == AxeState.Idle && currentStamina >= primaryAttackStaminaCost)
            {
                StartAttack(AttackType.LeftClick);
            }
        }

        // Secondary attack (Right Click)
        if (Input.GetMouseButtonDown(1))
        {
            if (currentState == AxeState.Idle && currentStamina >= secondaryAttackStaminaCost)
            {
                StartAttack(AttackType.RightClick);
            }
        }

        // Block (Middle Mouse Button)
        if (Input.GetMouseButtonDown(2))
        {
            if (currentState == AxeState.Idle && currentStamina > 0)
            {
                StartBlock();
            }
        }

        // Stop blocking
        if (Input.GetMouseButtonUp(2))
        {
            if (currentState == AxeState.Blocking)
            {
                StopBlock();
            }
        }
    }

    #endregion

    #region State Management

    void UpdateState()
    {
        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case AxeState.Attacking:
                UpdateAttacking();
                break;

            case AxeState.Blocking:
                UpdateBlocking();
                break;

            case AxeState.Cooldown:
                if (stateTimer >= attackCooldown)
                {
                    SetState(AxeState.Idle);
                }
                break;
        }
    }

    void SetState(AxeState newState)
    {
        AxeState previousState = currentState;
        currentState = newState;
        stateTimer = 0f;

        // Handle state-specific setup
        switch (newState)
        {
            case AxeState.Idle:
                if (axeAnimator != null && previousState != AxeState.Idle)
                {
                    axeAnimator.SetTrigger(idleTrigger);
                }
                break;

            case AxeState.Attacking:
                // Reset attack flags
                hasHitThisAttack = false;
                break;

            case AxeState.Blocking:
                break;

            case AxeState.Cooldown:
                StopSwingTrail();
                break;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[{axeName}] State: {previousState} → {newState}");
        }
    }

    #endregion

    #region Attack System

    void StartAttack(AttackType attackType)
    {
        lastAttackType = attackType;
        SetState(AxeState.Attacking);

        // Consume stamina
        float staminaCost = attackType == AttackType.LeftClick ? primaryAttackStaminaCost : secondaryAttackStaminaCost;
        currentStamina = Mathf.Max(0f, currentStamina - staminaCost);

        // Play animation
        if (axeAnimator != null)
        {
            string trigger = attackType == AttackType.LeftClick ? primaryAttackTrigger : secondaryAttackTrigger;
            axeAnimator.SetTrigger(trigger);
        }

        // Play sound
        AudioClip soundToPlay = attackType == AttackType.LeftClick ? primarySwingSound : secondarySwingSound;
        PlaySound(soundToPlay);

        // Start swing trail
        StartSwingTrail();

        if (enableDebugLogs)
        {
            Debug.Log($"[{axeName}] Started {attackType} attack - Stamina: {currentStamina:F1}/{maxStamina}");
        }
    }

    void UpdateAttacking()
    {
        float attackDuration = lastAttackType == AttackType.LeftClick ? primaryAttackDuration : secondaryAttackDuration;
        float attackProgress = stateTimer / attackDuration;

        // End attack when duration is complete
        if (stateTimer >= attackDuration)
        {
            SetState(AxeState.Cooldown);
        }
    }

    void UpdateColliderDuringAttack(float attackProgress)
    {
        bool shouldBeActive = attackProgress >= attackStartPercent && 
                             attackProgress <= attackEndPercent && 
                             !hasHitThisAttack;

        bool wasActive = canHitThisAttack;
        canHitThisAttack = shouldBeActive;

        // Enable/disable collider based on attack window
        if (axeCollider != null && axeCollider.enabled != shouldBeActive)
        {
            axeCollider.enabled = shouldBeActive;

            Debug.Log($"[ATTACK] Collider {(shouldBeActive ? "enabled" : "disabled")} - Progress: {attackProgress:F2}, Window: {attackStartPercent}-{attackEndPercent}");
        }
    }

    #endregion

    #region Collision Detection

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[COLLISION] Collision detected with {collision.gameObject.name}");
        Debug.Log($"[COLLISION] Current state: {currentState}");
        Debug.Log($"[COLLISION] Has hit this attack: {hasHitThisAttack}");
        Debug.Log($"[COLLISION] Target layer: {collision.gameObject.layer}");
        Debug.Log($"[COLLISION] Enemy layers mask: {enemyLayers.value}");
        
        // Only process hits when attacking and haven't hit yet
        if (hasHitThisAttack || currentState != AxeState.Attacking)
        {
            Debug.Log($"[COLLISION] Hit rejected - hasHit: {hasHitThisAttack}, state: {currentState}");
            return;
        }

        // Check if object is on correct layer
        if (!IsValidTarget(collision.collider))
        {
            Debug.Log($"[COLLISION] Hit rejected - invalid target layer");
            return;
        }

        Debug.Log($"[COLLISION] Processing hit with {collision.gameObject.name}!");
        // Process the hit
        ProcessHit(collision);
    }

    bool IsValidTarget(Collider target)
    {
        int targetLayerMask = 1 << target.gameObject.layer;
        bool isValid = (targetLayerMask & enemyLayers) != 0;
        
        if (!isValid)
        {
            Debug.Log($"[LAYER MISMATCH] Object {target.name} is on layer {target.gameObject.layer} (mask: {targetLayerMask}), but enemyLayers mask is {enemyLayers.value}");
            Debug.Log($"[LAYER FIX] To hit this object, set enemyLayers to include layer {target.gameObject.layer} in the inspector");
        }
        
        return isValid;
    }

    void ProcessHit(Collision collision)
    {
        // Mark that we've hit something this attack
        hasHitThisAttack = true;

        // Get collision info
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.collider.transform.position;
        Collider hitCollider = collision.collider;

        // Calculate damage
        float damage = lastAttackType == AttackType.LeftClick ? primaryDamage : secondaryDamage;

        // Apply damage
        var damageable = hitCollider.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);

            if (enableDebugLogs)
            {
                Debug.Log($"[{axeName}] Hit {hitCollider.name} for {damage} damage");
            }
        }

        // Play hit effects
        PlayHitEffects(hitPoint);

        // Play hit sound
        PlaySound(hitSound);
    }

    #endregion

    #region Blocking System

    void StartBlock()
    {
        isBlocking = true;
        SetState(AxeState.Blocking);

        if (axeAnimator != null)
        {
            axeAnimator.SetTrigger(blockTrigger);
        }

        PlaySound(blockSound);

        if (enableDebugLogs)
        {
            Debug.Log($"[{axeName}] Started blocking");
        }
    }

    void UpdateBlocking()
    {
        // Drain stamina while blocking
        float staminaDrain = blockStaminaCost * Time.deltaTime;
        currentStamina = Mathf.Max(0f, currentStamina - staminaDrain);

        // Stop blocking if stamina runs out
        if (currentStamina <= 0)
        {
            StopBlock();
        }
    }

    void StopBlock()
    {
        isBlocking = false;
        SetState(AxeState.Idle);

        if (enableDebugLogs)
        {
            Debug.Log($"[{axeName}] Stopped blocking");
        }
    }

    public bool IsBlocking()
    {
        return isBlocking && currentState == AxeState.Blocking;
    }

    public float GetBlockedDamage(float incomingDamage)
    {
        if (IsBlocking())
        {
            return incomingDamage * (1f - blockDamageReduction);
        }
        return incomingDamage;
    }

    #endregion

    #region Effects and Audio

    void PlayHitEffects(Vector3 hitPosition)
    {
        // Play particle effect at hit point
        if (hitEffect != null)
        {
            hitEffect.transform.position = hitPosition;
            hitEffect.Play();
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void StartSwingTrail()
    {
        if (swingTrail != null)
        {
            swingTrail.enabled = true;
            swingTrail.Clear();
            swingTrail.time = trailDuration;
        }
    }

    void StopSwingTrail()
    {
        if (swingTrail != null && enableTrailOnlyDuringAttack)
        {
            StartCoroutine(FadeOutTrail());
        }
    }

    IEnumerator FadeOutTrail()
    {
        if (swingTrail == null) yield break;

        yield return new WaitForSeconds(swingTrail.time);

        if (enableTrailOnlyDuringAttack)
        {
            swingTrail.enabled = false;
        }
    }

    #endregion

    #region Stamina System

    void RegenerateStamina()
    {
        if (currentState != AxeState.Blocking && currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        }
    }

    #endregion

    #region Debug and Visualization

    void OnDrawGizmosSelected()
    {
        if (!showColliderGizmos || axeCollider == null)
            return;

        // Set color based on collider state
        if (canHitThisAttack && currentState == AxeState.Attacking)
        {
            Gizmos.color = Color.green; // Active and can hit
        }
        else if (currentState == AxeState.Attacking)
        {
            Gizmos.color = Color.orange; // Attacking but not in hit window
        }
        else
        {
            Gizmos.color = Color.gray; // Inactive
        }

        // Draw collider bounds
        Gizmos.matrix = axeCollider.transform.localToWorldMatrix;

        if (axeCollider is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
            if (canHitThisAttack)
                Gizmos.DrawCube(box.center, box.size * 0.8f);
        }
        else if (axeCollider is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            if (canHitThisAttack)
                Gizmos.DrawSphere(sphere.center, sphere.radius * 0.8f);
        }
        else if (axeCollider is CapsuleCollider capsule)
        {
            Gizmos.DrawWireSphere(capsule.center, capsule.radius);
            if (canHitThisAttack)
                Gizmos.DrawSphere(capsule.center, capsule.radius * 0.8f);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    void OnGUI()
    {
        if (!showDebugInfo)
            return;

        GUILayout.BeginArea(new Rect(10, 200, 350, 200));
        GUILayout.Label($"=== {axeName} Debug ===");
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"Stamina: {currentStamina:F0}/{maxStamina}");
        GUILayout.Label($"Blocking: {(isBlocking ? "YES" : "NO")}");
        GUILayout.Label("");
        GUILayout.Label("=== Collision ===");
        GUILayout.Label($"Can Hit: {canHitThisAttack}");
        GUILayout.Label($"Has Hit: {hasHitThisAttack}");
        GUILayout.Label($"Collider Enabled: {(axeCollider?.enabled ?? false)}");

        if (currentState == AxeState.Attacking)
        {
            float attackDuration = lastAttackType == AttackType.LeftClick ? primaryAttackDuration : secondaryAttackDuration;
            float progress = stateTimer / attackDuration;
            GUILayout.Label($"Attack Progress: {progress:F2}");
            
            bool inWindow = progress >= attackStartPercent && progress <= attackEndPercent;
            GUILayout.Label($"In Hit Window: {inWindow}");
        }

        GUILayout.Label("");
        GUILayout.Label("Controls:");
        GUILayout.Label($"Left Click - Primary ({primaryAttackStaminaCost} stamina)");
        GUILayout.Label($"Right Click - Heavy ({secondaryAttackStaminaCost} stamina)");
        GUILayout.Label($"Middle Click - Block ({blockStaminaCost}/sec stamina)");
        GUILayout.EndArea();
    }

    #endregion
}
