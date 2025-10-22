using UnityEngine;
using System.Collections;

[System.Serializable]
public enum WoodState
{
    Tree,      // Initial state - large tree
    Log,       // After tree is chopped down
    Wood,      // After log is chopped up
    Destroyed  // Final state - object is destroyed
}

public class Wood : MonoBehaviour, IDamageable
{
    [Header("This Resource Settings")]
    public WoodState currentState = WoodState.Tree;
    public float maxDurability = 100f;
    public string stateName = "Tree";
    
    [Header("Audio & Effects")]
    public AudioClip hitSound;
    public AudioClip destroySound;
    public ParticleSystem hitEffect;
    public ParticleSystem destroyEffect;
    
    [Header("Next State Transition")]
    public GameObject nextStatePrefab; // What to spawn when this is destroyed
    public int spawnCount = 1; // How many of the next state to spawn (e.g., 1 tree -> 3 logs)
    
    [Header("Resource Rewards")]
    public int resourceAmount = 0; // How much resource this gives when destroyed
    public GameObject dropPrefab; // Optional pickup prefab to spawn
    
    [Header("General Settings")]
    public float damageReduction = 1f; // Multiplier for incoming damage
    public bool enableDebugLogs = false;
    public bool showDebugInfo = true;
    
    [Header("Transition Settings")]
    public float transitionDelay = 0.5f; // Delay before spawning next state
    public bool enableStateTransitionEffects = true;
    
    [Header("Drop Settings")]
    public float dropForce = 5f;
    public float spawnRadius = 2f; // Radius around this object to spawn next state
    public LayerMask groundLayer = 1;
    
    // Private variables
    private float currentDurability;
    private AudioSource audioSource;
    private bool isBeingDestroyed = false;
    
    // Events for external systems (like inventory or UI)
    public System.Action<WoodState, int> OnResourceGathered; // (state, amount)
    public System.Action<WoodState> OnStateChanged;
    
    void Start()
    {
        // Get or add audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        // Initialize durability
        currentDurability = maxDurability;
        
        if (enableDebugLogs)
        {
            Debug.Log($"Wood resource initialized as {currentState} ({stateName}) with {currentDurability} durability");
        }
        
        // Notify external systems
        OnStateChanged?.Invoke(currentState);
    }
    
    public void TakeDamage(float damage)
    {
        // Don't take damage if already being destroyed
        if (isBeingDestroyed)
            return;
        
        // Apply damage reduction
        float actualDamage = damage * damageReduction;
        currentDurability -= actualDamage;
        
        // Ensure durability doesn't go below 0
        currentDurability = Mathf.Max(currentDurability, 0f);
        
        if (enableDebugLogs)
        {
            Debug.Log($"{stateName} took {actualDamage} damage. Durability: {currentDurability:F1}/{maxDurability}");
        }
        
        // Play hit effects
        PlayHitEffects();
        
        // Check if this resource is depleted
        if (currentDurability <= 0)
        {
            StartDestruction();
        }
    }
    
    void PlayHitEffects()
    {
        // Play hit sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
        
        // Play hit particle effect
        if (hitEffect != null)
        {
            hitEffect.transform.position = transform.position;
            hitEffect.Play();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"{stateName} hit effects played");
        }
    }
    
    void StartDestruction()
    {
        if (isBeingDestroyed) return;
        
        isBeingDestroyed = true;
        StartCoroutine(DestructionCoroutine());
    }
    
    IEnumerator DestructionCoroutine()
    {
        if (enableDebugLogs)
        {
            Debug.Log($"{stateName} durability depleted, starting destruction sequence");
        }
        
        // Play destroy effects
        if (enableStateTransitionEffects)
        {
            // Play destroy sound
            if (destroySound != null && audioSource != null)
            {
                audioSource.PlayOneShot(destroySound);
            }
            
            // Play destroy particle effect
            if (destroyEffect != null)
            {
                destroyEffect.transform.position = transform.position;
                destroyEffect.Play();
            }
        }
        
        // Give resources if any
        if (resourceAmount > 0)
        {
            GiveResources();
        }
        
        // Wait for transition delay
        yield return new WaitForSeconds(transitionDelay);
        
        // Spawn next state if there is one
        if (nextStatePrefab != null)
        {
            SpawnNextState();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"{stateName} destroyed and removed from scene");
        }
        
        // Destroy this GameObject
        Destroy(gameObject);
    }
    
    void SpawnNextState()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            // Calculate spawn position
            Vector3 spawnPosition = GetSpawnPosition();
            
            // Spawn the next state
            GameObject nextStateObject = Instantiate(nextStatePrefab, spawnPosition, GetSpawnRotation());
            
            // Add some random force if it has a rigidbody (for logs falling from trees, etc.)
            Rigidbody rb = nextStateObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 randomDirection = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.2f, 0.8f),
                    Random.Range(-1f, 1f)
                ).normalized;
                
                rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"Spawned {nextStatePrefab.name} at {spawnPosition}");
            }
        }
    }
    
    Vector3 GetSpawnPosition()
    {
        // Random position within spawn radius
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // Try to find ground position
        if (Physics.Raycast(spawnPosition + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, groundLayer))
        {
            spawnPosition.y = hit.point.y;
        }
        else
        {
            // If no ground found, use current object's ground level
            spawnPosition.y = transform.position.y;
        }
        
        return spawnPosition;
    }
    
    Quaternion GetSpawnRotation()
    {
        // Random rotation, or keep current rotation depending on what makes sense
        return Random.rotation;
    }
    
    void GiveResources()
    {
        // Notify external systems about resource gathering
        OnResourceGathered?.Invoke(currentState, resourceAmount);
        
        // Spawn drop prefab if assigned
        if (dropPrefab != null)
        {
            SpawnResourceDrop();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Gathered {resourceAmount} resources from {stateName}");
        }
    }
    
    void SpawnResourceDrop()
    {
        Vector3 dropPosition = transform.position + Vector3.up * 0.5f;
        
        // Try to find ground position
        if (Physics.Raycast(dropPosition, Vector3.down, out RaycastHit hit, 10f, groundLayer))
        {
            dropPosition = hit.point + Vector3.up * 0.1f;
        }
        
        // Spawn the drop
        GameObject drop = Instantiate(dropPrefab, dropPosition, Random.rotation);
        
        // Add some random force if it has a rigidbody
        Rigidbody dropRb = drop.GetComponent<Rigidbody>();
        if (dropRb != null)
        {
            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;
            
            dropRb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
        }
    }
    
    // Public methods for external access
    public WoodState GetCurrentState() => currentState;
    public float GetCurrentDurability() => currentDurability;
    public float GetMaxDurability() => maxDurability;
    public float GetDurabilityPercentage() => currentDurability / maxDurability;
    public string GetStateName() => stateName;
    
    void OnGUI()
    {
        if (!showDebugInfo)
            return;
            
        // Only show if this is close to camera
        Camera cam = Camera.main;
        if (cam == null) return;
        
        float distanceToCamera = Vector3.Distance(transform.position, cam.transform.position);
        if (distanceToCamera > 10f) return; // Only show for nearby resources
        
        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        if (screenPos.z > 0) // Only show if in front of camera
        {
            Rect labelRect = new Rect(screenPos.x - 75, Screen.height - screenPos.y - 60, 150, 60);
            
            GUI.Box(labelRect, "");
            GUILayout.BeginArea(labelRect);
            GUILayout.Label($"Type: {stateName}");
            GUILayout.Label($"Durability: {currentDurability:F0}/{maxDurability:F0}");
            GUILayout.Label($"Progress: {(GetDurabilityPercentage() * 100):F0}%");
            GUILayout.EndArea();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw spawn radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        
        // Draw durability bar above object
        Vector3 barPosition = transform.position + Vector3.up * 3f;
        float barWidth = 2f;
        float barHeight = 0.2f;
        
        // Background bar
        Gizmos.color = Color.red;
        Gizmos.DrawCube(barPosition, new Vector3(barWidth, barHeight, 0.1f));
        
        // Durability bar
        if (maxDurability > 0)
        {
            float durabilityPercent = GetDurabilityPercentage();
            Gizmos.color = Color.Lerp(Color.red, Color.green, durabilityPercent);
            Vector3 durabilityBarSize = new Vector3(barWidth * durabilityPercent, barHeight, 0.1f);
            Vector3 durabilityBarPos = barPosition - new Vector3(barWidth * (1 - durabilityPercent) * 0.5f, 0, 0);
            Gizmos.DrawCube(durabilityBarPos, durabilityBarSize);
        }
        
        // Show what will spawn next
        if (nextStatePrefab != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < spawnCount && i < 10; i++) // Limit visual display to 10
            {
                Vector2 randomCircle = new Vector2(
                    Mathf.Cos(i * Mathf.PI * 2 / spawnCount),
                    Mathf.Sin(i * Mathf.PI * 2 / spawnCount)
                ) * spawnRadius * 0.7f;
                
                Vector3 previewPos = transform.position + new Vector3(randomCircle.x, 0.5f, randomCircle.y);
                Gizmos.DrawWireCube(previewPos, Vector3.one * 0.5f);
            }
        }
    }
}
