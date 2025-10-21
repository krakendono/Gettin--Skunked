using UnityEngine;
using System.Collections;

public class DebugHitDetectionGun : MonoBehaviour, IDamageable
{
    [Header("Hit Detection Settings")]
    public float health = 100f;
    public float flashDuration = 0.1f;
    public Color hitColor = Color.red;
    public bool showDebugInfo = true;
    
    [Header("Audio")]
    public AudioClip hitSound;
    
    // Private variables
    private Renderer objectRenderer;
    private Material originalMaterial;
    private Material hitMaterial;
    private AudioSource audioSource;
    private bool isFlashing = false;
    private float currentHealth;
    
    void Start()
    {
        // Initialize health
        currentHealth = health;
        
        // Get renderer component
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError($"DebugHitDetectionGun on {gameObject.name} requires a Renderer component!");
            return;
        }
        
        // Store original material
        originalMaterial = objectRenderer.material;
        
        // Create hit material
        CreateHitMaterial();
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.playOnAwake = false;
    }
    
    void CreateHitMaterial()
    {
        // Create a new material for the hit effect
        hitMaterial = new Material(originalMaterial);
        
        // If it's using Standard shader, change the albedo color
        if (hitMaterial.HasProperty("_Color"))
        {
            hitMaterial.color = hitColor;
        }
        // If it's using HDRP Lit shader, change the base color
        else if (hitMaterial.HasProperty("_BaseColor"))
        {
            hitMaterial.SetColor("_BaseColor", hitColor);
        }
        // If it's using URP Lit shader, change the base color
        else if (hitMaterial.HasProperty("_MainTex"))
        {
            hitMaterial.color = hitColor;
        }
        
        // Make it slightly emissive for better visibility
        if (hitMaterial.HasProperty("_EmissionColor"))
        {
            hitMaterial.SetColor("_EmissionColor", hitColor * 0.3f);
            hitMaterial.EnableKeyword("_EMISSION");
        }
    }
    
    public void TakeDamage(float damageAmount)
    {
        // Reduce health
        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        // Flash red
        if (!isFlashing)
        {
            StartCoroutine(FlashRed());
        }
        
        // Play hit sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
        
        // Debug info
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} took {damageAmount} damage! Health: {currentHealth}/{health}");
        }
        
        // Check if destroyed
        if (currentHealth <= 0f)
        {
            OnDestroyed();
        }
    }
    
    IEnumerator FlashRed()
    {
        if (objectRenderer == null || hitMaterial == null)
            yield break;
            
        isFlashing = true;
        
        // Change to hit material
        objectRenderer.material = hitMaterial;
        
        // Wait for flash duration
        yield return new WaitForSeconds(flashDuration);
        
        // Change back to original material
        if (objectRenderer != null && originalMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }
        
        isFlashing = false;
    }
    
    void OnDestroyed()
    {
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} has been destroyed!");
        }
        
        // You can add destruction effects here
        // For now, just disable the object
        gameObject.SetActive(false);
        
        // Or destroy it completely after a delay
        // Destroy(gameObject, 1f);
    }
    
    // Method to reset health (useful for testing)
    public void ResetHealth()
    {
        currentHealth = health;
        gameObject.SetActive(true);
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} health reset to {health}");
        }
    }
    
    // Method to manually trigger hit effect (for testing)
    [ContextMenu("Test Hit Effect")]
    public void TestHitEffect()
    {
        TakeDamage(10f);
    }
    
    void OnGUI()
    {
        if (!showDebugInfo)
            return;
            
        // Show health bar above object
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        
        if (screenPos.z > 0) // Only show if in front of camera
        {
            screenPos.y = Screen.height - screenPos.y; // Flip Y coordinate
            
            float barWidth = 100f;
            float barHeight = 10f;
            Rect barRect = new Rect(screenPos.x - barWidth/2, screenPos.y, barWidth, barHeight);
            
            // Background
            GUI.color = Color.black;
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            
            // Health bar
            float healthPercent = currentHealth / health;
            GUI.color = Color.Lerp(Color.red, Color.green, healthPercent);
            Rect healthRect = new Rect(barRect.x, barRect.y, barRect.width * healthPercent, barRect.height);
            GUI.DrawTexture(healthRect, Texture2D.whiteTexture);
            
            // Reset color
            GUI.color = Color.white;
            
            // Health text
            GUI.Label(new Rect(screenPos.x - 50, screenPos.y + 15, 100, 20), 
                     $"{currentHealth:F0}/{health:F0}", 
                     new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
    }
    
    void OnDestroy()
    {
        // Clean up materials
        if (hitMaterial != null && hitMaterial != originalMaterial)
        {
            DestroyImmediate(hitMaterial);
        }
    }
}
