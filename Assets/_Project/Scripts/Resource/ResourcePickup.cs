using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public string resourceName = "Wood";
    public ResourceType resourceType = ResourceType.Wood;
    public int quantity = 1;
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
    
    private Transform player;
    private InventorySystem playerInventory;
    private bool isInRange = false;
    private Vector3 startPosition;
    private AudioSource audioSource;
    
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
                playerInventory = FindObjectOfType<InventorySystem>();
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
        
        Debug.Log($"Resource pickup created: {resourceName} x{quantity}");
    }
    
    void Update()
    {
        HandleBobbing();
        CheckPlayerDistance();
        HandleInput();
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
        if (playerInventory == null)
        {
            Debug.LogWarning("No inventory system found on player!");
            return;
        }
        
        // Create resource item
        ResourceItem resourceItem = new ResourceItem(resourceName, resourceType, quantity);
        
        // Try to add to inventory
        if (playerInventory.AddItem(resourceItem))
        {
            // Successful pickup
            Debug.Log($"Picked up {quantity}x {resourceName}");
            
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
            Debug.Log("Inventory is full!");
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
        UnityEditor.Handles.Label(labelPos, $"{resourceName} x{quantity}");
        #endif
    }
}