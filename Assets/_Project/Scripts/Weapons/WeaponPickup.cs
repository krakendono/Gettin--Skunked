using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Settings")]
    public string weaponName = "Battle Axe";
    public WeaponType weaponType = WeaponType.Melee;
    public float damage = 50f;
    public float maxDurability = 100f;
    public GameObject weaponPrefab;
    
    [Header("Pickup Settings")]
    public float pickupRange = 3f;
    public bool autoPickup = false; // Weapons usually require manual pickup
    public KeyCode manualPickupKey = KeyCode.E;
    
    [Header("Visual Feedback")]
    public GameObject pickupEffect;
    public AudioClip pickupSound;
    public bool destroyOnPickup = true;
    public float rotationSpeed = 30f;
    public float bobSpeed = 1f;
    public float bobHeight = 0.2f;
    
    [Header("UI")]
    public bool showPickupPrompt = true;
    public string pickupPromptText = "Press E to pick up weapon";
    
    [Header("Floor Protection")]
    public float deleteHeightThreshold = -50f; // Delete if item falls below this Y position
    public bool enableFloorProtection = true;
    public LayerMask floorLayerMask = 1; // What layers count as "floor"
    public float floorCheckDistance = 1f;
    
    private Transform player;
    private InventorySystem playerInventory;
    private bool isInRange = false;
    private Vector3 startPosition;
    private AudioSource audioSource;
    private Rigidbody itemRigidbody;
    
    void Start()
    {
        // Find player and inventory system
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerInventory = playerObj.GetComponent<InventorySystem>();
            
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<InventorySystem>();
            }
        }
        
        // Store initial position for bobbing animation
        startPosition = transform.position;
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && pickupSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Get or add Rigidbody for physics
        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
            itemRigidbody.mass = 0.5f; // Slightly heavier than resources
        }
        
        // Add collider if none exists
        if (GetComponent<Collider>() == null)
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.7f; // Slightly larger than resources
        }
        
        Debug.Log($"Weapon pickup created: {weaponName}");
    }
    
    void Update()
    {
        HandleAnimations();
        CheckPlayerDistance();
        HandleInput();
        CheckFloorProtection();
    }
    
    void CheckFloorProtection()
    {
        if (!enableFloorProtection) return;
        
        // Check if item has fallen below deletion threshold
        if (transform.position.y < deleteHeightThreshold)
        {
            Debug.Log($"Deleting {weaponName} - fell below floor threshold ({deleteHeightThreshold})");
            Destroy(gameObject);
            return;
        }
        
        // Check if item is stuck in the floor
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, floorCheckDistance, floorLayerMask))
        {
            // If we're inside the floor, move up slightly
            if (hit.distance < 0.1f)
            {
                Vector3 correctedPosition = hit.point + Vector3.up * 0.3f;
                transform.position = correctedPosition;
                
                // Stop downward velocity
                if (itemRigidbody != null && itemRigidbody.linearVelocity.y < 0)
                {
                    itemRigidbody.linearVelocity = new Vector3(itemRigidbody.linearVelocity.x, 0, itemRigidbody.linearVelocity.z);
                }
            }
        }
    }
    
    void HandleAnimations()
    {
        // Rotation animation
        if (rotationSpeed > 0)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        }
        
        // Bobbing animation
        if (bobHeight > 0)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
    
    void CheckPlayerDistance()
    {
        if (player == null) return;
        
        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = isInRange;
        isInRange = distance <= pickupRange;
        
        // Auto pickup if enabled (usually not for weapons)
        if (isInRange && autoPickup && !wasInRange)
        {
            TryPickup();
        }
    }
    
    void HandleInput()
    {
        if (isInRange && !autoPickup && Input.GetKeyDown(manualPickupKey))
        {
            TryPickup();
        }
    }
    
    void TryPickup()
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("No inventory system found on player!");
            return;
        }
        
        // Create weapon item
        WeaponItem weaponItem = new WeaponItem(weaponName, weaponType, damage, maxDurability, weaponPrefab);
        
        // Try to add to inventory
        if (playerInventory.AddItem(weaponItem))
        {
            // Successful pickup
            Debug.Log($"Picked up weapon: {weaponName}");
            
            // Play effects
            PlayPickupEffects();
            
            // Destroy or hide the pickup
            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.Log("Weapon inventory is full!");
            // Could show UI message here
        }
    }
    
    void PlayPickupEffects()
    {
        // Play sound
        if (pickupSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }
        
        // Spawn effect
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, transform.rotation);
        }
    }
    
    void OnGUI()
    {
        if (!showPickupPrompt || !isInRange || autoPickup) return;
        
        if (player == null) return;
        
        // Show pickup prompt
        Camera cam = Camera.main;
        if (cam == null) return;
        
        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1f);
        if (screenPos.z > 0)
        {
            Rect promptRect = new Rect(screenPos.x - 100, Screen.height - screenPos.y - 30, 200, 30);
            GUI.Box(promptRect, pickupPromptText);
            
            // Show weapon stats
            Rect statsRect = new Rect(screenPos.x - 100, Screen.height - screenPos.y - 60, 200, 30);
            GUI.Label(statsRect, $"{weaponName} - Damage: {damage:F0}");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
        
        // Draw weapon info
        Gizmos.color = Color.cyan;
        Vector3 labelPos = transform.position + Vector3.up * 2f;
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"{weaponName}\nDmg: {damage:F0}");
        #endif
    }
}