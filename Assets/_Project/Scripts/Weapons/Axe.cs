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
    public float attackRange = 2.5f;
    public float attackAngle = 90f; // Degrees for swing arc
    
    [Header("Attack Timing")]
    public float primaryAttackDuration = 0.6f;
    public float secondaryAttackDuration = 1.0f;
    public float attackCooldown = 0.3f;
    public float blockDuration = 2.0f; // How long block lasts if held
    
    [Header("Attack Points")]
    public Transform attackPoint; // Point from which attack originates
    public LayerMask enemyLayers = 1; // What layers can be hit
    
    [Header("Effects")]
    public ParticleSystem swingEffect;
    public ParticleSystem hitEffect;
    public AudioClip primarySwingSound;
    public AudioClip secondarySwingSound;
    public AudioClip hitSound;
    public AudioClip blockSound;
    
    [Header("Animation")]
    public Animator axeAnimator; // For axe animations
    public string primaryAttackTrigger = "PrimaryAttack";
    public string secondaryAttackTrigger = "SecondaryAttack";
    public string blockTrigger = "Block";
    public string idleTrigger = "Idle";
    
    [Header("Blocking")]
    public float blockDamageReduction = 0.8f; // 80% damage reduction when blocking
    public float blockStaminaCost = 10f;
    public float maxStamina = 100f;
    public float staminaRegenRate = 20f; // Per second
    
    [Header("UI")]
    public bool showDebugInfo = true;
    public bool enableDebugLogs = false;
    
    // Private variables
    private AxeState currentState = AxeState.Idle;
    private AttackType lastAttackType;
    private float stateTimer = 0f;
    private AudioSource audioSource;
    private Camera playerCamera;
    private float currentStamina;
    private bool isBlocking = false;
    
    // Attack detection
    private bool hasHitThisAttack = false;
    
    void Start()
    {
        // Get components
        playerCamera = GetComponentInParent<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        // Set attack point if not assigned
        if (attackPoint == null)
            attackPoint = transform;
            
        // Initialize stamina
        currentStamina = maxStamina;
        
        // Set initial state
        SetState(AxeState.Idle);
    }

    void Update()
    {
        HandleInput();
        UpdateState();
        RegenerateStamina();
    }
    
    void HandleInput()
    {
        // Only handle input when idle or blocking
        if (currentState != AxeState.Idle && currentState != AxeState.Blocking)
            return;
            
        // Primary attack (Left Click)
        if (Input.GetMouseButtonDown(0))
        {
            if (currentState == AxeState.Idle)
            {
                StartAttack(AttackType.LeftClick);
            }
        }
        
        // Secondary attack (Right Click)
        if (Input.GetMouseButtonDown(1))
        {
            if (currentState == AxeState.Idle)
            {
                StartAttack(AttackType.RightClick);
            }
        }
        
        // Block (Mouse Wheel - Middle Click)
        if (Input.GetMouseButtonDown(2))
        {
            if (currentState == AxeState.Idle && currentStamina >= blockStaminaCost)
            {
                StartBlock();
            }
        }
        
        // Stop blocking when mouse wheel is released
        if (Input.GetMouseButtonUp(2))
        {
            if (currentState == AxeState.Blocking)
            {
                StopBlock();
            }
        }
    }
    
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
    
    void StartAttack(AttackType attackType)
    {
        lastAttackType = attackType;
        hasHitThisAttack = false;
        SetState(AxeState.Attacking);
        
        // Play animation
        if (axeAnimator != null)
        {
            if (attackType == AttackType.LeftClick)
            {
                axeAnimator.SetTrigger(primaryAttackTrigger);
            }
            else
            {
                axeAnimator.SetTrigger(secondaryAttackTrigger);
            }
        }
        
        // Play sound
        AudioClip soundToPlay = attackType == AttackType.LeftClick ? primarySwingSound : secondarySwingSound;
        PlaySound(soundToPlay);
        
        // Play swing effect
        if (swingEffect != null)
        {
            swingEffect.Play();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Started {attackType} attack");
        }
    }
    
    void UpdateAttacking()
    {
        float attackDuration = lastAttackType == AttackType.LeftClick ? primaryAttackDuration : secondaryAttackDuration;
        float attackProgress = stateTimer / attackDuration;
        
        // Check for hits during the middle portion of the attack (30% to 70% of animation)
        if (attackProgress >= 0.3f && attackProgress <= 0.7f && !hasHitThisAttack)
        {
            CheckForHits();
        }
        
        // End attack when duration is complete
        if (stateTimer >= attackDuration)
        {
            SetState(AxeState.Cooldown);
        }
    }
    
    void CheckForHits()
    {
        Vector3 attackOrigin = attackPoint.position;
        Vector3 attackDirection = attackPoint.forward;
        
        // Get all colliders within attack range
        Collider[] hitColliders = Physics.OverlapSphere(attackOrigin, attackRange, enemyLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            // Check if the target is within the attack angle
            Vector3 directionToTarget = (hitCollider.transform.position - attackOrigin).normalized;
            float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);
            
            if (angleToTarget <= attackAngle / 2f)
            {
                // We hit something!
                hasHitThisAttack = true;
                
                float damage = lastAttackType == AttackType.LeftClick ? primaryDamage : secondaryDamage;
                
                // Apply damage if it's damageable
                var damageable = hitCollider.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"Axe hit {hitCollider.name} for {damage} damage");
                    }
                }
                
                // Play hit effects
                PlayHitEffects(hitCollider.transform.position);
                
                // Only hit one target per swing
                break;
            }
        }
    }
    
    void StartBlock()
    {
        isBlocking = true;
        SetState(AxeState.Blocking);
        
        // Consume stamina
        currentStamina -= blockStaminaCost;
        
        // Play animation
        if (axeAnimator != null)
        {
            axeAnimator.SetTrigger(blockTrigger);
        }
        
        // Play sound
        PlaySound(blockSound);
        
        if (enableDebugLogs)
        {
            Debug.Log("Started blocking");
        }
    }
    
    void UpdateBlocking()
    {
        // Drain stamina while blocking
        currentStamina -= Time.deltaTime * (blockStaminaCost / 2f); // Slower drain while holding
        
        // Stop blocking if out of stamina or max duration reached
        if (currentStamina <= 0 || stateTimer >= blockDuration)
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
            Debug.Log("Stopped blocking");
        }
    }
    
    void SetState(AxeState newState)
    {
        currentState = newState;
        stateTimer = 0f;
        
        // Set idle animation when returning to idle
        if (newState == AxeState.Idle && axeAnimator != null)
        {
            axeAnimator.SetTrigger(idleTrigger);
        }
    }
    
    void RegenerateStamina()
    {
        if (currentState != AxeState.Blocking && currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
    }
    
    void PlayHitEffects(Vector3 hitPosition)
    {
        // Play hit particle effect
        if (hitEffect != null)
        {
            hitEffect.transform.position = hitPosition;
            hitEffect.Play();
        }
        
        // Play hit sound
        PlaySound(hitSound);
    }
    
    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    // Public method for other scripts to check if axe is blocking (for damage reduction)
    public bool IsBlocking()
    {
        return isBlocking && currentState == AxeState.Blocking;
    }
    
    // Public method for applying damage reduction when blocking
    public float GetBlockedDamage(float incomingDamage)
    {
        if (IsBlocking())
        {
            return incomingDamage * (1f - blockDamageReduction);
        }
        return incomingDamage;
    }
    
    void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;
            
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        
        // Draw attack angle
        Vector3 attackDirection = attackPoint.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -attackAngle / 2f, 0) * attackDirection;
        Vector3 rightBoundary = Quaternion.Euler(0, attackAngle / 2f, 0) * attackDirection;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(attackPoint.position, leftBoundary * attackRange);
        Gizmos.DrawRay(attackPoint.position, rightBoundary * attackRange);
        
        // Draw arc
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.Lerp(-attackAngle / 2f, attackAngle / 2f, i / 9f);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * attackDirection;
            Gizmos.DrawRay(attackPoint.position, direction * attackRange);
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo)
            return;
            
        GUILayout.BeginArea(new Rect(10, 200, 250, 180));
        GUILayout.Label($"Axe: {axeName}");
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"Stamina: {currentStamina:F0}/{maxStamina}");
        GUILayout.Label($"Blocking: {(isBlocking ? "YES" : "NO")}");
        GUILayout.Label("");
        GUILayout.Label("Controls:");
        GUILayout.Label("Left Click - Primary Attack");
        GUILayout.Label("Right Click - Heavy Attack");
        GUILayout.Label("Mouse Wheel - Block");
        GUILayout.EndArea();
    }
}
