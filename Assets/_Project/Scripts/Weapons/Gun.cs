using UnityEngine;

[System.Serializable]
public enum FireMode
{
    SemiAuto,    // Single shot per click (pistol)
    FullAuto,    // Continuous fire while held (machine gun)
    Burst        // Fire multiple rounds per click
}

public class Gun : MonoBehaviour
{
    [Header("Gun Settings")]
    public string gunName = "Pistol";
    public FireMode fireMode = FireMode.SemiAuto;
    public float damage = 25f;
    public float range = 100f;
    public float fireRate = 600f; // Rounds per minute
    public int burstCount = 3; // For burst mode only
    
    [Header("Ammo")]
    public int magazineSize = 12;
    public int currentAmmo = 12;
    public int totalAmmo = 120;
    public float reloadTime = 2f;
    
    [Header("Recoil & Accuracy")]
    public bool enableRecoil = false; // Toggle recoil on/off
    public float recoilForce = 2f;
    public float maxRecoilAngle = 5f;
    public float recoilRecoverySpeed = 10f;
    public float spreadAngle = 1f; // Bullet spread in degrees
    public bool enableSpread = false;
    
    [Header("Effects")]
    public Transform firePoint;
    public ParticleSystem muzzleFlashParticles;
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
    
    [Header("Aiming Line")]
    public bool showAimingLine = true;
    public LineRenderer aimingLineRenderer; // Assign your existing LineRenderer here
    public Color aimingLineColor = Color.red;
    public float lineSmoothSpeed = 10f;
    
    [Header("UI")]
    public bool showDebugInfo = true;
    public bool enableDebugLogs = false; // Toggle debug console output
    
    // Private variables
    private float nextFireTime = 0f;
    private bool isReloading = false;
    private bool isFireing = false;
    private bool justFired = false; // Track if we just fired this frame for debug timing
    private int burstShotsFired = 0;
    private Camera playerCamera;
    private AudioSource audioSource;
    private Vector2 currentRecoil = Vector2.zero;
    private Vector2 targetRecoil = Vector2.zero;
    
    // Shared raycast results for both aiming and shooting
    private Vector3 currentAimOrigin;
    private Vector3 currentAimDirection;
    private Vector3 currentAimEndPoint;
    private RaycastHit currentAimHit;
    private bool currentAimHitSomething;
    
    void Start()
    {
        // Get components
        playerCamera = GetComponentInParent<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        // Initialize ammo
        currentAmmo = magazineSize;
        
        // Set fire point if not assigned
        if (firePoint == null)
            firePoint = transform;
            
        // Setup LineRenderer for aiming line
        SetupAimingLine();
    }

    void Update()
    {
        // Reset the justFired flag each frame
        justFired = false;
        
        HandleInput();
        HandleRecoil();
        
        // Auto fire for full auto weapons
        if (fireMode == FireMode.FullAuto && isFireing && !isReloading)
        {
            if (Time.time >= nextFireTime)
            {
                Fire();
            }
        }
    }
    
    void LateUpdate()
    {
        // Calculate aim once and use for both line and shooting
        CalculateAim();
        
        // Update aiming line after all other updates are complete
        UpdateAimingLine();
    }
    
    void CalculateAim()
    {
        // Single calculation used by both aiming line and shooting
        currentAimOrigin = GetShootOrigin();
        currentAimDirection = GetShootDirection(false); // false = no spread for aiming
        
        // Perform the raycast once
        currentAimHitSomething = Physics.Raycast(currentAimOrigin, currentAimDirection, out currentAimHit, range);
        
        if (currentAimHitSomething)
        {
            currentAimEndPoint = currentAimHit.point;
        }
        else
        {
            currentAimEndPoint = currentAimOrigin + (currentAimDirection * range);
        }
    }
    
    void HandleInput()
    {
        // Check if input should be disabled (UI open or cursor unlocked)
        if (PlayerInputManager.ShouldDisableInput())
            return;
        
        // Fire input
        bool firePressed = Input.GetMouseButtonDown(0);
        bool fireHeld = Input.GetMouseButton(0);
        bool fireReleased = Input.GetMouseButtonUp(0);
        
        // Reload input
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < magazineSize)
        {
            StartReload();
        }
        
        // Toggle aiming line
        if (Input.GetKeyDown(KeyCode.L))
        {
            showAimingLine = !showAimingLine;
        }
        
        // Handle different fire modes
        switch (fireMode)
        {
            case FireMode.SemiAuto:
                if (firePressed && !isReloading)
                {
                    Fire();
                }
                break;
                
            case FireMode.FullAuto:
                if (firePressed && !isReloading)
                {
                    isFireing = true;
                    Fire();
                }
                if (fireReleased)
                {
                    isFireing = false;
                }
                break;
                
            case FireMode.Burst:
                if (firePressed && !isReloading && burstShotsFired == 0)
                {
                    StartCoroutine(FireBurst());
                }
                break;
        }
    }
    
    void Fire()
    {
        // Check if we can fire
        if (Time.time < nextFireTime || isReloading)
            return;
            
        // Check ammo
        if (currentAmmo <= 0)
        {
            PlaySound(emptySound);
            return;
        }
        
        // Mark that we just fired for debug purposes
        justFired = true;
        
        // Calculate next fire time based on fire rate
        nextFireTime = Time.time + (60f / fireRate);
        
        // Consume ammo
        currentAmmo--;
        
        // Perform raycast
        PerformRaycast();
        
        // Apply recoil
        ApplyRecoil();
        
        // Play effects
        PlayFireEffects();
        
        // Auto reload when empty
        if (currentAmmo <= 0)
        {
            StartReload();
        }
    }
    
    void PerformRaycast()
    {
        // Use the pre-calculated aim results (same as aiming line)
        Vector3 shootOrigin = currentAimOrigin;
        Vector3 shootDirection = currentAimDirection;
        Vector3 endPoint = currentAimEndPoint;
        bool hitSomething = currentAimHitSomething;
        RaycastHit hit = currentAimHit;
        
        // Debug output
        if (enableDebugLogs)
        {
            if (hitSomething)
            {
                Debug.Log($"FIRE: Start={shootOrigin} | End={endPoint} | Hit={hit.collider.name} | Distance={Vector3.Distance(shootOrigin, endPoint):F2}");
            }
            else
            {
                Debug.Log($"FIRE: Start={shootOrigin} | End={endPoint} | Hit=NOTHING | Max Range={range}");
            }
        }
        
        // Apply damage if we hit something
        if (hitSomething)
        {
            // Check if it's an enemy and apply damage
            var enemy = hit.collider.GetComponent<IDamageable>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                if (enableDebugLogs)
                {
                    Debug.Log($"Damage Applied: {damage} to {hit.collider.name}");
                }
            }
        }
        
        // Debug ray in scene view
        Debug.DrawRay(shootOrigin, shootDirection * range, Color.red, 0.1f);
    }
    
    Vector3 GetShootOrigin()
    {
        return firePoint.position;
    }
    
    Vector3 GetShootDirection(bool applySpread = false)
    {
        Vector3 direction = firePoint.forward;
        
        // Add random spread if enabled in inspector, requested by caller, and spread angle > 0
        if (enableSpread && applySpread && spreadAngle > 0f)
        {
            float spreadX = Random.Range(-spreadAngle, spreadAngle);
            float spreadY = Random.Range(-spreadAngle, spreadAngle);
            
            // Apply spread to the direction
            Quaternion spreadRotation = Quaternion.Euler(spreadY, spreadX, 0);
            direction = spreadRotation * direction;
        }
        
        return direction;
    }
    
    void ApplyRecoil()
    {
        // Only apply recoil if enabled
        if (!enableRecoil)
            return;
            
        // Add recoil to target
        float recoilX = Random.Range(-maxRecoilAngle, maxRecoilAngle) * 0.5f;
        float recoilY = Random.Range(0f, maxRecoilAngle);
        
        targetRecoil += new Vector2(recoilX, recoilY) * recoilForce;
        targetRecoil = Vector2.ClampMagnitude(targetRecoil, maxRecoilAngle);
    }
    
    void HandleRecoil()
    {
        // Only process recoil if enabled and there's recoil to handle
        if (!enableRecoil || (targetRecoil.magnitude <= 0.01f && currentRecoil.magnitude <= 0.01f))
            return;
            
        // Smoothly apply recoil to camera
        currentRecoil = Vector2.Lerp(currentRecoil, targetRecoil, Time.deltaTime * 15f);
        
        // Apply recoil to camera rotation
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation *= Quaternion.Euler(-currentRecoil.y, currentRecoil.x, 0f);
        }
        
        // Recover from recoil
        targetRecoil = Vector2.Lerp(targetRecoil, Vector2.zero, Time.deltaTime * recoilRecoverySpeed);
        
        // Stop recoil when it gets very small
        if (targetRecoil.magnitude < 0.01f)
        {
            targetRecoil = Vector2.zero;
        }
        if (currentRecoil.magnitude < 0.01f)
        {
            currentRecoil = Vector2.zero;
        }
    }
    
    void PlayFireEffects()
    {
        // Muzzle flash particle system
        if (muzzleFlashParticles != null)
        {
            // Set the particle system to world space so it doesn't move with the gun
            var main = muzzleFlashParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            
            // Move particle system to fire point position
            muzzleFlashParticles.transform.position = firePoint.position;
            muzzleFlashParticles.transform.rotation = firePoint.rotation;
            
            // Play the particle system
            muzzleFlashParticles.Play();
        }
        
        // Fire sound
        PlaySound(fireSound);
    }
    
    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    void StartReload()
    {
        if (isReloading || totalAmmo <= 0)
            return;
            
        StartCoroutine(ReloadCoroutine());
    }
    
    System.Collections.IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        PlaySound(reloadSound);
        
        yield return new WaitForSeconds(reloadTime);
        
        // Calculate how much ammo to reload
        int ammoNeeded = magazineSize - currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, totalAmmo);
        
        currentAmmo += ammoToReload;
        totalAmmo -= ammoToReload;
        
        isReloading = false;
    }
    
    System.Collections.IEnumerator FireBurst()
    {
        burstShotsFired = 0;
        
        while (burstShotsFired < burstCount && currentAmmo > 0 && !isReloading)
        {
            Fire();
            burstShotsFired++;
            
            if (burstShotsFired < burstCount)
            {
                yield return new WaitForSeconds(60f / fireRate);
            }
        }
        
        burstShotsFired = 0;
    }
    
    void SetupAimingLine()
    {
        // Use the existing LineRenderer you assigned in the inspector
        if (aimingLineRenderer != null)
        {
            // Configure basic settings (your existing LineRenderer may already have these set)
            aimingLineRenderer.positionCount = 2;
            aimingLineRenderer.useWorldSpace = true;
            aimingLineRenderer.startColor = aimingLineColor;
            aimingLineRenderer.endColor = aimingLineColor;
            
            // Initially set visibility
            aimingLineRenderer.enabled = showAimingLine;
        }
        else
        {
            Debug.LogWarning("No LineRenderer assigned to Gun script! Please assign your existing LineRenderer in the inspector.");
        }
    }
    
    void UpdateAimingLine()
    {
        if (aimingLineRenderer == null || !showAimingLine)
        {
            if (aimingLineRenderer != null)
                aimingLineRenderer.enabled = false;
            return;
        }
        
        aimingLineRenderer.enabled = true;
        
        // Use the pre-calculated aim results (identical to shooting)
        Vector3 shootOrigin = currentAimOrigin;
        Vector3 endPoint = currentAimEndPoint;
        bool hitSomething = currentAimHitSomething;
        RaycastHit hit = currentAimHit;
        
        // Debug output only when firing
        if (justFired && enableDebugLogs)
        {
            if (hitSomething)
            {
                Debug.Log($"AIM LINE: Start={shootOrigin} | End={endPoint} | Hit={hit.collider.name} | Distance={Vector3.Distance(shootOrigin, endPoint):F2}");
            }
            else
            {
                Debug.Log($"AIM LINE: Start={shootOrigin} | End={endPoint} | Hit=NOTHING | Max Range={range}");
            }
        }
        
        // Set line positions
        aimingLineRenderer.SetPosition(0, shootOrigin);
        aimingLineRenderer.SetPosition(1, endPoint);
        
        // Update color based on what we're aiming at
        Color currentColor = aimingLineColor;
        if (hitSomething)
        {
            // Check if we're aiming at something damageable
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                currentColor = Color.red; // Red when aiming at enemy
            }
            else
            {
                currentColor = Color.yellow; // Yellow when aiming at other objects
            }
        }
        else
        {
            currentColor = Color.green; // Green when aiming at nothing
        }
        
        // Set color
        aimingLineRenderer.startColor = currentColor;
        aimingLineRenderer.endColor = currentColor;
    }
    
    void OnGUI()
    {
        if (!showDebugInfo)
            return;
            
        GUILayout.BeginArea(new Rect(10, 10, 200, 180));
        GUILayout.Label($"Gun: {gunName}");
        GUILayout.Label($"Fire Mode: {fireMode}");
        GUILayout.Label($"Ammo: {currentAmmo}/{magazineSize}");
        GUILayout.Label($"Total Ammo: {totalAmmo}");
        GUILayout.Label($"Reloading: {isReloading}");
        GUILayout.Label("Press R to Reload");
        GUILayout.Label($"Aiming Line: {(showAimingLine ? "ON" : "OFF")}");
        GUILayout.Label("Press L to Toggle Line");
        GUILayout.EndArea();
    }
}

// Interface for damageable objects
public interface IDamageable
{
    void TakeDamage(float damage);
}
