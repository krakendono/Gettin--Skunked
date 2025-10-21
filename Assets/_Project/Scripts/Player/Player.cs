using UnityEngine;

public class Player : MonoBehaviour
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
    
    void Start()
    {
        // Get components
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        
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
        
        // Handle input
        HandleMouseLook();
        HandleMovement();
        HandleJump();
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        
        // Move the controller
        controller.Move(velocity * Time.deltaTime);
    }
    
    void HandleMouseLook()
    {
        // Handle cursor lock/unlock controls
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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
        
        // Click to lock cursor when unlocked
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
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
