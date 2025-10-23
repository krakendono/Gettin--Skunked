using UnityEngine;

[System.Serializable]
public enum VehicleType
{
    Car,
    Truck,
    Motorcycle,
    Boat,
    Plane
}

[System.Serializable]
public class VehicleStats
{
    [Header("Performance")]
    public float maxSpeed = 50f;
    public float acceleration = 10f;
    public float brakeForce = 15f;
    public float reverseSpeed = 20f;
    
    [Header("Handling")]
    public float turnSpeed = 100f;
    public float traction = 1f;
    public float stability = 0.8f;
    
    [Header("Physics")]
    public float mass = 1000f;
    public float centerOfMassOffset = -0.5f; // Lower = more stable
}

public class Vehicle : MonoBehaviour
{
    [Header("Vehicle Settings")]
    public VehicleType vehicleType = VehicleType.Car;
    public VehicleStats stats = new VehicleStats();
    public string vehicleName = "Vehicle";
    
    [Header("Components")]
    public Transform[] wheels; // Assign wheel transforms for visual rotation
    public Transform centerOfMass; // Optional: custom center of mass point
    public ParticleSystem exhaustEffect;
    
    [Header("Audio")]
    public AudioSource engineAudioSource;
    public AudioClip engineStartSound;
    public AudioClip engineIdleSound;
    public AudioClip engineRunningSound;
    public AudioClip brakeSound;
    public AudioClip hornSound;
    
    [Header("Fuel System")]
    public bool requiresFuel = true;
    public float maxFuel = 100f;
    public float fuelConsumptionRate = 1f; // Fuel per second while driving
    public float currentFuel = 100f;
    
    [Header("Access Control")]
    public bool requiresKey = false; // Does this vehicle need a key?
    public string requiredKeyName = "Car Key"; // Name of key item needed
    public bool isLocked = false; // Is the vehicle currently locked?
    public string ownerPlayerName = ""; // Who owns this vehicle (empty = anyone can use)
    public bool allowFriends = true; // Can friends/party members drive?
    public bool allowAnyone = false; // Public vehicle anyone can use?
    
    [Header("Interaction")]
    public Transform[] seatPositions; // Where players sit
    public float interactionRange = 3f;
    public KeyCode enterExitKey = KeyCode.E;
    public bool allowMultiplePassengers = true;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool enableDebugLogs = false;
    
    // Private variables
    private Rigidbody vehicleRigidbody;
    private bool isEngineOn = false;
    private bool isPlayerDriving = false;
    private GameObject currentDriver;
    private float currentSpeed;
    private float motorInput;
    private float steerInput;
    private bool brakeInput;
    private bool hornInput;
    
    // Audio management
    private float targetEngineVolume = 0f;
    private float targetEnginePitch = 1f;
    
    // Events
    public System.Action<bool> OnEngineStateChanged; // true = on, false = off
    public System.Action<GameObject> OnPlayerEnterVehicle;
    public System.Action<GameObject> OnPlayerExitVehicle;
    public System.Action OnOutOfFuel;
    
    void Start()
    {
        InitializeVehicle();
    }
    
    void InitializeVehicle()
    {
        // Get or add rigidbody
        vehicleRigidbody = GetComponent<Rigidbody>();
        if (vehicleRigidbody == null)
        {
            vehicleRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        // Set up physics
        vehicleRigidbody.mass = stats.mass;
        vehicleRigidbody.isKinematic = false; // Ensure vehicle can move
        vehicleRigidbody.useGravity = true; // Ensure gravity works
        
        // Set center of mass
        if (centerOfMass != null)
        {
            vehicleRigidbody.centerOfMass = centerOfMass.localPosition;
        }
        else
        {
            vehicleRigidbody.centerOfMass = new Vector3(0, stats.centerOfMassOffset, 0);
        }
        
        // Set up audio - use existing AudioSource from inspector
        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.volume = 0f;
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning("No Engine Audio Source assigned in Vehicle inspector!");
        }
        
        // Initialize fuel
        currentFuel = maxFuel;
        
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle '{vehicleName}' initialized - Type: {vehicleType}, Max Speed: {stats.maxSpeed}");
        }
    }
    
    void Update()
    {
        // Check for player interaction
        CheckPlayerInteraction();
        
        // Handle input if player is driving
        if (isPlayerDriving)
        {
            HandleInput();
        }
        
        // Update audio
        UpdateAudio();
        
        // Update effects
        UpdateEffects();
        
        // Update fuel consumption
        if (isEngineOn && requiresFuel)
        {
            ConsumeFuel();
        }
        
        // Update current speed for reference
        currentSpeed = vehicleRigidbody.linearVelocity.magnitude;
    }
    
    void FixedUpdate()
    {
        if (isPlayerDriving && isEngineOn && HasFuel())
        {
            ApplyMotorForce();
            ApplySteering();
            ApplyBraking();
        }
        
        // Apply downforce for stability
        ApplyDownforce();
    }
    
    void CheckPlayerInteraction()
    {
        // Don't check interaction if UI is open
        try
        {
            if (PlayerInputManager.ShouldDisableInput())
                return;
        }
        catch
        {
            // PlayerInputManager might not exist, continue without it
        }
        
        // Find nearby player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            if (enableDebugLogs)
                Debug.Log("No player found with 'Player' tag");
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        
        if (distanceToPlayer <= interactionRange)
        {
            // Check if player can access this vehicle
            bool canAccess = CanPlayerAccessVehicle(player);
            string accessMessage = GetAccessMessage(player);
            
            if (enableDebugLogs)
            {
                Debug.Log($"Player in range. Can access: {canAccess}, Message: {accessMessage}");
            }
            
            // Check for enter/exit input
            if (Input.GetKeyDown(enterExitKey))
            {
                if (enableDebugLogs)
                    Debug.Log($"E key pressed. Currently driving: {isPlayerDriving}");
                
                if (isPlayerDriving)
                {
                    ExitVehicle();
                }
                else if (canAccess)
                {
                    EnterVehicle(player);
                }
                else
                {
                    // Show why they can't access it
                    ShowAccessDeniedMessage(accessMessage);
                }
            }
        }
    }
    
    void HandleInput()
    {
        // Don't handle vehicle input if UI is open
        if (PlayerInputManager.ShouldDisableInput())
        {
            motorInput = 0f;
            steerInput = 0f;
            brakeInput = false;
            hornInput = false;
            return;
        }
        
        // Motor input (W/S or Up/Down)
        motorInput = Input.GetAxis("Vertical");
        
        // Steering input (A/D or Left/Right)
        steerInput = Input.GetAxis("Horizontal");
        
        // Brake input (Space)
        brakeInput = Input.GetKey(KeyCode.Space);
        
        // Horn input (H)
        hornInput = Input.GetKeyDown(KeyCode.H);
        
        // Engine on/off (F)
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleEngine();
        }
        
        // Handle horn
        if (hornInput && hornSound != null)
        {
            AudioSource.PlayClipAtPoint(hornSound, transform.position);
        }
    }
    
    void ApplyMotorForce()
    {
        if (Mathf.Abs(motorInput) > 0.1f)
        {
            Vector3 motorForce = transform.forward * motorInput * stats.acceleration * 1000f;
            
            // Limit max speed
            float maxSpeedToUse = motorInput > 0 ? stats.maxSpeed : stats.reverseSpeed;
            if (currentSpeed < maxSpeedToUse)
            {
                vehicleRigidbody.AddForce(motorForce);
            }
        }
    }
    
    void ApplySteering()
    {
        if (Mathf.Abs(steerInput) > 0.1f && currentSpeed > 1f)
        {
            // Steering is more effective at higher speeds
            float steerForce = steerInput * stats.turnSpeed * (currentSpeed / stats.maxSpeed);
            steerForce = Mathf.Clamp(steerForce, -stats.turnSpeed, stats.turnSpeed);
            
            vehicleRigidbody.AddTorque(Vector3.up * steerForce);
        }
    }
    
    void ApplyBraking()
    {
        if (brakeInput)
        {
            // Apply brake force opposite to velocity
            Vector3 brakeForce = -vehicleRigidbody.linearVelocity.normalized * stats.brakeForce * 1000f;
            vehicleRigidbody.AddForce(brakeForce);
            
            // Play brake sound
            if (brakeSound != null && !engineAudioSource.isPlaying)
            {
                AudioSource.PlayClipAtPoint(brakeSound, transform.position, 0.5f);
            }
        }
    }
    
    void ApplyDownforce()
    {
        // Apply downward force for stability at high speeds
        float downforce = currentSpeed * stats.stability * 10f;
        vehicleRigidbody.AddForce(Vector3.down * downforce);
    }
    
    void UpdateAudio()
    {
        if (engineAudioSource == null) return;
        
        if (isEngineOn)
        {
            // Set target volume and pitch based on engine state
            if (Mathf.Abs(motorInput) > 0.1f)
            {
                targetEngineVolume = 0.8f;
                targetEnginePitch = 1f + (currentSpeed / stats.maxSpeed) * 0.5f;
                
                if (engineAudioSource.clip != engineRunningSound)
                {
                    engineAudioSource.clip = engineRunningSound;
                    engineAudioSource.Play();
                }
            }
            else
            {
                targetEngineVolume = 0.3f;
                targetEnginePitch = 1f;
                
                if (engineAudioSource.clip != engineIdleSound)
                {
                    engineAudioSource.clip = engineIdleSound;
                    engineAudioSource.Play();
                }
            }
        }
        else
        {
            targetEngineVolume = 0f;
            targetEnginePitch = 0f;
        }
        
        // Smoothly adjust audio
        engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetEngineVolume, Time.deltaTime * 3f);
        engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetEnginePitch, Time.deltaTime * 2f);
    }
    
    void UpdateEffects()
    {
        // Rotate wheels based on speed
        if (wheels != null && wheels.Length > 0)
        {
            float wheelRotationSpeed = currentSpeed * 360f / (2f * Mathf.PI * 1f); // Assuming 1m wheel radius
            foreach (Transform wheel in wheels)
            {
                if (wheel != null)
                {
                    wheel.Rotate(Vector3.right, wheelRotationSpeed * Time.deltaTime);
                }
            }
        }
        
        // Handle exhaust effects
        if (exhaustEffect != null)
        {
            if (isEngineOn && Mathf.Abs(motorInput) > 0.1f)
            {
                if (!exhaustEffect.isPlaying)
                    exhaustEffect.Play();
            }
            else
            {
                if (exhaustEffect.isPlaying)
                    exhaustEffect.Stop();
            }
        }
    }
    
    void ConsumeFuel()
    {
        if (Mathf.Abs(motorInput) > 0.1f)
        {
            currentFuel -= fuelConsumptionRate * Time.deltaTime;
            currentFuel = Mathf.Max(currentFuel, 0f);
            
            if (currentFuel <= 0f)
            {
                OnOutOfFuel?.Invoke();
                TurnOffEngine();
                
                if (enableDebugLogs)
                {
                    Debug.Log($"Vehicle '{vehicleName}' ran out of fuel!");
                }
            }
        }
    }
    
    public void EnterVehicle(GameObject player)
    {
        if (isPlayerDriving) return;
        
        isPlayerDriving = true;
        currentDriver = player;
        
        // Disable the Player script specifically
        var playerScript = player.GetComponent<Player>();
        if (playerScript != null)
        {
            playerScript.enabled = false;
        }
        
        // Also disable CharacterController as backup
        var playerController = player.GetComponent<CharacterController>();
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Position player in vehicle (only if seat positions are assigned)
        if (seatPositions != null && seatPositions.Length > 0 && seatPositions[0] != null)
        {
            player.transform.position = seatPositions[0].position;
            player.transform.rotation = seatPositions[0].rotation;
            player.transform.SetParent(transform);
        }
        else
        {
            // Just parent the player to the vehicle without moving them
            player.transform.SetParent(transform);
        }
        
        // Start engine automatically
        TurnOnEngine();
        
        OnPlayerEnterVehicle?.Invoke(player);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Player entered vehicle '{vehicleName}'");
        }
    }
    
    public void ExitVehicle()
    {
        if (!isPlayerDriving || currentDriver == null) return;
        
        GameObject player = currentDriver;
        
        // Re-enable the Player script
        var playerScript = player.GetComponent<Player>();
        if (playerScript != null)
        {
            playerScript.enabled = true;
        }
        
        // Re-enable CharacterController
        var playerController = player.GetComponent<CharacterController>();
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        // Position player outside vehicle
        Vector3 exitPosition = transform.position + transform.right * 2f;
        
        // Unparent first, then move
        player.transform.SetParent(null);
        player.transform.position = exitPosition;
        
        OnPlayerExitVehicle?.Invoke(player);
        
        currentDriver = null;
        isPlayerDriving = false;
        
        // Turn off engine
        TurnOffEngine();
        
        if (enableDebugLogs)
        {
            Debug.Log($"Player exited vehicle '{vehicleName}'");
        }
    }
    
    public void TurnOnEngine()
    {
        if (isEngineOn || !HasFuel()) return;
        
        isEngineOn = true;
        
        // Play engine start sound
        if (engineStartSound != null)
        {
            AudioSource.PlayClipAtPoint(engineStartSound, transform.position);
        }
        
        OnEngineStateChanged?.Invoke(true);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle '{vehicleName}' engine started");
        }
    }
    
    public void TurnOffEngine()
    {
        if (!isEngineOn) return;
        
        isEngineOn = false;
        
        // Stop audio
        if (engineAudioSource != null)
        {
            engineAudioSource.Stop();
        }
        
        OnEngineStateChanged?.Invoke(false);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle '{vehicleName}' engine stopped");
        }
    }
    
    public void ToggleEngine()
    {
        if (isEngineOn)
        {
            TurnOffEngine();
        }
        else
        {
            TurnOnEngine();
        }
    }
    
    public void AddFuel(float amount)
    {
        currentFuel += amount;
        currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Added {amount} fuel to '{vehicleName}'. Current fuel: {currentFuel:F1}/{maxFuel}");
        }
    }
    
    public bool HasFuel()
    {
        return !requiresFuel || currentFuel > 0f;
    }
    
    public bool CanPlayerAccessVehicle(GameObject player)
    {
        // If vehicle is locked, check for key
        if (isLocked || requiresKey)
        {
            if (!PlayerHasKey(player))
            {
                return false;
            }
        }
        
        // Check ownership permissions
        if (!string.IsNullOrEmpty(ownerPlayerName))
        {
            string playerName = GetPlayerName(player);
            
            // Owner can always access
            if (playerName == ownerPlayerName)
            {
                return true;
            }
            
            // Check friend/party access
            if (allowFriends && IsPlayerFriend(player))
            {
                return true;
            }
            
            // Check public access
            if (allowAnyone)
            {
                return true;
            }
            
            // Denied - not owner and no permissions
            return false;
        }
        
        // No owner set - anyone can access if not locked
        return !isLocked || PlayerHasKey(player);
    }
    
    public string GetAccessMessage(GameObject player)
    {
        if (isLocked && !PlayerHasKey(player))
        {
            if (requiresKey)
            {
                return $"Vehicle locked. Need {requiredKeyName}";
            }
            return "Vehicle is locked";
        }
        
        if (!string.IsNullOrEmpty(ownerPlayerName))
        {
            string playerName = GetPlayerName(player);
            
            if (playerName != ownerPlayerName && !allowFriends && !allowAnyone)
            {
                return $"This vehicle belongs to {ownerPlayerName}";
            }
        }
        
        return "Press E to enter vehicle";
    }
    
    private bool PlayerHasKey(GameObject player)
    {
        if (!requiresKey) return true;
        
        // Check player's inventory for the key
        InventorySystem playerInventory = player.GetComponent<InventorySystem>();
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<InventorySystem>();
        }
        
        if (playerInventory != null)
        {
            // Use the HasItem method to check for the required key
            return playerInventory.HasItem(requiredKeyName, 1);
        }
        
        return false;
    }
    
    private string GetPlayerName(GameObject player)
    {
        // Try to get player name from various possible components
        // You might have a PlayerInfo, PlayerData, or similar component
        
        // For now, use the GameObject name as fallback
        return player.name;
    }
    
    private bool IsPlayerFriend(GameObject player)
    {
        // This would integrate with your friend/party system
        // For now, return true as placeholder
        // You could check a friends list, party members, etc.
        return allowFriends;
    }
    
    private void ShowAccessDeniedMessage(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle access denied: {message}");
        }
        
        // Here you could show a UI notification, play a sound, etc.
        // For example:
        // UIManager.ShowNotification(message);
        // AudioSource.PlayClipAtPoint(deniedSound, transform.position);
    }
    
    public void SetOwner(string playerName)
    {
        ownerPlayerName = playerName;
        
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle '{vehicleName}' ownership set to: {playerName}");
        }
    }
    
    public void LockVehicle()
    {
        isLocked = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle '{vehicleName}' locked");
        }
    }
    
    public void UnlockVehicle()
    {
        isLocked = false;
        
        if (enableDebugLogs)
        {
            Debug.Log($"Vehicle '{vehicleName}' unlocked");
        }
    }
    
    public float GetFuelPercentage()
    {
        return currentFuel / maxFuel;
    }
    
    public float GetSpeedKmh()
    {
        return currentSpeed * 3.6f; // Convert m/s to km/h
    }
    
    public bool IsMoving()
    {
        return currentSpeed > 1f;
    }
    
    // Public getters
    public bool IsEngineOn => isEngineOn;
    public bool IsPlayerDriving => isPlayerDriving;
    public float CurrentSpeed => currentSpeed;
    public float CurrentFuel => currentFuel;
    public GameObject CurrentDriver => currentDriver;
    
    void OnGUI()
    {
        if (!showDebugInfo)
            return;
        
        // Vehicle HUD when driving
        if (isPlayerDriving)
        {
            GUI.Box(new Rect(10, 10, 200, 120), "Vehicle Info");
            GUILayout.BeginArea(new Rect(15, 35, 190, 100));
            
            GUILayout.Label($"Vehicle: {vehicleName}");
            GUILayout.Label($"Speed: {GetSpeedKmh():F0} km/h");
            GUILayout.Label($"Engine: {(isEngineOn ? "ON" : "OFF")}");
            
            if (requiresFuel)
            {
                GUILayout.Label($"Fuel: {currentFuel:F0}/{maxFuel}");
            }
            
            GUILayout.Label("Controls:");
            GUILayout.Label("WASD - Drive, Space - Brake");
            GUILayout.Label("F - Engine, H - Horn, E - Exit");
            
            GUILayout.EndArea();
        }
        // Access status when near vehicle
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= interactionRange)
                {
                    string accessMessage = GetAccessMessage(player);
                    bool canAccess = CanPlayerAccessVehicle(player);
                    
                    GUI.color = canAccess ? Color.green : Color.red;
                    GUI.Box(new Rect(10, Screen.height - 100, 250, 60), "Vehicle Access");
                    GUI.color = Color.white;
                    
                    GUILayout.BeginArea(new Rect(15, Screen.height - 85, 240, 50));
                    GUILayout.Label($"Vehicle: {vehicleName}");
                    GUILayout.Label(accessMessage);
                    
                    if (!string.IsNullOrEmpty(ownerPlayerName))
                    {
                        GUILayout.Label($"Owner: {ownerPlayerName}");
                    }
                    
                    GUILayout.EndArea();
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
        
        // Draw center of mass
        Gizmos.color = Color.red;
        Vector3 com = centerOfMass != null ? centerOfMass.position : transform.position + new Vector3(0, stats.centerOfMassOffset, 0);
        Gizmos.DrawWireSphere(com, 0.2f);
        
        // Draw seat positions
        if (seatPositions != null)
        {
            Gizmos.color = Color.blue;
            foreach (Transform seat in seatPositions)
            {
                if (seat != null)
                {
                    Gizmos.DrawWireCube(seat.position, Vector3.one * 0.5f);
                }
            }
        }
    }
}
