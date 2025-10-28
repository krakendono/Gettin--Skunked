using UnityEngine;

public class PlayerBased : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    
    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    // Private variables
    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity;
    private bool isGrounded;
    private float verticalRotation = 0f;
    private float horizontalRotation = 0f;
    
    // Rotation locking
    private bool wasMovementLocked = false;
    private float lockedVerticalRotation;
    private float lockedHorizontalRotation;
    
    // UI System references
    private InventorySystem inventorySystem;
    private CraftingSystem craftingSystem;
    
    void Start()
    {
        // Get components
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        
        // Find UI systems for integration
        inventorySystem = FindFirstObjectByType<InventorySystem>();
        craftingSystem = FindFirstObjectByType<CraftingSystem>();
        
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // If no camera found, try to find main camera
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        // Initialize rotation values
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // When losing focus (alt-tabbing or clicking away), unlock cursor
        if (!hasFocus)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnDestroy()
    {
        // Ensure cursor is unlocked when object is destroyed
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDisable()
    {
        // Ensure cursor is unlocked when script is disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // Ground check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }
        
        // Simple check: Should player be able to move?
        bool canMove = PlayerInputManager.CanPlayerMove();
        
        // Handle cursor management
        HandleCursorState();
        
        // ONLY handle input if player can move
        if (canMove)
        {
            HandleMouseLook();
            HandleMovement();
            HandleJump();
            wasMovementLocked = false;
        }
        else
        {
            // Lock rotation when movement is disabled
            if (!wasMovementLocked)
            {
                // Store current rotation when first locking
                lockedVerticalRotation = verticalRotation;
                lockedHorizontalRotation = horizontalRotation;
                wasMovementLocked = true;
            }
            
            // Force rotation to stay locked
            verticalRotation = lockedVerticalRotation;
            horizontalRotation = lockedHorizontalRotation;
            
            // Apply locked rotations
            transform.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
        }
        
        // Always handle gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }
    
    void HandleMouseLook()
    {
        // Only process mouse look if cursor is locked
        if (Cursor.lockState != CursorLockMode.Locked)
            return;
            
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Update rotation values
        horizontalRotation += mouseX;
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        
        // Apply rotations
        transform.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }
    
    bool IsAnyUIOpen()
    {
        // Check if inventory is open
        if (inventorySystem != null && inventorySystem.IsInventoryOpen())
            return true;
            
        // Check if crafting is open
        if (craftingSystem != null && craftingSystem.IsCraftingMenuOpen())
            return true;
            
        return false;
    }
    
    void HandleCursorState()
    {
        bool isUIOpen = IsAnyUIOpen();
        
        // Handle ESC key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isUIOpen)
            {
                // UI systems will handle closing menus
                return;
            }
            else
            {
                // Toggle cursor lock
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
        
        // Auto-manage cursor for UI
        if (isUIOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Auto-lock cursor when UI is closed (no clicking required)
            if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    
    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal"); // A/D keys
        float vertical = Input.GetAxis("Vertical");     // W/S keys
        
        // Check if sprinting
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        
        // Calculate movement direction based on player's Y rotation
        Vector3 direction = (transform.forward * vertical + transform.right * horizontal).normalized;
        
        // Move the player (only horizontal movement)
        controller.Move(direction * currentSpeed * Time.deltaTime);
    }
    
    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw ground check sphere in scene view
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}
