using Fusion;
using UnityEngine;

public class ResourcePickup : NetworkBehaviour
{
    [Header("Pickup Settings")]
    public string resourceName = "Wood";
    public ResourceType resourceType = ResourceType.Wood;
    public int quantity = 1;
    // Networked replicated data used when running a session
    [Networked] public NetworkString<_32> NetResourceName { get; set; }
    [Networked] public ResourceType NetResourceType { get; set; }
    [Networked] public int NetQuantity { get; set; }

    public float pickupRange = 3f;
    public bool autoPickup = true;
    public KeyCode manualPickupKey = KeyCode.E;
    
    [Header("Visual Feedback")]
    public GameObject pickupEffect;
    public AudioClip pickupSound;
    public bool destroyOnPickup = true;
    public float bobSpeed = 1f;
    public float bobHeight = 0.5f;
    
    [Header("UI")]
    public bool showPickupPrompt = true;
    public string pickupPromptText = "Press E to pick up";
    
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
        // Find player and inventory system (offline fallback)
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
            itemRigidbody.mass = 0.1f; // Light weight for pickups
        }
        
        // Add collider if none exists
        if (GetComponent<Collider>() == null)
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.5f; // Small pickup size
        }
        
        Debug.Log($"Resource pickup created: {resourceName} x{quantity}");
    }
    
    void Update()
    {
        HandleBobbing();
        CheckPlayerDistance();
        HandleInput();
        CheckFloorProtection();
    }

    // Helpers to get current values (networked in session, local otherwise)
    private string CurrentName => (Runner != null && Runner.IsRunning) ? NetResourceName.ToString() : resourceName;
    private ResourceType CurrentType => (Runner != null && Runner.IsRunning) ? NetResourceType : resourceType;
    private int CurrentQuantity => (Runner != null && Runner.IsRunning) ? NetQuantity : quantity;
    
    void CheckFloorProtection()
    {
        if (!enableFloorProtection) return;
        
        // Check if item has fallen below deletion threshold
        if (transform.position.y < deleteHeightThreshold)
        {
            Debug.Log($"Deleting {resourceName} - fell below floor threshold ({deleteHeightThreshold})");
            Destroy(gameObject);
            return;
        }
        
        // Check if item is stuck in the floor (optional additional check)
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, floorCheckDistance, floorLayerMask))
        {
            // If we're inside the floor, move up slightly
            if (hit.distance < 0.1f)
            {
                Vector3 correctedPosition = hit.point + Vector3.up * 0.2f;
                transform.position = correctedPosition;
                
                // Stop downward velocity
                if (itemRigidbody != null && itemRigidbody.linearVelocity.y < 0)
                {
                    itemRigidbody.linearVelocity = new Vector3(itemRigidbody.linearVelocity.x, 0, itemRigidbody.linearVelocity.z);
                }
            }
        }
    }
    
    void HandleBobbing()
    {
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
        
        // Auto pickup if enabled
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
        // If running in a Fusion session, request pickup via NetworkInventory RPC (server authoritative)
        if (Runner != null && Runner.IsRunning)
        {
            var inv = FindLocalNetworkInventory();
            if (inv != null)
            {
                inv.RPC_RequestPickup(Object.Id);
            }
            return;
        }

        // Offline fallback: use local InventorySystem
        if (playerInventory == null)
        {
            Debug.LogWarning("No inventory system found on player!");
            return;
        }
        
        ResourceItem resourceItem = new ResourceItem(CurrentName, CurrentType, CurrentQuantity);
        if (playerInventory.AddItem(resourceItem))
        {
            Debug.Log($"Picked up {CurrentQuantity}x {CurrentName}");
            PlayPickupEffects();
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
            Debug.Log("Inventory is full!");
        }
    }

    private NetworkInventory FindLocalNetworkInventory()
    {
        // Try to get local player's NetworkInventory (InputAuthority)
        if (Runner == null) return null;
        foreach (var no in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(no, out var obj))
            {
                var inv = obj.GetComponent<NetworkInventory>();
                if (inv != null && inv.Object != null && inv.Object.HasInputAuthority)
                {
                    return inv;
                }
            }
        }
        // Fallback: find any with input authority
        var any = FindFirstObjectByType<NetworkInventory>();
        if (any != null && any.Object != null && any.Object.HasInputAuthority) return any;
        return null;
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
            Rect promptRect = new Rect(screenPos.x - 75, Screen.height - screenPos.y - 30, 150, 30);
            GUI.Box(promptRect, pickupPromptText);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
        
        // Draw pickup info
        Gizmos.color = Color.yellow;
        Vector3 labelPos = transform.position + Vector3.up * 2f;
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"{CurrentName} x{CurrentQuantity}");
        #endif
    }
}