using UnityEngine;

/// <summary>
/// Utility class to check if player input should be disabled due to UI state
/// </summary>
public static class PlayerInputManager
{
    private static InventorySystem inventorySystem;
    private static CraftingSystem craftingSystem;
    
    /// <summary>
    /// Check if any UI is open or cursor is unlocked, which should disable weapon/player input
    /// </summary>
    public static bool ShouldDisableInput()
    {
        // Cache systems if not found yet
        if (inventorySystem == null)
            inventorySystem = Object.FindFirstObjectByType<InventorySystem>();
        if (craftingSystem == null)
            craftingSystem = Object.FindFirstObjectByType<CraftingSystem>();
        
        // Check cursor state
        if (Cursor.lockState != CursorLockMode.Locked)
            return true;
        
        // Check if inventory is open
        if (inventorySystem != null && inventorySystem.IsInventoryOpen())
            return true;
        
        // Check if crafting is open
        if (craftingSystem != null && craftingSystem.IsCraftingMenuOpen())
            return true;
        
        return false;
    }
    
    /// <summary>
    /// Check if player movement should be allowed
    /// </summary>
    public static bool CanPlayerMove()
    {
        return !ShouldDisableInput();
    }
    
    /// <summary>
    /// Check if weapons should be able to fire/attack
    /// </summary>
    public static bool CanUseWeapons()
    {
        return !ShouldDisableInput();
    }
}